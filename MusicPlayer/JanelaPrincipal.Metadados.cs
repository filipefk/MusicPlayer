using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MusicPlayer.Nucleo;
using Windows.Globalization.NumberFormatting;
using Windows.Media.Playback;

namespace MusicPlayer;

public sealed partial class JanelaPrincipal
{
    private const int LarguraDialogoMetadados = 420;
    private const double FaixaMaxima = 999;
    private const double AnoMaximo = 9999;

    private bool _preenchendo;
    private int _totalPreenchimento;
    private int _concluidosPreenchimento;

    private void EditarMetadadosSelecionado()
    {
        if (lvwFila.SelectedItems.Count == 1 && lvwFila.SelectedItems[0] is ItemFila item && EditorMetadados.PodeEditar(item.Caminho))
            Disparar(EditarMetadadosAsync(item));
    }

    private async Task EditarMetadadosAsync(ItemFila item)
    {
        var tags = await EditorMetadados.LerAsync(item.Caminho);
        if (tags is null)
        {
            MostrarAviso(Textos.Formatar("ErroLerMetadados", item.NomeArquivo));
            return;
        }

        var editadas = await PedirTagsAsync(item, tags);
        if (editadas is null || editadas == tags)
            return;

        var erro = await GravarTagsAsync(item, editadas);
        if (erro is not null)
            MostrarAviso(Textos.Formatar("ErroSalvarMetadados", item.NomeArquivo, erro));
    }

