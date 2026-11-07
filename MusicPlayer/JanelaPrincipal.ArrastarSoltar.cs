using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace MusicPlayer;

public sealed partial class JanelaPrincipal
{
    // Registrado com handledEventsToo: o ListView marca o arraste como tratado (é ele que reordena a fila),
    // mas arquivos vindos do Explorer devem ser aceitos em qualquer ponto da janela, inclusive sobre a lista.
    // O arraste interno da reordenação não traz StorageItems e por isso é ignorado aqui.
    private void ConfigurarArrastarSoltar()
    {
        grdRaiz.AllowDrop = true;
        grdRaiz.AddHandler(UIElement.DragOverEvent, new DragEventHandler(grdRaiz_DragOver), true);
        grdRaiz.AddHandler(UIElement.DropEvent, new DragEventHandler(grdRaiz_Drop), true);
    }

    private void grdRaiz_DragOver(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            return;

        e.AcceptedOperation = DataPackageOperation.Copy;
        if (e.DragUIOverride is not null)
        {
            e.DragUIOverride.Caption = Textos.Obter("ArrastarAdicionar");
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
        }
    }

    // O adiamento é pedido antes do primeiro await, ainda dentro do evento.
    private void grdRaiz_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
            Disparar(SoltarAsync(e));
    }

    private async Task SoltarAsync(DragEventArgs e)
    {
        // A lista de itens só pode ser lida durante o evento.
        IReadOnlyList<IStorageItem> itens;
        var adiamento = e.GetDeferral();
        try
        {
            itens = await e.DataView.GetStorageItemsAsync();
        }
        finally
        {
            adiamento.Complete();
        }

        var caminhos = itens.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
        if (caminhos.Count > 0)
            await AdicionarAoFimAsync(caminhos);
    }
}
