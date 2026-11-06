using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using MusicPlayer.Nucleo;
using System.Diagnostics;

namespace MusicPlayer;

public partial class App : Application
{
    // Lido também pela thread de fundo do AppInstance.Activated (Application.Current não é seguro ali).
    private static App? _instancia;

    private readonly DispatcherQueue _filaInterface;
    private JanelaPrincipal? _janela;
    private readonly List<ArgumentosAtivacao> _ativacoesPendentes = [];

    public App()
    {
        // Idioma escolhido pelo usuário (ou o do Windows), antes de criar qualquer elemento do WinUI:
        // o PrimaryLanguageOverride vale também para os textos internos dos controles.
        var idioma = Configuracoes.Carregar().Idioma ?? Textos.IdiomaDoWindows();
        Textos.DefinirIdioma(idioma);
        DefinirIdiomaDoWinUI(Textos.IdiomaAtual);

        InitializeComponent();
        _filaInterface = DispatcherQueue.GetForCurrentThread();
        _instancia = this;

        UnhandledException += App_UnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Trace.TraceError($"Erro não tratado: {e.ExceptionObject}");
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Duplo clique no Explorer: o Windows passa o caminho do arquivo como argumento ("%1").
        var argumentos = ArgumentosAtivacao.Interpretar(Environment.GetCommandLineArgs().Skip(1));
        _janela = new JanelaPrincipal(argumentos);
        _janela.Activate();

        foreach (var pendente in _ativacoesPendentes)
            _janela.ReceberAtivacao(pendente);
        _ativacoesPendentes.Clear();
    }

    // Outra instância foi aberta (ex.: duplo clique em outro arquivo) e redirecionou a ativação para esta.
    public static void ReceberAtivacao(ArgumentosAtivacao argumentos)
    {
        var app = _instancia;
        if (app is null)
            return;

        app._filaInterface.TryEnqueue(() =>
        {
            if (app._janela is null)
                app._ativacoesPendentes.Add(argumentos);
            else
                app._janela.ReceberAtivacao(argumentos);
        });
    }

    // Só tem efeito completo na próxima abertura (os controles já criados mantêm os textos internos).
    public static void DefinirIdiomaDoWinUI(string idioma)
    {
        try
        {
            Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = idioma;
        }
        catch (Exception ex)
        {
            Trace.TraceError(ex.ToString());
        }
    }

    // Exceções de event handlers e de tarefas disparadas por JanelaPrincipal.Disparar chegam aqui.
    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Trace.TraceError(e.Exception.ToString());
        if (_janela is null)
            return;

        e.Handled = true;
        _janela.MostrarErroInesperado(e.Exception);
    }
}
