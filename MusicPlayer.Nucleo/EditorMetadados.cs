using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace MusicPlayer.Nucleo;

// Grava as tags pelo próprio Windows (o mesmo property handler do Explorer em Propriedades > Detalhes).
// Só MP3 (ID3v2) e M4A (MP4) têm gravação garantida; nos demais formatos o handler pode ser só leitura.
public static class EditorMetadados
{
    private const string SeparadorGeneros = "; ";
    private const string SeparadorNomeComEspacos = " - ";
    private const int TentativasGravacao = 3;
    private const int EsperaEntreTentativasMs = 250;
    // HRESULT_FROM_WIN32(ERROR_SHARING_VIOLATION): o arquivo acabou de ser liberado pelo player e ainda está aberto.
    private const int ErroCompartilhamento = unchecked((int)0x80070020);

    private static readonly HashSet<string> ExtensoesEditaveis = new([".mp3", ".m4a"], StringComparer.OrdinalIgnoreCase);

    public static bool PodeEditar(string caminho) =>
        !string.IsNullOrWhiteSpace(caminho) && ExtensoesEditaveis.Contains(Path.GetExtension(caminho));

    // "Artista - Título" ou "Artista_Título": vale o primeiro " - "; sem ele, o primeiro "-" ou "_" da esquerda para a direita.
    // Sem título nem artista nas tags, preenche os dois a partir do nome do arquivo (sem a extensão).
    public static TagsMusica SugerirPeloNome(string caminho, TagsMusica tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        if (!string.IsNullOrWhiteSpace(tags.Titulo) || !string.IsNullOrWhiteSpace(tags.Artista))
            return tags;

        var nome = Path.GetFileNameWithoutExtension(caminho);
        // " - " tem preferência, para não quebrar nomes com hífen como "Jay-Z - Song".
        var separador = nome.IndexOf(SeparadorNomeComEspacos, StringComparison.Ordinal);
        var tamanhoSeparador = SeparadorNomeComEspacos.Length;
        if (separador < 0)
        {
            separador = nome.IndexOfAny(['-', '_']);
            tamanhoSeparador = 1;
        }

        if (separador < 0)
            return tags;

        var artista = nome[..separador].Trim();
        var titulo = nome[(separador + tamanhoSeparador)..].Trim();
        return artista.Length == 0 || titulo.Length == 0 ? tags : tags with { Titulo = titulo, Artista = artista };
    }

    public static async Task<TagsMusica?> LerAsync(string caminho)
    {
        try
        {
            var musica = await ObterPropriedadesAsync(caminho);
            return new TagsMusica(
                musica.Title ?? string.Empty,
                musica.Artist ?? string.Empty,
                musica.Album ?? string.Empty,
                musica.TrackNumber,
                musica.Year,
                string.Join(SeparadorGeneros, musica.Genre));
        }
        catch (Exception ex) when (EhErroArquivo(ex))
        {
            Trace.TraceWarning($"Tags não lidas de {caminho}: {ex.Message}");
            return null;
        }
    }

    // Devolve null quando gravou; senão, a mensagem do erro.
    public static async Task<string?> SalvarAsync(string caminho, TagsMusica tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                var musica = await ObterPropriedadesAsync(caminho);
                musica.Title = tags.Titulo.Trim();
                musica.Artist = tags.Artista.Trim();
                musica.Album = tags.Album.Trim();
                musica.TrackNumber = tags.Faixa;
                musica.Year = tags.Ano;
                musica.Genre.Clear();
                foreach (var genero in tags.Genero.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    musica.Genre.Add(genero);

                await musica.SavePropertiesAsync();
                return null;
            }
            catch (Exception ex) when (EhErroArquivo(ex))
            {
                if (ex.HResult == ErroCompartilhamento && tentativa < TentativasGravacao)
                {
                    await Task.Delay(EsperaEntreTentativasMs);
                    continue;
                }

                Trace.TraceWarning($"Tags não gravadas em {caminho}: {ex.Message}");
                return ex.Message;
            }
        }
    }

    private static async Task<MusicProperties> ObterPropriedadesAsync(string caminho)
    {
        var arquivo = await StorageFile.GetFileFromPathAsync(caminho);
        return await arquivo.Properties.GetMusicPropertiesAsync();
    }

    private static bool EhErroArquivo(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ArgumentException or COMException;
}