    private async Task<TagsMusica?> PedirTagsAsync(ItemFila item, TagsMusica tags)
    {
        var sugeridas = EditorMetadados.SugerirPeloNome(item.Caminho, tags);
        var txbTitulo = CriarCampoTexto("CampoTitulo", sugeridas.Titulo);
        var txbArtista = CriarCampoTexto("CampoArtista", sugeridas.Artista);
        var txbAlbum = CriarCampoTexto("CampoAlbum", tags.Album);
        var nmbFaixa = CriarCampoNumero("CampoFaixa", tags.Faixa, FaixaMaxima);
        var nmbAno = CriarCampoNumero("CampoAno", tags.Ano, AnoMaximo);
        var txbGenero = CriarCampoTexto("CampoGenero", tags.Genero);
        txbGenero.PlaceholderText = Textos.Obter("GeneroExemplo");

        // Faixa e Ano lado a lado, como no Explorer.
        var grdNumeros = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }
        };
        Grid.SetColumn(nmbAno, 1);
        grdNumeros.Children.Add(nmbFaixa);
        grdNumeros.Children.Add(nmbAno);

        var pnlCampos = new StackPanel { Spacing = 12, Width = LarguraDialogoMetadados };
        // Selecionável (Ctrl+C ou menu de contexto) para copiar partes do nome para os campos.
        pnlCampos.Children.Add(new TextBlock
        {
            Text = item.NomeArquivo,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap
        });
        pnlCampos.Children.Add(txbTitulo);
        pnlCampos.Children.Add(txbArtista);
        pnlCampos.Children.Add(txbAlbum);
        pnlCampos.Children.Add(grdNumeros);
        pnlCampos.Children.Add(txbGenero);

        await _filaDialogos.WaitAsync();
        try
        {
            var dialogo = new ContentDialog
            {
                XamlRoot = grdRaiz.XamlRoot,
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                RequestedTheme = grdRaiz.ActualTheme,
                Title = Textos.Obter("EditarMetadadosTitulo"),
                Content = pnlCampos,
                PrimaryButtonText = Textos.Obter("Salvar"),
                CloseButtonText = Textos.Obter("Cancelar"),
                DefaultButton = ContentDialogButton.Primary
            };
            dialogo.Opened += (_, _) => txbTitulo.Focus(FocusState.Programmatic);
            if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
                return null;
        }
        finally
        {
            _filaDialogos.Release();
        }

        return new TagsMusica(
            txbTitulo.Text.Trim(),
            txbArtista.Text.Trim(),
            txbAlbum.Text.Trim(),
            ValorNumero(nmbFaixa),
            ValorNumero(nmbAno),
            txbGenero.Text.Trim());
    }

    private static TextBox CriarCampoTexto(string chaveRotulo, string valor) => new()
    {
        Header = Textos.Obter(chaveRotulo),
        Text = valor
    };

    private static NumberBox CriarCampoNumero(string chaveRotulo, uint valor, double maximo) => new()
    {
        Header = Textos.Obter(chaveRotulo),
        Minimum = 0,
        Maximum = maximo,
        // Vazio (NaN) = sem valor na tag.
        Value = valor > 0 ? valor : double.NaN,
        SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden,
        ValidationMode = NumberBoxValidationMode.InvalidInputOverwritten,
        NumberFormatter = new DecimalFormatter
        {
            FractionDigits = 0,
            IntegerDigits = 1,
            IsGrouped = false,
            NumberRounder = new IncrementNumberRounder { Increment = 1 }
        }
    };

    private static uint ValorNumero(NumberBox campo) =>
        double.IsNaN(campo.Value) || campo.Value < 1 ? 0 : (uint)Math.Round(campo.Value);

    private void abbPreencherPeloNome_Click(object sender, RoutedEventArgs e) => Disparar(PreencherPeloNomeAsync());

    // Aplica a sugestão do diálogo de edição a toda a fila: só arquivos editáveis sem título e sem artista
    // cujo nome segue o padrão. Um arquivo por vez, com a lista, a pesquisa e o embaralhar desabilitados.
    private async Task PreencherPeloNomeAsync()
    {
        if (Ocupado)
            return;

        var candidatos = _fila.Itens.Where(PodePreencherPeloNome).ToList();
        if (candidatos.Count == 0)
        {
            await AvisarAsync(Textos.Obter("PreencherPeloNomeNenhum"));
            return;
        }

        if (!await ConfirmarAsync(Textos.Formatar("ConfirmarPreencherPeloNome", candidatos.Count), Textos.Obter("Atualizar"))
            || Ocupado)
            return;

        var geracao = _geracaoCarga;
        var atualizados = 0;
        var falhas = 0;
        _preenchendo = true;
        _totalPreenchimento = candidatos.Count;
        _concluidosPreenchimento = 0;
        AtualizarEstadoCarga();
        AtualizarResumoFila();
        try
        {
            foreach (var item in candidatos)
            {
                // Com a lista desabilitada, só "Limpar fila" tira músicas dela: o resto não é mais da fila.
                if (geracao != _geracaoCarga)
                    break;

                // As tags carregadas não distinguem título vazio de título igual ao nome do arquivo.
                var tags = await EditorMetadados.LerAsync(item.Caminho);
                if (tags is null)
                {
                    falhas++;
                }
                else
                {
                    var sugeridas = EditorMetadados.SugerirPeloNome(item.Caminho, tags);
                    if (sugeridas != tags)
                    {
                        if (await GravarTagsAsync(item, sugeridas) is null)
                            atualizados++;
                        else
                            falhas++;
                    }
                }

                _concluidosPreenchimento++;
                AgendarAtualizacaoFila();
            }
        }
        finally
        {
            _preenchendo = false;
            _totalPreenchimento = _concluidosPreenchimento = 0;
            AtualizarEstadoCarga();
            AtualizarResumoFila();
        }

        if (falhas > 0)
            MostrarAviso(Textos.Formatar("PreencherPeloNomeResultadoErros", atualizados, falhas));
        else
            MostrarAviso(Textos.Formatar("PreencherPeloNomeResultado", atualizados), InfoBarSeverity.Success);
    }

    // Triagem pelas tags já carregadas: sem título o ItemFila mostra o nome do arquivo sem a extensão.
    private static bool PodePreencherPeloNome(ItemFila item)
    {
        if (!EditorMetadados.PodeEditar(item.Caminho) || item.Artista.Length > 0
            || item.Titulo != Path.GetFileNameWithoutExtension(item.Caminho))
            return false;

        var vazias = new TagsMusica(string.Empty, string.Empty, string.Empty, 0, 0, string.Empty);
        return EditorMetadados.SugerirPeloNome(item.Caminho, vazias) != vazias;
    }

    // A música carregada no player fica com o arquivo aberto: é solta antes de gravar
    // e recarregada depois, na mesma posição e no mesmo estado (tocando ou pausada).
    private async Task<string?> GravarTagsAsync(ItemFila item, TagsMusica tags)
    {
        var recarregar = ReferenceEquals(item, _fila.Atual) && _player.Source is not null;
        var posicao = _player.PlaybackSession.Position;
        var tocava = _player.PlaybackSession.PlaybackState is MediaPlaybackState.Playing or MediaPlaybackState.Buffering;
        var ocioso = _ocioso;
        var versao = _versaoCarga;
        if (recarregar)
        {
            versao = ++_versaoCarga;
            _player.Pause();
            _player.Source = null;
            TrocarFonte(null, null);
        }

        var erro = await EditorMetadados.SalvarAsync(item.Caminho, tags);
        item.AplicarMetadados(await LeitorMetadados.LerAsync(item.Caminho));
        MarcarLidoForaDaCarga(item);
        AgendarAtualizacaoFila();

        // Outra música pedida durante a gravação (teclas de mídia, duplo clique...): fica a escolha do usuário.
        if (recarregar && versao == _versaoCarga)
        {
            await TocarAsync(item, iniciar: tocava, posicao: posicao);
            // Fim da fila continua sendo fim da fila: arquivos adicionados depois ainda começam a tocar.
            _ocioso = ocioso;
        }
        else if (ReferenceEquals(item, _fila.Atual))
        {
            ExibirTocandoAgora(item);
        }

        return erro;
    }
}
