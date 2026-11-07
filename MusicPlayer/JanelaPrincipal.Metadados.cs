using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MusicPlayer.Nucleo;
using Windows.Globalization.NumberFormatting;
using Windows.Media.Playback;

namespace MusicPlayer;

public sealed partial class JanelaPrincipal
{
    private const int LarguraDialogoMetadados = 420;
    private const double FaixaMaxima = 999;
    private const double AnoMaximo = 9999;

    private void EditarMetadadosSelecionado()
    {
        if (lvwFila.SelectedItems.Count == 1 && lvwFila.SelectedItems[0] is ItemFila item && EditorMetadados.PodeEditar(item.Caminho))
            Disparar(EditarMetadadosAsync(item));
    }

    private async Task EditarMetadadosAsync(ItemFila item)
    {
        var tags = await EditorMetadados.LerAsync(item.Caminho);
        if (tags is null)
        {
            MostrarAviso(Textos.Formatar("ErroLerMetadados", item.NomeArquivo));
            return;
        }

        var editadas = await PedirTagsAsync(item, tags);
        if (editadas is null || editadas == tags)
            return;

        var erro = await GravarTagsAsync(item, editadas);
        if (erro is not null)
            MostrarAviso(Textos.Formatar("ErroSalvarMetadados", item.NomeArquivo, erro));
    }

    private async Task<TagsMusica?> PedirTagsAsync(ItemFila item, TagsMusica tags)
    {
        var txbTitulo = CriarCampoTexto("CampoTitulo", tags.Titulo);
        var txbArtista = CriarCampoTexto("CampoArtista", tags.Artista);
        var txbAlbum = CriarCampoTexto("CampoAlbum", tags.Album);
        var nmbFaixa = CriarCampoNumero("CampoFaixa", tags.Faixa, FaixaMaxima);
        var nmbAno = CriarCampoNumero("CampoAno", tags.Ano, AnoMaximo);
        var txbGenero = CriarCampoTexto("CampoGenero", tags.Genero);
        txbGenero.PlaceholderText = Textos.Obter("GeneroExemplo");

        // Faixa e Ano lado a lado, como no Explorer.
        var grdNumeros = new Grid
        {
            ColumnSpacing = 12,
            ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition() }
        };
        Grid.SetColumn(nmbAno, 1);
        grdNumeros.Children.Add(nmbFaixa);
        grdNumeros.Children.Add(nmbAno);

        var pnlCampos = new StackPanel { Spacing = 12, Width = LarguraDialogoMetadados };
        pnlCampos.Children.Add(new TextBlock
        {
            Text = item.NomeArquivo,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        pnlCampos.Children.Add(txbTitulo);
        pnlCampos.Children.Add(txbArtista);
        pnlCampos.Children.Add(txbAlbum);
        pnlCampos.Children.Add(grdNumeros);
        pnlCampos.Children.Add(txbGenero);

        await _filaDialogos.WaitAsync();
        try
        {
            var dialogo = new ContentDialog
            {
                XamlRoot = grdRaiz.XamlRoot,
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                RequestedTheme = grdRaiz.ActualTheme,
                Title = Textos.Obter("EditarMetadadosTitulo"),
                Content = pnlCampos,
                PrimaryButtonText = Textos.Obter("Salvar"),
                CloseButtonText = Textos.Obter("Cancelar"),
                DefaultButton = ContentDialogButton.Primary
            };
            dialogo.Opened += (_, _) => txbTitulo.Focus(FocusState.Programmatic);
            if (await dialogo.ShowAsync() != ContentDialogResult.Primary)
                return null;
        }
        finally
        {
            _filaDialogos.Release();
        }

        return new TagsMusica(
            txbTitulo.Text.Trim(),
            txbArtista.Text.Trim(),
            txbAlbum.Text.Trim(),
            ValorNumero(nmbFaixa),
            ValorNumero(nmbAno),
            txbGenero.Text.Trim());
    }

    private static TextBox CriarCampoTexto(string chaveRotulo, string valor) => new()
    {
        Header = Textos.Obter(chaveRotulo),
        Text = valor
    };

    private static NumberBox CriarCampoNumero(string chaveRotulo, uint valor, double maximo) => new()
    {
        Header = Textos.Obter(chaveRotulo),
        Minimum = 0,
        Maximum = maximo,
        // Vazio (NaN) = sem valor na tag.
        Value = valor > 0 ? valor : double.NaN,
        SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden,
        ValidationMode = NumberBoxValidationMode.InvalidInputOverwritten,
        NumberFormatter = new DecimalFormatter
        {
            FractionDigits = 0,
            IntegerDigits = 1,
            IsGrouped = false,
            NumberRounder = new IncrementNumberRounder { Increment = 1 }
        }
    };

    private static uint ValorNumero(NumberBox campo) =>
        double.IsNaN(campo.Value) || campo.Value < 1 ? 0 : (uint)Math.Round(campo.Value);

    // A música carregada no player fica com o arquivo aberto: é solta antes de gravar
    // e recarregada depois, na mesma posição e no mesmo estado (tocando ou pausada).
    private async Task<string?> GravarTagsAsync(ItemFila item, TagsMusica tags)
    {
        var recarregar = ReferenceEquals(item, _fila.Atual) && _player.Source is not null;
        var posicao = _player.PlaybackSession.Position;
        var tocava = _player.PlaybackSession.PlaybackState is MediaPlaybackState.Playing or MediaPlaybackState.Buffering;
        var ocioso = _ocioso;
        var versao = _versaoCarga;
        if (recarregar)
        {
            versao = ++_versaoCarga;
            _player.Pause();
            _player.Source = null;
            TrocarFonte(null, null);
        }

        var erro = await EditorMetadados.SalvarAsync(item.Caminho, tags);
        item.AplicarMetadados(await LeitorMetadados.LerAsync(item.Caminho));
        MarcarLidoForaDaCarga(item);
        AgendarAtualizacaoFila();

        // Outra música pedida durante a gravação (teclas de mídia, duplo clique...): fica a escolha do usuário.
        if (recarregar && versao == _versaoCarga)
        {
            await TocarAsync(item, iniciar: tocava, posicao: posicao);
            // Fim da fila continua sendo fim da fila: arquivos adicionados depois ainda começam a tocar.
            _ocioso = ocioso;
        }
        else if (ReferenceEquals(item, _fila.Atual))
        {
            ExibirTocandoAgora(item);
        }

        return erro;
    }
}
