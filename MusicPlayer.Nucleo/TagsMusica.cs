namespace MusicPlayer.Nucleo;

// Campos editáveis pelo EditorMetadados. Faixa e Ano 0 = sem valor; gêneros separados por ";".
public sealed record TagsMusica(string Titulo, string Artista, string Album, uint Faixa, uint Ano, string Genero);
