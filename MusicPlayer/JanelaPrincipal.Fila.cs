using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Storage.Pickers;
using MusicPlayer.Nucleo;
using System.Diagnostics;
using System.Numerics;
using Windows.System;
using Windows.UI.ViewManagement;

namespace MusicPlayer;

public sealed partial class JanelaPrincipal
{
    private const string GlifoOrdenar = "";
    // Duas setas em círculo (Sync), girando enquanto ordena ou embaralha.
    private const string GlifoReordenando = "";
    private const string PropriedadeGiro = "RotationAngleInDegrees";
    // Uma volta; também é o tempo mínimo de giro, para não "piscar" em filas pequenas.
    private static readonly TimeSpan DuracaoGiroReordenar = TimeSpan.FromMilliseconds(700);

    private bool _reordenando;
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

    private void abbAdicionarArquivos_Click(object sender, RoutedEventArgs e) => Disparar(AdicionarArquivosAsync());

    private async Task AdicionarArquivosAsync()
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

    private void abbAdicionarPasta_Click(object sender, RoutedEventArgs e) => Disparar(AdicionarPastaAsync());

    private async Task AdicionarPastaAsync()
    {
        var seletor = new FolderPicker(AppWindow.Id) { SuggestedStartLocation = PickerLocationId.MusicLibrary };
        var pasta = await seletor.PickSingleFolderAsync();
        if (pasta is not null)
            await AdicionarAoFimAsync([pasta.Path]);
    }

    private void mfiEmbaralhar_Click(object sender, RoutedEventArgs e) => Disparar(ReordenarAsync(_fila.SortearOrdem, rolarParaAtual: false));

    private void mfiOrdenar_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string tag } && Enum.TryParse<CriterioOrdenacao>(tag, out var criterio))
            Disparar(ReordenarAsync(() => _fila.OrdenarPor(criterio), rolarParaAtual: true));
    }

    // A ordem nova é aplicada de uma vez (a lista é desenhada uma vez só, sem piscar). Isso ocupa a
    // thread da interface, por isso o giro do ícone é uma animação do compositor, que segue sozinha.
    private async Task ReordenarAsync(Func<IReadOnlyList<ItemFila>> obterOrdem, bool rolarParaAtual)
    {
        if (_reordenando || _fila.Itens.Count < 2)
            return;

        _reordenando = true;
        var inicio = Stopwatch.StartNew();
        try
        {
            // Desabilita a pesquisa (limpando-a, se ativa) enquanto reordena.
            AtualizarEstadoCarga();
            var animado = IniciarGiroReordenar();
            // Dois quadros: o compositor recebe a animação antes de a thread ficar ocupada.
            await Task.Delay(TimeSpan.FromMilliseconds(32));

            var destino = 0;
            foreach (var item in obterOrdem())
            {
                if (_fila.MoverPara(item, destino))
                    destino++;
            }
            // Ordenar: a música atual pode ter ido para qualquer lugar. Embaralhar: ela está no topo.
            var alvo = (rolarParaAtual ? _fila.Atual : null) ?? _fila.Itens.FirstOrDefault();
            if (alvo is not null)
                lvwFila.ScrollIntoView(alvo);

            var restante = DuracaoGiroReordenar - inicio.Elapsed;
            if (animado && restante > TimeSpan.Zero)
                await Task.Delay(restante);
        }
        finally
        {
            PararGiroReordenar();
            _reordenando = false;
            AtualizarEstadoCarga();
        }
    }

    // Com "Efeitos de animação" desligado no Windows, só troca o ícone.
    private bool IniciarGiroReordenar()
    {
        fntOrdenar.Glyph = GlifoReordenando;
        if (!new UISettings().AnimationsEnabled)
            return false;

        var visual = ElementCompositionPreview.GetElementVisual(fntOrdenar);
        var compositor = visual.Compositor;
        var giro = compositor.CreateScalarKeyFrameAnimation();
        giro.InsertKeyFrame(0f, 0f);
        giro.InsertKeyFrame(1f, 360f, compositor.CreateLinearEasingFunction());
        giro.Duration = DuracaoGiroReordenar;
        giro.IterationBehavior = AnimationIterationBehavior.Forever;

        visual.CenterPoint = new Vector3((float)(fntOrdenar.ActualWidth / 2), (float)(fntOrdenar.ActualHeight / 2), 0);
        visual.StartAnimation(PropriedadeGiro, giro);
        return true;
    }

    private void PararGiroReordenar()
    {
        var visual = ElementCompositionPreview.GetElementVisual(fntOrdenar);
        visual.StopAnimation(PropriedadeGiro);
        visual.RotationAngleInDegrees = 0;
        fntOrdenar.Glyph = GlifoOrdenar;
    }

    private void abbLimparFila_Click(object sender, RoutedEventArgs e) => Disparar(LimparFilaAsync());

    private async Task LimparFilaAsync()
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
