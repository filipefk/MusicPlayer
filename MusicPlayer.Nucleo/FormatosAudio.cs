namespace MusicPlayer.Nucleo;

// Formatos que o Media Foundation do Windows 10/11 decodifica sem codecs de terceiros.
// Ogg (Vorbis) e Opus dependem das Web Media Extensions, que já vêm no Windows 11.
public static class FormatosAudio
{
    public static IReadOnlyList<string> Extensoes { get; } =
    [
        ".mp3", ".m4a", ".aac", ".wav", ".wma", ".flac", ".alac", ".ogg", ".oga", ".opus", ".ac3", ".ec3", ".3gp", ".amr", ".adts"
    ];

    private static readonly HashSet<string> ConjuntoExtensoes = new(Extensoes, StringComparer.OrdinalIgnoreCase);

    public static bool EhSuportado(string caminho) =>
        !string.IsNullOrWhiteSpace(caminho) && ConjuntoExtensoes.Contains(Path.GetExtension(caminho));
}
