using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Numerics;
using Windows.UI.ViewManagement;

namespace MusicPlayer;

// Roda do mouse no topo ou no fim da fila: a lista se desloca um pouco e volta, como se forçasse o limite.
// Toque e touchpad de precisão já têm o elástico nativo (DirectManipulation) e não passam por aqui.
public sealed partial class JanelaPrincipal
{
    private const float DeslocamentoLimiteRolagem = 24f;
    private static readonly TimeSpan DuracaoLimiteRolagem = TimeSpan.FromMilliseconds(350);
    private const string PropriedadeDeslocamento = "Translation.Y";
    // Tolerância para o arredondamento do offset em escalas de DPI fracionárias.
    private const double ToleranciaFimRolagem = 0.5;

    private readonly UISettings _configuracoesInterface = new();
    private ScrollViewer? _rolagemFila;

    private void ConfigurarRolagem()
    {
        // O ScrollViewer interno marca a roda como tratada.
        lvwFila.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler(lvwFila_PointerWheelChanged), true);
        lvwFila.Loaded += (_, _) => _rolagemFila ??= ProcurarDescendentePorTipo<ScrollViewer>(lvwFila);
    }

    private void lvwFila_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_rolagemFila is null || lvwFila.Items.Count == 0 || !lvwFila.IsEnabled)
            return;

        var ponto = e.GetCurrentPoint(lvwFila).Properties;
        if (ponto.IsHorizontalMouseWheel || ponto.MouseWheelDelta == 0)
            return;

        float direcao;
        if (ponto.MouseWheelDelta > 0 && _rolagemFila.VerticalOffset <= 0)
            direcao = 1f;
        else if (ponto.MouseWheelDelta < 0 && _rolagemFila.VerticalOffset >= _rolagemFila.ScrollableHeight - ToleranciaFimRolagem)
            direcao = -1f;
        else
            return;

        AnimarLimiteRolagem(direcao);
    }

    // Cada tique reinicia a animação a partir do valor atual: girando sem parar, a lista fica "segura" no limite.
    private void AnimarLimiteRolagem(float direcao)
    {
        // O painel é recriado quando o ItemsSource troca (filtro), por isso não é guardado.
        if (!_configuracoesInterface.AnimationsEnabled || lvwFila.ItemsPanelRoot is not { } painel)
            return;

        ElementCompositionPreview.SetIsTranslationEnabled(painel, true);
        var visual = ElementCompositionPreview.GetElementVisual(painel);
        var compositor = visual.Compositor;

        var animacao = compositor.CreateScalarKeyFrameAnimation();
        animacao.InsertExpressionKeyFrame(0f, "this.StartingValue");
        animacao.InsertKeyFrame(0.3f, direcao * DeslocamentoLimiteRolagem,
            compositor.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0.8f), new Vector2(0.4f, 1f)));
        animacao.InsertKeyFrame(1f, 0f,
            compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0f), new Vector2(0.2f, 1f)));
        animacao.Duration = DuracaoLimiteRolagem;

        visual.StartAnimation(PropriedadeDeslocamento, animacao);
    }

    private static T? ProcurarDescendentePorTipo<T>(DependencyObject raiz) where T : DependencyObject
    {
        var quantidade = VisualTreeHelper.GetChildrenCount(raiz);
        for (var i = 0; i < quantidade; i++)
        {
            var filho = VisualTreeHelper.GetChild(raiz, i);
            if (filho is T encontrado)
                return encontrado;
            if (ProcurarDescendentePorTipo<T>(filho) is { } neto)
                return neto;
        }
        return null;
    }
}
