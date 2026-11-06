using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using MusicPlayer.Nucleo;
using System.Diagnostics;
using Windows.ApplicationModel.Activation;

namespace MusicPlayer;

// Instância única (padrão do Windows App SDK para apps sem pacote): a primeira instância registra a chave
// e recebe as ativações seguintes; as demais redirecionam a ativação (com os arquivos) para ela e fecham.
public static class Program
{
    private const string ChaveInstancia = "MusicPlayer.InstanciaPrincipal";

    [STAThread]
    private static int Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (RedirecionarParaInstanciaPrincipal())
            return 0;

        Application.Start(parametros =>
        {
            var contexto = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(contexto);
            _ = new App();
        });
        return 0;
    }

    private static bool RedirecionarParaInstanciaPrincipal()
    {
        var ativacao = AppInstance.GetCurrent().GetActivatedEventArgs();
        var principal = AppInstance.FindOrRegisterForKey(ChaveInstancia);
        if (principal.IsCurrent)
        {
            principal.Activated += InstanciaPrincipal_Activated;
            return false;
        }

        Redirecionar(ativacao, principal);
        return true;
    }

    // O RedirectActivationToAsync precisa ser aguardado sem bloquear a thread STA (CoWaitForMultipleObjects).
    private static void Redirecionar(AppActivationArguments ativacao, AppInstance principal)
    {
        var evento = NativeMethods.CreateEvent(0, true, false, null);
        try
        {
            _ = Task.Run(() =>
            {
                try
                {
                    principal.RedirectActivationToAsync(ativacao).AsTask().Wait();
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.ToString());
                }
                finally
                {
                    NativeMethods.SetEvent(evento);
                }
            });
            _ = NativeMethods.CoWaitForMultipleObjects(NativeMethods.CWMO_DEFAULT, NativeMethods.INFINITE, 1, [evento], out _);
        }
        finally
        {
            NativeMethods.CloseHandle(evento);
        }

        // Este processo acabou de ser iniciado pelo usuário e por isso pode passar o foco para a instância principal.
        try
        {
            using var processo = Process.GetProcessById((int)principal.ProcessId);
            NativeMethods.SetForegroundWindow(processo.MainWindowHandle);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Trace.TraceError(ex.ToString());
        }
    }

    // Chega numa thread de fundo; o App repassa para a thread da interface.
    private static void InstanciaPrincipal_Activated(object? sender, AppActivationArguments e)
    {
        if (e.Kind != ExtendedActivationKind.Launch || e.Data is not ILaunchActivatedEventArgs lancamento)
            return;

        App.ReceberAtivacao(ArgumentosAtivacao.Interpretar(LinhaComando.Separar(lancamento.Arguments)));
    }
}
