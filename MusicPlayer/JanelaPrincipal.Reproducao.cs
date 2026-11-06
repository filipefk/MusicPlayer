using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using MusicPlayer.Nucleo;
using System.Diagnostics;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace MusicPlayer;

public sealed partial class JanelaPrincipal
{
    private const int IntervaloPosicaoMs = 250;
    // "Anterior" depois deste tempo volta ao início da música atual, como nos tocadores comuns.
    private static readonly TimeSpan LimiteReiniciarMusica = TimeSpan.FromSeconds(3);

    private const string GlifoTocar = "";
    private const string GlifoPausar = "";
    private const string GlifoRepetirTodas = "";
    private const string GlifoRepetirUma = "";
    private const string GlifoVolume = "";
    private const string GlifoMudo = "";

    private MediaPlayer _player = null!;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer _temporizadorPosicao = null!;
    private MediaSource? _fonteAtual;
    private StorageItemThumbnail? _capaAtual;
    // Cada carga ganha um número: se outra música for pedida no meio do caminho, a anterior é descartada.
    private int _versaoCarga;
    private TimeSpan? _posicaoPendente;
    private bool _tocarAoAbrir;
    // A fila chegou ao fim (ou nada foi carregado): arquivos soltos ou adicionados começam a tocar.
    private bool _ocioso = true;
    private int _falhasSeguidas;
    private bool _atualizandoPosicao;
    private bool _arrastandoPosicao;
    private bool _atualizandoVolume;

