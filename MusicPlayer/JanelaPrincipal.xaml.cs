using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MusicPlayer.Nucleo;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using Windows.Graphics;
using Windows.System;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace MusicPlayer;

// O MediaPlayer e a capa são liberados no Closed; Window não é IDisposable.
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable")]
public sealed partial class JanelaPrincipal : Window
{
    private const string NomeAplicativo = "Music Player";
    private const int LarguraInicial = 1000;
    private const int AlturaInicial = 640;
    private const int LarguraMinima = 420;
    private const int AlturaMinima = 480;
    // Abaixo desta largura do corpo, "Tocando agora" fica em cima da fila (AplicarLayout).
    private const double LarguraLayoutEmpilhado = 720;
    private const double LarguraColunaTocando = 320;
    private const double EspacoEntrePaineis = 8;

    private readonly FilaReproducao _fila = new();
    // Guardada no construtor: a propriedade DispatcherQueue da janela só pode ser lida na thread da interface.
    private readonly DispatcherQueue _filaInterface;
    private readonly ArgumentosAtivacao _argumentosIniciais;
    private bool? _layoutEmpilhado;
    private bool _painelTocandoVisivel = true;

    public JanelaPrincipal(ArgumentosAtivacao argumentosIniciais)
    {
        InitializeComponent();
        _argumentosIniciais = argumentosIniciais;
        _filaInterface = DispatcherQueue;

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(tlbTitulo);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "MusicPlayer.ico"));
        Closed += JanelaPrincipal_Closed;

        var configuracoes = Configuracoes.Carregar();
        _idiomaEscolhido = configuracoes.Idioma is { } idioma && Textos.IdiomasSuportados.Contains(idioma) ? idioma : null;
        _fila.ModoRepeticao = configuracoes.ModoRepeticao;
        _painelTocandoVisivel = configuracoes.PainelTocandoVisivel;
        AplicarPainelTocando();

        lvwFila.ItemsSource = _fila.Itens;
        _fila.Itens.CollectionChanged += Itens_CollectionChanged;

        ConfigurarReproducao(configuracoes);
        ConfigurarAtivacao();
        ConfigurarMenuFila();
        ConfigurarArrastarSoltar();
        // Espaço tocar/pausar em qualquer lugar da janela (antes de botões e da lista tratarem a tecla).
        grdRaiz.AddHandler(UIElement.PreviewKeyDownEvent, new KeyEventHandler(grdRaiz_PreviewKeyDown), true);
        grdCorpo.SizeChanged += (_, e) => AplicarLayout(e.NewSize.Width);

        AplicarIdioma();
        grdRaiz.Loaded += grdRaiz_Loaded;
    }

    private void grdRaiz_Loaded(object sender, RoutedEventArgs e)
    {
        grdRaiz.Loaded -= grdRaiz_Loaded;
        AjustarTamanhoJanela();
        Disparar(IniciarAsync());
    }

    // A fila da última sessão volta sempre (pausada); arquivos recebidos na abertura entram depois, como num duplo clique.
    private async Task IniciarAsync()
    {
        await RestaurarFilaAsync();
        if (_argumentosIniciais.Caminhos.Count > 0)
            ReceberAtivacao(_argumentosIniciais);
    }

    // O AppWindow trabalha em pixels físicos; os tamanhos são definidos em pixels lógicos (100%).
    private void AjustarTamanhoJanela()
    {
        var escala = grdRaiz.XamlRoot?.RasterizationScale ?? 1.0;
        AppWindow.Resize(new SizeInt32((int)(LarguraInicial * escala), (int)(AlturaInicial * escala)));
        if (AppWindow.Presenter is OverlappedPresenter apresentador)
        {
            apresentador.PreferredMinimumWidth = (int)(LarguraMinima * escala);
            apresentador.PreferredMinimumHeight = (int)(AlturaMinima * escala);
        }
    }

    private void AplicarLayout(double largura)
    {
        var empilhado = largura < LarguraLayoutEmpilhado;
        if (_layoutEmpilhado == empilhado)
            return;

        _layoutEmpilhado = empilhado;
        if (empilhado)
        {
            colTocando.Width = new GridLength(1, GridUnitType.Star);
            colFila.Width = new GridLength(0);
            rowSuperior.Height = GridLength.Auto;
            rowInferior.Height = new GridLength(1, GridUnitType.Star);
            Grid.SetColumn(brdFila, 0);
            Grid.SetRow(brdFila, 1);
            brdTocando.Padding = new Thickness(16);
            pnlTocando.Orientation = Microsoft.UI.Xaml.Controls.Orientation.Horizontal;
            pnlTextosTocando.VerticalAlignment = VerticalAlignment.Center;
            grdCapa.Width = grdCapa.Height = 96;
            fntCapaPadrao.FontSize = 36;
            AlinharTextosTocando(HorizontalAlignment.Left, TextAlignment.Left);
        }
        else
        {
            colTocando.Width = new GridLength(_painelTocandoVisivel ? LarguraColunaTocando : 0);
            colFila.Width = new GridLength(1, GridUnitType.Star);
            rowSuperior.Height = new GridLength(1, GridUnitType.Star);
            rowInferior.Height = new GridLength(0);
            Grid.SetColumn(brdFila, 1);
            Grid.SetRow(brdFila, 0);
            brdTocando.Padding = new Thickness(24);
            pnlTocando.Orientation = Microsoft.UI.Xaml.Controls.Orientation.Vertical;
            pnlTextosTocando.VerticalAlignment = VerticalAlignment.Top;
            grdCapa.Width = grdCapa.Height = 240;
            fntCapaPadrao.FontSize = 72;
            AlinharTextosTocando(HorizontalAlignment.Center, TextAlignment.Center);
        }
    }

    // Oculto: o painel some e a fila ocupa todo o corpo, nos dois layouts (lado a lado ou empilhado).
    private void AplicarPainelTocando()
    {
        abbPainelTocando.IsChecked = _painelTocandoVisivel;
        brdTocando.Visibility = _painelTocandoVisivel ? Visibility.Visible : Visibility.Collapsed;
        grdCorpo.ColumnSpacing = grdCorpo.RowSpacing = _painelTocandoVisivel ? EspacoEntrePaineis : 0;
        if (_layoutEmpilhado != true)
            colTocando.Width = new GridLength(_painelTocandoVisivel ? LarguraColunaTocando : 0);
    }

    private void abbPainelTocando_Click(object sender, RoutedEventArgs e)
    {
        _painelTocandoVisivel = abbPainelTocando.IsChecked == true;
        AplicarPainelTocando();
        AtualizarDicaPainelTocando();
    }

    private void AtualizarDicaPainelTocando() =>
        ToolTipService.SetToolTip(abbPainelTocando, Textos.Obter(_painelTocandoVisivel ? "OcultarPainelDica" : "MostrarPainelDica"));

    private void AlinharTextosTocando(HorizontalAlignment alinhamento, TextAlignment texto)
    {
        foreach (var bloco in new[] { txtTitulo, txtArtista, txtAlbum })
        {
            bloco.HorizontalAlignment = alinhamento;
            bloco.HorizontalTextAlignment = texto;
        }
    }

    private void grdRaiz_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Space || e.Handled || !EspacoEhDoPlayer(e.OriginalSource as DependencyObject))
            return;

        e.Handled = true;
        AlternarTocarPausar();
    }

    // Diálogos e menus ficam numa camada de popups fora do grdRaiz, mas a tecla também chega aqui:
    // só vale tocar/pausar quando o foco está na própria janela e fora de um campo de texto.
    private bool EspacoEhDoPlayer(DependencyObject? origem)
    {
        if (origem is null)
            return true;

        for (var elemento = origem; elemento is not null; elemento = VisualTreeHelper.GetParent(elemento))
        {
            if (elemento is TextBox or PasswordBox or RichEditBox or AutoSuggestBox or NumberBox)
                return false;
            if (ReferenceEquals(elemento, grdRaiz))
                return true;
        }
        return false;
    }

    private void Itens_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        AtualizarResumoFila();
        abbLimparFila.IsEnabled = _fila.Itens.Count > 0;
        abbEmbaralhar.IsEnabled = _fila.Itens.Count > 1;
        pnlFilaVazia.Visibility = _fila.Itens.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AtualizarResumoFila()
    {
        var quantidade = _fila.Itens.Count;
        txtResumoFila.Text = quantidade switch
        {
            0 => string.Empty,
            1 => Textos.Formatar("ResumoFilaUma", FormatoTempo.Formatar(_fila.DuracaoTotal)),
            _ => Textos.Formatar("ResumoFila", quantidade, FormatoTempo.Formatar(_fila.DuracaoTotal))
        };
    }

    private void AtualizarTitulo()
    {
        var titulo = _fila.Atual is { } atual ? $"{atual.Titulo} — {NomeAplicativo}" : NomeAplicativo;
        Title = titulo;
        tlbTitulo.Title = titulo;
    }

    private void MostrarAviso(string mensagem, Microsoft.UI.Xaml.Controls.InfoBarSeverity severidade =
        Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning)
    {
        ifbAviso.Severity = severidade;
        ifbAviso.Message = mensagem;
        ifbAviso.IsOpen = true;
    }

    private void JanelaPrincipal_Closed(object sender, WindowEventArgs args)
    {
        SalvarEstado();
        EncerrarReproducao();
        _temporizadorAtivacao.Stop();
    }

    private void SalvarEstado()
    {
        var configuracoes = Configuracoes.Carregar();
        configuracoes.Volume = sldVolume.Value;
        configuracoes.Mudo = _player.IsMuted;
        configuracoes.ModoRepeticao = _fila.ModoRepeticao;
        configuracoes.PainelTocandoVisivel = _painelTocandoVisivel;
        configuracoes.Salvar();

        EstadoFila.Capturar(_fila, _player.PlaybackSession.Position).Salvar();
    }

    // Ponto único de "dispara e esquece": uma exceção da tarefa chega ao Application.UnhandledException
    // (tratado em App.xaml.cs) em vez de ser perdida.
    private static async void Disparar(Task tarefa) => await tarefa;
}
