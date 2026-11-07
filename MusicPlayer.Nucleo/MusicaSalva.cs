namespace MusicPlayer.Nucleo;

// Item da fila no fila.json. Sem ModificadoUtc, as tags não foram lidas e só o caminho vale.
public sealed record MusicaSalva(string Caminho, string? Titulo, string? Artista, string? Album, double DuracaoSegundos, DateTime? ModificadoUtc)
{
    public static MusicaSalva De(ItemFila item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.MetadadosCarregados && item.ModificadoUtc is { } modificado
            ? new MusicaSalva(item.Caminho, item.Titulo, item.Artista, item.Album, item.Duracao.TotalSeconds, modificado)
            : new MusicaSalva(item.Caminho, null, null, null, 0, null);
    }

    // Só devolve as tags se o arquivo não mudou desde que foram lidas.
    public MetadadosMusica? ObterMetadadosValidos(DateTime modificadoAtualUtc) =>
        ModificadoUtc == modificadoAtualUtc
            ? new MetadadosMusica(Titulo, Artista, Album, TimeSpan.FromSeconds(DuracaoSegundos), ModificadoUtc)
            : null;
}