    private void ConfigurarReproducao(Configuracoes configuracoes)
    {
        _player = new MediaPlayer
        {
            AudioCategory = MediaPlayerAudioCategory.Media,
            AutoPlay = false,
            Volume = configuracoes.Volume / 100,
            IsMuted = configuracoes.Mudo
        };

        // Eventos do MediaPlayer chegam em threads de fundo.
        _player.MediaOpened += (_, _) => _filaInterface.TryEnqueue(Player_MediaOpened);
        _player.MediaEnded += (_, _) => _filaInterface.TryEnqueue(() => TocarProximo(automatico: true));
        _player.MediaFailed += (_, e) => _filaInterface.TryEnqueue(() => TratarFalhaReproducao(e.ErrorMessage));
        _player.PlaybackSession.PlaybackStateChanged += (_, _) => _filaInterface.TryEnqueue(AtualizarBotaoTocar);

        // Teclas de mídia do teclado e o painel de mídia do Windows (SMTC): Próximo/Anterior são da nossa fila.
        var comandos = _player.CommandManager;
        comandos.NextBehavior.EnablingRule = MediaCommandEnablingRule.Always;
        comandos.PreviousBehavior.EnablingRule = MediaCommandEnablingRule.Always;
        comandos.NextReceived += (_, e) =>
        {
            e.Handled = true;
            _filaInterface.TryEnqueue(() => TocarProximo(automatico: false));
        };
        comandos.PreviousReceived += (_, e) =>
        {
            e.Handled = true;
            _filaInterface.TryEnqueue(TocarAnterior);
        };

        _temporizadorPosicao = _filaInterface.CreateTimer();
        _temporizadorPosicao.Interval = TimeSpan.FromMilliseconds(IntervaloPosicaoMs);
        _temporizadorPosicao.IsRepeating = true;
        _temporizadorPosicao.Tick += (_, _) => AtualizarPosicao();
        _temporizadorPosicao.Start();

        // O Slider trata o clique e o arraste: só com handledEventsToo o início e o fim chegam aqui.
        sldPosicao.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, _) => _arrastandoPosicao = true), true);
        sldPosicao.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => ConcluirArrastePosicao()), true);
        sldPosicao.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler((_, _) => ConcluirArrastePosicao()), true);

        _atualizandoVolume = true;
        sldVolume.Value = configuracoes.Volume;
        _atualizandoVolume = false;

        AtualizarBotaoTocar();
        AtualizarBotaoRepetir();
        AtualizarBotaoVolume();
        ExibirTocandoAgora(null);
    }

    private async Task RestaurarFilaAsync()
    {
        var estado = EstadoFila.Carregar();
        var atual = estado.IndiceAtual >= 0 && estado.IndiceAtual < estado.Arquivos.Count ? estado.Arquivos[estado.IndiceAtual] : null;
        var existentes = await Task.Run(() => estado.Arquivos.Where(File.Exists).ToList());
        if (existentes.Count == 0)
            return;

        var itens = _fila.Adicionar(existentes);
        CarregarMetadados(itens);

        var itemAtual = atual is null ? null : itens.FirstOrDefault(i => string.Equals(i.Caminho, atual, StringComparison.OrdinalIgnoreCase));
        if (itemAtual is not null)
            await TocarAsync(itemAtual, iniciar: false, posicao: TimeSpan.FromSeconds(estado.PosicaoSegundos));
    }

    private async Task TocarAsync(ItemFila item, bool iniciar = true, TimeSpan? posicao = null)
    {
        var versao = ++_versaoCarga;
        _ocioso = false;
        _fila.DefinirAtual(item);
        lvwFila.ScrollIntoView(item);

        if (!item.MetadadosCarregados)
            item.AplicarMetadados(await LeitorMetadados.LerAsync(item.Caminho));
        if (versao != _versaoCarga)
            return;
        ExibirTocandoAgora(item);

        StorageFile arquivo;
        try
        {
            arquivo = await StorageFile.GetFileFromPathAsync(item.Caminho);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            if (versao == _versaoCarga)
                TratarFalhaReproducao(ex.Message);
            return;
        }

        var capa = await ObterCapaAsync(arquivo);
        if (versao != _versaoCarga)
        {
            capa?.Dispose();
            return;
        }

        var fonte = MediaSource.CreateFromStorageFile(arquivo);
        var itemReproducao = new MediaPlaybackItem(fonte);
        var exibicao = itemReproducao.GetDisplayProperties();
        exibicao.Type = MediaPlaybackType.Music;
        exibicao.MusicProperties.Title = item.Titulo;
        exibicao.MusicProperties.Artist = item.Artista;
        exibicao.MusicProperties.AlbumTitle = item.Album;
        if (capa is not null)
            exibicao.Thumbnail = RandomAccessStreamReference.CreateFromStream(capa);
        itemReproducao.ApplyDisplayProperties(exibicao);

        _posicaoPendente = posicao is { } p && p > TimeSpan.Zero ? p : null;
        _tocarAoAbrir = iniciar;
        _player.Source = itemReproducao;
        TrocarFonte(fonte, capa);
        await ExibirCapaAsync(capa);
    }

    private void Player_MediaOpened()
    {
        _falhasSeguidas = 0;
        sldPosicao.IsEnabled = true;
        if (_posicaoPendente is { } posicao && posicao < _player.PlaybackSession.NaturalDuration)
            _player.PlaybackSession.Position = posicao;
        _posicaoPendente = null;

        if (_tocarAoAbrir)
            _player.Play();
        AtualizarPosicao();
    }

    private void TocarProximo(bool automatico)
    {
        var proximo = _fila.ObterProximo(automatico);
        if (proximo is null)
        {
            if (automatico)
                ConcluirFila();
            return;
        }

        if (ReferenceEquals(proximo, _fila.Atual) && _player.Source is not null)
        {
            _player.PlaybackSession.Position = TimeSpan.Zero;
            _player.Play();
            return;
        }

        Disparar(TocarAsync(proximo));
    }

    private void TocarAnterior()
    {
        if (_player.Source is not null && _player.PlaybackSession.Position > LimiteReiniciarMusica)
        {
            _player.PlaybackSession.Position = TimeSpan.Zero;
            return;
        }

        var anterior = _fila.ObterAnterior();
        if (anterior is null || ReferenceEquals(anterior, _fila.Atual))
        {
            if (_player.Source is not null)
                _player.PlaybackSession.Position = TimeSpan.Zero;
            return;
        }

        Disparar(TocarAsync(anterior, iniciar: true));
    }

    private void AlternarTocarPausar()
    {
        if (_player.Source is null || _ocioso)
        {
            var item = _fila.Atual ?? _fila.Itens.FirstOrDefault();
            if (item is not null)
                Disparar(TocarAsync(item));
            return;
        }

        if (_player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
            _player.Pause();
        else
            _player.Play();
    }

    // Fim da fila sem repetição: para no início da última música, pronta para tocar de novo.
    private void ConcluirFila()
    {
        _player.Pause();
        _player.PlaybackSession.Position = TimeSpan.Zero;
        _ocioso = true;
        AtualizarPosicao();
    }

    private void PararEDescarregar()
    {
        ++_versaoCarga;
        _player.Pause();
        _player.Source = null;
        TrocarFonte(null, null);
        _ocioso = true;
        sldPosicao.IsEnabled = false;
        ExibirTocandoAgora(null);
        _ = ExibirCapaAsync(null);
        AtualizarPosicao();
        AtualizarBotaoTocar();
    }

    // Uma falha (formato não suportado, arquivo apagado...) avisa e passa para a próxima;
    // se todas falharem seguidas, para, para não ficar em laço com "Repetir todas".
    private void TratarFalhaReproducao(string detalhe)
    {
        Trace.TraceWarning($"Falha ao reproduzir {_fila.Atual?.Caminho}: {detalhe}");
        MostrarAviso(Textos.Formatar("ErroReproduzir", _fila.Atual?.NomeArquivo ?? string.Empty));
        _falhasSeguidas++;
        if (_falhasSeguidas >= _fila.Itens.Count)
        {
            _falhasSeguidas = 0;
            PararEDescarregar();
            return;
        }

        var proximo = _fila.ObterProximo(automatico: false);
        if (proximo is null)
            PararEDescarregar();
        else
            Disparar(TocarAsync(proximo, iniciar: _tocarAoAbrir));
    }

    private void TrocarFonte(MediaSource? fonte, StorageItemThumbnail? capa)
    {
        if (!ReferenceEquals(_fonteAtual, fonte))
            _fonteAtual?.Dispose();
        _fonteAtual = fonte;

        if (!ReferenceEquals(_capaAtual, capa))
            _capaAtual?.Dispose();
        _capaAtual = capa;
    }

    private static async Task<StorageItemThumbnail?> ObterCapaAsync(StorageFile arquivo)
    {
        try
        {
            var miniatura = await arquivo.GetThumbnailAsync(ThumbnailMode.MusicView, 512, ThumbnailOptions.UseCurrentScale);
            // Sem capa embutida o Windows devolve o ícone do tipo de arquivo: nesse caso fica o glifo padrão.
            if (miniatura is null || miniatura.Type != ThumbnailType.Image)
            {
                miniatura?.Dispose();
                return null;
            }
            return miniatura;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(ex.Message);
            return null;
        }
    }

    private async Task ExibirCapaAsync(StorageItemThumbnail? capa)
    {
        if (capa is null)
        {
            imgCapa.Source = null;
            imgCapa.Visibility = Visibility.Collapsed;
            fntCapaPadrao.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var imagem = new BitmapImage();
            using (var copia = capa.CloneStream())
                await imagem.SetSourceAsync(copia);
            // Outra música pode ter sido carregada durante a leitura da imagem.
            if (!ReferenceEquals(capa, _capaAtual))
                return;
            imgCapa.Source = imagem;
            imgCapa.Visibility = Visibility.Visible;
            fntCapaPadrao.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning(ex.Message);
            imgCapa.Visibility = Visibility.Collapsed;
            fntCapaPadrao.Visibility = Visibility.Visible;
        }
    }

    private void ExibirTocandoAgora(ItemFila? item)
    {
        txtTitulo.Text = item?.Titulo ?? Textos.Obter("NenhumaMusica");
        txtArtista.Text = item?.Artista ?? string.Empty;
        txtAlbum.Text = item?.Album ?? string.Empty;
        txtArtista.Visibility = string.IsNullOrEmpty(txtArtista.Text) ? Visibility.Collapsed : Visibility.Visible;
        txtAlbum.Visibility = string.IsNullOrEmpty(txtAlbum.Text) ? Visibility.Collapsed : Visibility.Visible;
        AtualizarTitulo();
    }

    private void AtualizarPosicao()
    {
        var sessao = _player.PlaybackSession;
        var duracao = _player.Source is null ? TimeSpan.Zero : sessao.NaturalDuration;
        var posicao = _player.Source is null ? TimeSpan.Zero : sessao.Position;
        txtDuracao.Text = FormatoTempo.Formatar(duracao);

        if (_arrastandoPosicao)
            return;

        _atualizandoPosicao = true;
        sldPosicao.Maximum = Math.Max(1, duracao.TotalSeconds);
        sldPosicao.Value = Math.Min(posicao.TotalSeconds, sldPosicao.Maximum);
        _atualizandoPosicao = false;
        txtDecorrido.Text = FormatoTempo.Formatar(posicao);
    }

    private void sldPosicao_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_atualizandoPosicao)
            return;

        // Durante o arraste só o tempo exibido acompanha; a busca acontece ao soltar.
        if (_arrastandoPosicao)
            txtDecorrido.Text = FormatoTempo.Formatar(TimeSpan.FromSeconds(e.NewValue));
        else
            BuscarPosicao(e.NewValue);
    }

    private void ConcluirArrastePosicao()
    {
        if (!_arrastandoPosicao)
            return;

        _arrastandoPosicao = false;
        BuscarPosicao(sldPosicao.Value);
    }

    private void BuscarPosicao(double segundos)
    {
        if (_player.Source is null || !_player.PlaybackSession.CanSeek)
            return;

        _player.PlaybackSession.Position = TimeSpan.FromSeconds(segundos);
        AtualizarPosicao();
    }

    private void btnTocarPausar_Click(object sender, RoutedEventArgs e) => AlternarTocarPausar();

    private void btnProximo_Click(object sender, RoutedEventArgs e) => TocarProximo(automatico: false);

    private void btnAnterior_Click(object sender, RoutedEventArgs e) => TocarAnterior();

    private void btnRepetir_Click(object sender, RoutedEventArgs e)
    {
        _fila.ModoRepeticao = _fila.ModoRepeticao switch
        {
            ModoRepeticao.Desligado => ModoRepeticao.Todas,
            ModoRepeticao.Todas => ModoRepeticao.Uma,
            _ => ModoRepeticao.Desligado
        };
        AtualizarBotaoRepetir();
    }

    private void btnMudo_Click(object sender, RoutedEventArgs e)
    {
        _player.IsMuted = !_player.IsMuted;
        AtualizarBotaoVolume();
    }

    private void sldVolume_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_atualizandoVolume || _player is null)
            return;

        _player.Volume = e.NewValue / 100;
        if (_player.IsMuted && e.NewValue > 0)
            _player.IsMuted = false;
        AtualizarBotaoVolume();
    }

    private void AtualizarBotaoTocar()
    {
        var estado = _player.PlaybackSession.PlaybackState;
        var tocando = estado is MediaPlaybackState.Playing or MediaPlaybackState.Buffering
            || (estado == MediaPlaybackState.Opening && _tocarAoAbrir);
        fntTocarPausar.Glyph = tocando ? GlifoPausar : GlifoTocar;
        DefinirDica(btnTocarPausar, Textos.Obter(tocando ? "PausarDica" : "TocarDica"));
    }

    private void AtualizarBotaoRepetir()
    {
        fntRepetir.Glyph = _fila.ModoRepeticao == ModoRepeticao.Uma ? GlifoRepetirUma : GlifoRepetirTodas;
        // Desligado fica esmaecido; ligado (todas ou uma) aparece normal.
        fntRepetir.Opacity = _fila.ModoRepeticao == ModoRepeticao.Desligado ? 0.45 : 1;
        DefinirDica(btnRepetir, Textos.Obter(_fila.ModoRepeticao switch
        {
            ModoRepeticao.Todas => "RepetirTodasDica",
            ModoRepeticao.Uma => "RepetirUmaDica",
            _ => "RepetirDesligadoDica"
        }));
    }

    private void AtualizarBotaoVolume()
    {
        var mudo = _player.IsMuted || sldVolume.Value <= 0;
        fntVolume.Glyph = mudo ? GlifoMudo : GlifoVolume;
        fntMudo.Glyph = _player.IsMuted ? GlifoVolume : GlifoMudo;
        DefinirDica(btnVolume, Textos.Formatar("VolumeDica", (int)Math.Round(sldVolume.Value)));
        DefinirDica(btnMudo, Textos.Obter(_player.IsMuted ? "ReativarSomDica" : "MudoDica"));
    }

    // Botões só com ícone: a dica também é o nome lido pelos leitores de tela.
    private static void DefinirDica(FrameworkElement elemento, string texto)
    {
        ToolTipService.SetToolTip(elemento, texto);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(elemento, texto);
    }

    private void EncerrarReproducao()
    {
        _temporizadorPosicao.Stop();
        _player.Pause();
        _player.Source = null;
        TrocarFonte(null, null);
        _player.Dispose();
    }
}
