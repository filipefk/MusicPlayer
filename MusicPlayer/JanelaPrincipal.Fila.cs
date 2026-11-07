using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Storage.Pickers;
using MusicPlayer.Nucleo;
using System.Diagnostics;
using Windows.System;

namespace MusicPlayer;

public sealed partial class JanelaPrincipal
{
    private readonly MenuFlyout _mfoFila = new();
    private readonly MenuFlyoutItem _mfiTocar = new() { Icon = new FontIcon { Glyph = "" } };
    private readonly MenuFlyoutItem _mfiRemover = new() { Icon = new FontIcon { Glyph = "" } };
    private readonly MenuFlyoutItem _mfiAbrirLocal = new() { Icon = new FontIcon { Glyph = "" } };
    private readonly MenuFlyoutItem _mfiEditarMetadados = new() { Icon = new FontIcon { Glyph = "" } };

    private void ConfigurarMenuFila()
    {
        _mfiTocar.Click += (_, _) => TocarSelecionado();
        _mfiRemover.Click += (_, _) => RemoverSelecionados();
        _mfiAbrirLocal.Click += (_, _) => AbrirLocalSelecionado();
        _mfiEditarMetadados.Click += (_, _) => EditarMetadadosSelecionado();
        _mfoFila.Items.Add(_mfiTocar);
        _mfoFila.Items.Add(new MenuFlyoutSeparator());
        _mfoFila.Items.Add(_mfiEditarMetadados);
        _mfoFila.Items.Add(_mfiAbrirLocal);
        _mfoFila.Items.Add(_mfiRemover);
    }

    // Itens adicionados pelo usuário (botões ou arrastar): sempre no fim da fila.
    private async Task AdicionarAoFimAsync(IEnumerable<string> caminhos)
    {
        var (arquivos, pastas) = await ExpandirAsync(caminhos);
        if (arquivos.Count == 0)
        {
            MostrarAviso(Textos.Obter("NenhumArquivoSuportado"));
            return;
        }

        var novos = _fila.Adicionar(arquivos);
        CarregarMetadados(novos, pastas);
        if (_ocioso)
            await TocarAsync(novos[0]);
    }

    // As pastas voltam à parte para a leitura das tags em lote (LerPastaAsync).
    private static Task<(IReadOnlyList<string> Arquivos, List<string> Pastas)> ExpandirAsync(IEnumerable<string> caminhos)
    {
        var lista = caminhos.ToList();
        return Task.Run(() => (ExpansorCaminhos.Expandir(lista), lista.Where(Directory.Exists).ToList()));
    }

    private async void abbAdicionarArquivos_Click(object sender, RoutedEventArgs e)
    {
        var seletor = new FileOpenPicker(AppWindow.Id)
        {
            SuggestedStartLocation = PickerLocationId.MusicLibrary,
            ViewMode = PickerViewMode.List
        };
        foreach (var extensao in FormatosAudio.Extensoes)
            seletor.FileTypeFilter.Add(extensao);

        var escolhidos = await seletor.PickMultipleFilesAsync();
        if (escolhidos is { Count: > 0 })
            await AdicionarAoFimAsync(escolhidos.Select(r => r.Path));
    }

    private async void abbAdicionarPasta_Click(object sender, RoutedEventArgs e)
    {
        var seletor = new FolderPicker(AppWindow.Id) { SuggestedStartLocation = PickerLocationId.MusicLibrary };
        var pasta = await seletor.PickSingleFolderAsync();
        if (pasta is not null)
            await AdicionarAoFimAsync([pasta.Path]);
    }

    private void abbEmbaralhar_Click(object sender, RoutedEventArgs e)
    {
        _fila.Embaralhar();
        // Com filtro, a lista visível só é reordenada no próximo AplicarFiltro: aplica já para rolar ao topo certo.
        if (FiltroAtivo)
            AplicarFiltro();
        var visiveis = FiltroAtivo ? _itensFiltrados : _fila.Itens;
        if (visiveis.Count > 0)
            lvwFila.ScrollIntoView(visiveis[0]);
    }

    private async void abbLimparFila_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmarAsync(Textos.Obter("ConfirmarLimparFila"), Textos.Obter("LimparFila")))
            return;

        PararEDescarregar();
        CancelarCarga();
        _fila.Limpar();
    }

    private void lvwFila_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ItemDoElemento(e.OriginalSource as DependencyObject) is { } item)
            Disparar(TocarAsync(item));
    }

    private void lvwFila_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Enter:
                TocarSelecionado();
                e.Handled = true;
                break;
            case VirtualKey.Delete:
                RemoverSelecionados();
                e.Handled = true;
                break;
        }
    }

    private void lvwFila_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var quantidade = lvwFila.SelectedItems.Count;
        _mfiTocar.IsEnabled = quantidade == 1;
        _mfiAbrirLocal.IsEnabled = quantidade == 1;
        _mfiEditarMetadados.IsEnabled = quantidade == 1 && lvwFila.SelectedItems[0] is ItemFila item && EditorMetadados.PodeEditar(item.Caminho);
    }

    // Clique direito num item não selecionado passa a selecionar só ele (como no Explorer).
    private void lvwFila_ContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        var item = ItemDoElemento(e.OriginalSource as DependencyObject);
        if (item is not null && !lvwFila.SelectedItems.Contains(item))
        {
            lvwFila.SelectedItems.Clear();
            lvwFila.SelectedItems.Add(item);
        }

        if (lvwFila.SelectedItems.Count == 0)
            return;

        _mfiTocar.Text = Textos.Obter("MenuTocar");
        _mfiRemover.Text = Textos.Obter(lvwFila.SelectedItems.Count == 1 ? "MenuRemover" : "MenuRemoverSelecionadas");
        _mfiAbrirLocal.Text = Textos.Obter("MenuAbrirLocal");
        _mfiEditarMetadados.Text = Textos.Obter("MenuEditarMetadados");

        var opcoes = new FlyoutShowOptions();
        if (e.TryGetPosition(lvwFila, out var ponto))
            opcoes.Position = ponto;
        _mfoFila.ShowAt(lvwFila, opcoes);
        e.Handled = true;
    }

    private ItemFila? ItemDoElemento(DependencyObject? elemento)
    {
        while (elemento is not null and not ListViewItem)
            elemento = VisualTreeHelper.GetParent(elemento);

        return elemento is ListViewItem container ? lvwFila.ItemFromContainer(container) as ItemFila : null;
    }

    private void TocarSelecionado()
    {
        if (lvwFila.SelectedItems.Count == 1 && lvwFila.SelectedItems[0] is ItemFila item)
            Disparar(TocarAsync(item));
    }

    private void RemoverSelecionados()
    {
        var itens = lvwFila.SelectedItems.OfType<ItemFila>().ToList();
        if (itens.Count == 0)
            return;

        var tocava = !_ocioso && _player.PlaybackSession.PlaybackState == Windows.Media.Playback.MediaPlaybackState.Playing;
        var resultado = _fila.Remover(itens);
        if (!resultado.AtualRemovido)
            return;

        if (resultado.Seguinte is { } seguinte)
            Disparar(TocarAsync(seguinte, iniciar: tocava));
        else
            PararEDescarregar();
    }

    private void AbrirLocalSelecionado()
    {
        if (lvwFila.SelectedItems.Count != 1 || lvwFila.SelectedItems[0] is not ItemFila item)
            return;

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.Caminho}\"") { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            MostrarAviso(ex.Message);
        }
    }
}
