using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicPlayer.Nucleo;

public sealed partial class ItemFila : ObservableObject
{
    public ItemFila(string caminho)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminho);
        Caminho = caminho;
        Titulo = Path.GetFileNameWithoutExtension(caminho);
        Artista = string.Empty;
        Album = string.Empty;
    }

    public string Caminho { get; }

    public string NomeArquivo => Path.GetFileName(Caminho);

    [ObservableProperty]
    public partial string Titulo { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitulo))]
    public partial string Artista { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitulo))]
    public partial string Album { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DuracaoTexto))]
    public partial TimeSpan Duracao { get; set; }

    // Definido pela FilaReproducao: é o item em reprodução (ou pausado).
    [ObservableProperty]
    public partial bool EhAtual { get; set; }

    public bool MetadadosCarregados { get; private set; }

    public string Subtitulo =>
        string.IsNullOrEmpty(Artista) ? Album
        : string.IsNullOrEmpty(Album) ? Artista
        : $"{Artista} · {Album}";

    // A interface troca o subtítulo vazio por um texto padrão no idioma atual; após trocar o idioma, relê.
    public void AtualizarTextos() => OnPropertyChanged(nameof(Subtitulo));

    public string DuracaoTexto => Duracao > TimeSpan.Zero ? FormatoTempo.Formatar(Duracao) : string.Empty;

    public void AplicarMetadados(MetadadosMusica? metadados)
    {
        MetadadosCarregados = true;
        if (metadados is null)
            return;

        // Sem título (inclusive apagado na edição das tags): volta ao nome do arquivo.
        Titulo = string.IsNullOrWhiteSpace(metadados.Titulo) ? Path.GetFileNameWithoutExtension(Caminho) : metadados.Titulo.Trim();
        Artista = metadados.Artista?.Trim() ?? string.Empty;
        Album = metadados.Album?.Trim() ?? string.Empty;
        if (metadados.Duracao > TimeSpan.Zero)
            Duracao = metadados.Duracao;
    }
}
