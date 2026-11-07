using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MusicPlayer.Nucleo;
using System.Collections.ObjectModel;
using Windows.System;

namespace MusicPlayer;

// Com filtro, o ListView mostra uma segunda coleção sincronizada por Move/Insert/Remove (sem recriar
// os itens nem perder a seleção). A fila em si não muda: próximo/anterior seguem a ordem completa.
public sealed partial class JanelaPrincipal
{
    // Espera curta: digitação rápida e a leitura das tags em lote refiltram uma vez só.
    private static readonly TimeSpan AtrasoFiltro = TimeSpan.FromMilliseconds(150);

    private readonly ObservableCollection<ItemFila> _itensFiltrados = [];
    private Microsoft.UI.Dispatching.DispatcherQueueTimer _temporizadorFiltro = null!;
    private string[] _palavrasFiltro = [];
    private bool _botaoLimparFiltroLigado;

    private bool FiltroAtivo => _palavrasFiltro.Length > 0;

    private void ConfigurarFiltro()
    {
        _temporizadorFiltro = _filaInterface.CreateTimer();
        _temporizadorFiltro.Interval = AtrasoFiltro;
        _temporizadorFiltro.IsRepeating = false;
        _temporizadorFiltro.Tick += (_, _) => AplicarFiltro();

        var atalho = new KeyboardAccelerator { Key = VirtualKey.F, Modifiers = VirtualKeyModifiers.Control };
        atalho.Invoked += (_, e) =>
        {
            asbFiltro.Focus(FocusState.Keyboard);
            e.Handled = true;
        };
        grdRaiz.KeyboardAccelerators.Add(atalho);
        // Sem isso o WinUI mostra "Ctrl+F" como dica ao passar o mouse sobre a janela.
        grdRaiz.KeyboardAcceleratorPlacementMode = KeyboardAcceleratorPlacementMode.Hidden;

        asbFiltro.Loaded += (_, _) => AtualizarBotaoLimparFiltro();
    }

    private void asbFiltro_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _palavrasFiltro = FiltroFila.SepararPalavras(sender.Text);
        _temporizadorFiltro.Stop();
        _temporizadorFiltro.Start();
        AtualizarBotaoLimparFiltro();
    }

    // O modelo do TextBox só mostra o X com o campo focado (estado visual ButtonVisible); o valor local
    // vence o Collapsed do modelo, então o X fica visível sempre que houver texto. O clique interno do
    // TextBox também só limpa com foco, por isso o X chama o LimparFiltro.
    private void AtualizarBotaoLimparFiltro()
    {
        if (ProcurarDescendente(asbFiltro, "DeleteButton") is not Button botao)
            return;

        if (!_botaoLimparFiltroLigado)
        {
            botao.Click += (_, _) => LimparFiltro();
            _botaoLimparFiltroLigado = true;
        }

        if (asbFiltro.Text.Length > 0)
            botao.Visibility = Visibility.Visible;
        else
            botao.ClearValue(UIElement.VisibilityProperty);
    }

    private static FrameworkElement? ProcurarDescendente(DependencyObject raiz, string nome)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(raiz); i++)
        {
            var filho = VisualTreeHelper.GetChild(raiz, i);
            if (filho is FrameworkElement elemento && elemento.Name == nome)
                return elemento;
            if (ProcurarDescendente(filho, nome) is { } encontrado)
                return encontrado;
        }
        return null;
    }

    private void asbFiltro_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && asbFiltro.Text.Length > 0)
        {
            LimparFiltro();
            e.Handled = true;
        }
    }

    private void LimparFiltro()
    {
        asbFiltro.Text = string.Empty;
        _palavrasFiltro = [];
        AplicarFiltro();
    }

    private void AplicarFiltro()
    {
        _temporizadorFiltro.Stop();

        if (!FiltroAtivo)
        {
            if (!ReferenceEquals(lvwFila.ItemsSource, _fila.Itens))
            {
                lvwFila.ItemsSource = _fila.Itens;
                _itensFiltrados.Clear();
                // De volta à fila completa: a música atual fica selecionada e à vista.
                if (_fila.Atual is { } atual)
                {
                    lvwFila.SelectedItem = atual;
                    lvwFila.ScrollIntoView(atual);
                }
            }
            // Reordenar só faz sentido com a fila completa à vista.
            lvwFila.CanReorderItems = lvwFila.CanDragItems = true;
        }
        else
        {
            SincronizarFiltrados(_fila.Itens.Where(i => FiltroFila.Corresponde(i, _palavrasFiltro)).ToList());
            if (!ReferenceEquals(lvwFila.ItemsSource, _itensFiltrados))
                lvwFila.ItemsSource = _itensFiltrados;
            lvwFila.CanReorderItems = lvwFila.CanDragItems = false;
        }

        AtualizarResumoFila();
        AtualizarPainelVazio();
    }

    private void SincronizarFiltrados(List<ItemFila> desejados)
    {
        var manter = desejados.ToHashSet();
        for (var i = _itensFiltrados.Count - 1; i >= 0; i--)
        {
            if (!manter.Contains(_itensFiltrados[i]))
                _itensFiltrados.RemoveAt(i);
        }

        for (var i = 0; i < desejados.Count; i++)
        {
            var item = desejados[i];
            if (i < _itensFiltrados.Count && ReferenceEquals(_itensFiltrados[i], item))
                continue;

            var origem = _itensFiltrados.IndexOf(item);
            if (origem >= 0)
                _itensFiltrados.Move(origem, i);
            else
                _itensFiltrados.Insert(i, item);
        }
    }

    // ScrollIntoView de um item que não está na fonte atual do ListView não tem para onde ir.
    private void RolarAteItem(ItemFila item)
    {
        if (!FiltroAtivo || _itensFiltrados.Contains(item))
            lvwFila.ScrollIntoView(item);
    }

    private void AtualizarPainelVazio()
    {
        var filaVazia = _fila.Itens.Count == 0;
        var semResultado = !filaVazia && FiltroAtivo && _itensFiltrados.Count == 0;
        pnlFilaVazia.Visibility = filaVazia || semResultado ? Visibility.Visible : Visibility.Collapsed;
        fntFilaVazia.Glyph = semResultado ? "" : "";
        txtFilaVazia.Text = Textos.Obter(semResultado ? "FiltroSemResultado" : "FilaVazia");
    }

    // Música atual na barra de reprodução: mostra na fila mesmo que o filtro a esconda.
    private void btnMusicaAtual_Click(object sender, RoutedEventArgs e)
    {
        if (_fila.Atual is not { } atual)
            return;

        // Limpar a pesquisa já seleciona e mostra a música atual.
        if (FiltroAtivo && !_itensFiltrados.Contains(atual))
        {
            LimparFiltro();
            return;
        }

        lvwFila.SelectedItem = atual;
        lvwFila.ScrollIntoView(atual);
    }
}
