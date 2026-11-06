using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;

namespace MusicPlayer;

public sealed partial class JanelaPrincipal
{
    // O WinUI só permite um ContentDialog aberto por vez na janela: os pedidos esperam a vez.
    private readonly SemaphoreSlim _filaDialogos = new(1, 1);

    private async Task AvisarAsync(string mensagem)
    {
        await _filaDialogos.WaitAsync();
        try
        {
            var dialogo = new ContentDialog
            {
                XamlRoot = grdRaiz.XamlRoot,
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                RequestedTheme = grdRaiz.ActualTheme,
                Title = NomeAplicativo,
                Content = new TextBlock
                {
                    Text = mensagem,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true
                },
                CloseButtonText = Textos.Obter("Ok"),
                DefaultButton = ContentDialogButton.Close
            };
            await dialogo.ShowAsync();
        }
        finally
        {
            _filaDialogos.Release();
        }
    }

    public void MostrarErroInesperado(Exception ex) => Disparar(MostrarErroInesperadoAsync(ex));

    private async Task MostrarErroInesperadoAsync(Exception ex)
    {
        try
        {
            await AvisarAsync(Textos.Formatar("ErroInesperado", ex.Message));
        }
        catch (Exception erroDialogo)
        {
            // Sem janela para mostrar o erro (ex.: fechando): fica só no Trace, para não entrar em laço.
            Trace.TraceError(erroDialogo.ToString());
        }
    }
}
