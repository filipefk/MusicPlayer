using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MusicPlayer.Nucleo;

namespace MusicPlayer;

public sealed partial class JanelaPrincipal
{
    // Escolha salva (null = idioma do Windows); define o item marcado no menu Idioma.
    private string? _idiomaEscolhido;

    private void rmiIdioma_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioMenuFlyoutItem { Tag: string codigo })
            return;

        _idiomaEscolhido = string.IsNullOrEmpty(codigo) ? null : codigo;
        var configuracoes = Configuracoes.Carregar();
        configuracoes.Idioma = _idiomaEscolhido;
        configuracoes.Salvar();

        Textos.DefinirIdioma(_idiomaEscolhido ?? Textos.IdiomaDoWindows());
        App.DefinirIdiomaDoWinUI(Textos.IdiomaAtual);
        AplicarIdioma();
    }

    // Chamado no construtor e a cada troca: os textos que só existem ao abrir (diálogos, menu de
    // contexto, "Sobre", avisos) já são lidos de Textos na hora e não precisam de nada aqui.
    private void AplicarIdioma()
    {
        abbAdicionarArquivos.Label = Textos.Obter("AdicionarArquivos");
        ToolTipService.SetToolTip(abbAdicionarArquivos, Textos.Obter("AdicionarArquivosDica"));
        abbAdicionarPasta.Label = Textos.Obter("AdicionarPasta");
        ToolTipService.SetToolTip(abbAdicionarPasta, Textos.Obter("AdicionarPastaDica"));
        abbOrdenar.Label = Textos.Obter("Ordenar");
        ToolTipService.SetToolTip(abbOrdenar, Textos.Obter("OrdenarDica"));
        mfiOrdenarTitulo.Text = Textos.Obter("OrdenarTitulo");
        mfiOrdenarArtista.Text = Textos.Obter("OrdenarArtista");
        mfiOrdenarNomeArquivo.Text = Textos.Obter("OrdenarNomeArquivo");
        mfiOrdenarPasta.Text = Textos.Obter("OrdenarPasta");
        mfiEmbaralhar.Text = Textos.Obter("Embaralhar");
        abbLimparFila.Label = Textos.Obter("LimparFila");
        ToolTipService.SetToolTip(abbLimparFila, Textos.Obter("LimparFilaDica"));
        abbPreencherPeloNome.Label = Textos.Obter("PreencherPeloNome");
        abbPainelTocando.Label = Textos.Obter("PainelTocando");
        AtualizarDicaPainelTocando();

        abbIdioma.Label = Textos.Obter("Idioma");
        ToolTipService.SetToolTip(abbIdioma, Textos.Obter("IdiomaDica"));
        rmiIdiomaWindows.Text = Textos.Obter("IdiomaWindows");
        MarcarIdiomaEscolhido();

        abbSobre.Label = Textos.Obter("Sobre");
        ToolTipService.SetToolTip(abbSobre, Textos.Obter("SobreDica"));

        txtTituloFila.Text = Textos.Obter("Fila");
        AtualizarEstadoCarga();
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(asbFiltro, Textos.Obter("Pesquisar"));
        AtualizarPainelVazio();
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(lvwFila, Textos.Obter("Fila"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(sldPosicao, Textos.Obter("Posicao"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(sldVolume, Textos.Obter("Volume"));
        AtualizarResumoFila();

        DefinirDica(btnAnterior, Textos.Obter("AnteriorDica"));
        DefinirDica(btnProximo, Textos.Obter("ProximoDica"));
        AtualizarBotaoTocar();
        AtualizarBotaoRepetir();
        AtualizarBotaoVolume();
        ExibirTocandoAgora(_fila.Atual);
    }

    private void MarcarIdiomaEscolhido()
    {
        rmiIdiomaWindows.IsChecked = _idiomaEscolhido is null;
        rmiPortugues.IsChecked = _idiomaEscolhido == Textos.Portugues;
        rmiIngles.IsChecked = _idiomaEscolhido == Textos.Ingles;
        rmiEspanhol.IsChecked = _idiomaEscolhido == Textos.Espanhol;
    }
}
