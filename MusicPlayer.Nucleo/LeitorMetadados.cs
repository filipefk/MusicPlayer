using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;

namespace MusicPlayer.Nucleo;

// Tags lidas pelo próprio Windows (as mesmas colunas do Explorer): ID3, MP4, FLAC/Vorbis, ASF...
public static class LeitorMetadados
{
    private const uint TamanhoPaginaPasta = 200;

    public static async Task<MetadadosMusica?> LerAsync(string caminho)
    {
        try
        {
            var arquivo = await StorageFile.GetFileFromPathAsync(caminho).AsTask().ConfigureAwait(false);
            return await LerArquivoAsync(arquivo).ConfigureAwait(false);
        }
        catch (Exception ex) when (EhFalhaDeLeitura(ex))
        {
            Trace.TraceWarning($"Metadados não lidos de {caminho}: {ex.Message}");
            return null;
        }
    }

    // Pasta inteira (com subpastas) numa consulta só: o Windows já traz as tags junto (prefetch), usando o
    // índice de pesquisa quando a pasta está indexada. Bem mais rápido do que abrir arquivo por arquivo.
    public static async IAsyncEnumerable<(string Caminho, MetadadosMusica Metadados)> LerPastaAsync(string pasta)
    {
        var consulta = await CriarConsultaAsync(pasta).ConfigureAwait(false);
        if (consulta is null)
            yield break;

        for (uint inicio = 0; ; inicio += TamanhoPaginaPasta)
        {
            var pagina = await ObterPaginaAsync(consulta, inicio).ConfigureAwait(false);
            foreach (var arquivo in pagina)
            {
                if (await LerArquivoSemFalharAsync(arquivo).ConfigureAwait(false) is { } metadados)
                    yield return (arquivo.Path, metadados);
            }

            if (pagina.Count < TamanhoPaginaPasta)
                yield break;
        }
    }

    private static async Task<MetadadosMusica> LerArquivoAsync(StorageFile arquivo)
    {
        var musica = await arquivo.Properties.GetMusicPropertiesAsync().AsTask().ConfigureAwait(false);
        var artista = string.IsNullOrWhiteSpace(musica.Artist) ? musica.AlbumArtist : musica.Artist;
        // A mesma data que a restauração compara (File.GetLastWriteTimeUtc), fora da thread da interface.
        return new MetadadosMusica(musica.Title, artista, musica.Album, musica.Duration, File.GetLastWriteTimeUtc(arquivo.Path));
    }

    private static async Task<MetadadosMusica?> LerArquivoSemFalharAsync(StorageFile arquivo)
    {
        try
        {
            return await LerArquivoAsync(arquivo).ConfigureAwait(false);
        }
        catch (Exception ex) when (EhFalhaDeLeitura(ex))
        {
            Trace.TraceWarning($"Metadados não lidos de {arquivo.Path}: {ex.Message}");
            return null;
        }
    }

    private static async Task<StorageFileQueryResult?> CriarConsultaAsync(string pasta)
    {
        try
        {
            var raiz = await StorageFolder.GetFolderFromPathAsync(pasta).AsTask().ConfigureAwait(false);
            var opcoes = new QueryOptions(CommonFileQuery.DefaultQuery, FormatosAudio.Extensoes)
            {
                FolderDepth = FolderDepth.Deep,
                IndexerOption = IndexerOption.UseIndexerWhenAvailable
            };
            opcoes.SetPropertyPrefetch(PropertyPrefetchOptions.MusicProperties, []);
            return raiz.CreateFileQueryWithOptions(opcoes);
        }
        catch (Exception ex) when (EhFalhaDeLeitura(ex))
        {
            Trace.TraceWarning($"Pasta não consultada {pasta}: {ex.Message}");
            return null;
        }
    }

    private static async Task<IReadOnlyList<StorageFile>> ObterPaginaAsync(StorageFileQueryResult consulta, uint inicio)
    {
        try
        {
            return await consulta.GetFilesAsync(inicio, TamanhoPaginaPasta).AsTask().ConfigureAwait(false);
        }
        catch (Exception ex) when (EhFalhaDeLeitura(ex))
        {
            Trace.TraceWarning($"Consulta da pasta interrompida: {ex.Message}");
            return [];
        }
    }

    private static bool EhFalhaDeLeitura(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ArgumentException or COMException;
}
