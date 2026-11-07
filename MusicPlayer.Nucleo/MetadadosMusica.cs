namespace MusicPlayer.Nucleo;

// ModificadoUtc = data do arquivo quando as tags foram lidas: decide se as tags salvas no fila.json ainda valem.
public sealed record MetadadosMusica(string? Titulo, string? Artista, string? Album, TimeSpan Duracao, DateTime? ModificadoUtc = null);
