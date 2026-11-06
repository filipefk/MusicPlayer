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
    // Leitura das tags em paralelo, sem abrir centenas de arquivos ao mesmo tempo.
    private const int LeiturasSimultaneasMetadados = 4;

    private readonly SemaphoreSlim _limiteMetadados = new(LeiturasSimultaneasMetadados);
    private readonly MenuFlyout _mfoFila = new();
    private readonly MenuFlyoutItem _mfiTocar = new() { Icon = new FontIcon { Glyph = "" } };
    private readonly MenuFlyoutItem _mfiRemover = new() { Icon = new FontIcon { Glyph = "" } };
    private readonly MenuFlyoutItem _mfiAbrirLocal = new() { Icon = new FontIcon { Glyph = "" } };

    private void ConfigurarMenuFila()
    {
        _mfiTocar.Click += (_, _) => TocarSelecionado();
        _mfiRemover.Click += (_, _) => RemoverSelecionados();
        _mfiAbrirLocal.Click += (_, _) => AbrirLocalSelecionado();
        _mfoFila.Items.Add(_mfiTocar);
        _mfoFila.Items.Add(new MenuFlyoutSeparator());
        _mfoFila.Items.Add(_mfiAbrirLocal);
        _mfoFila.Items.Add(_mfiRemover);
    }

    // Itens adicionados pelo usuário (botões ou arrastar): sempre no fim da fila.
    private async Task AdicionarAoFimAsync(IEnumerable<string> caminhos)
    {
        var lista = caminhos.ToList();
        var arquivos = await Task.Run(() => ExpansorCaminhos.Expandir(lista));
        if (arquivos.Count == 0)
        {
            MostrarAviso(Textos.Obter("NenhumArquivoSuportado"));
            return;
        }

        var novos = _fila.Adicionar(arquivos);
        CarregarMetadados(novos);
        if (_ocioso)
            await TocarAsync(novos[0]);
    }

    private void CarregarMetadados(IEnumerable<ItemFila> itens)
    {
        foreach (var item in itens)
            Disparar(CarregarMetadadosAsync(item));
    }

    private async Task CarregarMetadadosAsync(ItemFila item)
    {
        await _limiteMetadados.WaitAsync();
        try
        {
            if (item.MetadadosCarregados)
                return;

            item.AplicarMetadados(await LeitorMetadados.LerAsync(item.Caminho));
        }
        finally
        {
            _limiteMetadados.Release();
        }

        AtualizarResumoFila();
        if (ReferenceEquals(item, _fila.Atual))
            ExibirTocandoAgora(item);
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
        if (_fila.Itens.Count > 0)
            lvwFila.ScrollIntoView(_fila.Itens[0]);
    }

    private async void abbLimparFila_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmarAsync(Textos.Obter("ConfirmarLimparFila"), Textos.Obter("LimparFila")))
            return;

        PararEDescarregar();
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
