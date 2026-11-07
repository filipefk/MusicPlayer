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
                if (await LerArquivoSemFalharAsync(arquivo).ConfigureAwait(false) is { } leitura)
                    yield return leitura;
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

    // Até o Path pode falhar se o serviço da consulta caiu: fica dentro do try.
    private static async Task<(string Caminho, MetadadosMusica Metadados)?> LerArquivoSemFalharAsync(StorageFile arquivo)
    {
        try
        {
            var caminho = arquivo.Path;
            return (caminho, await LerArquivoAsync(arquivo).ConfigureAwait(false));
        }
        catch (Exception ex) when (EhFalhaDeLeitura(ex))
        {
            Trace.TraceWarning($"Metadados não lidos na leitura da pasta: {ex.Message}");
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

    // A página vem do serviço de consulta do Windows, que pode cair no meio de pastas grandes
    // (RPC_E_SERVER_DIED_DNE ao percorrê-la): é copiada aqui, dentro do try. Com falha a leitura em lote
    // para, e os leitores arquivo a arquivo do app terminam o que faltou.
    private static async Task<IReadOnlyList<StorageFile>> ObterPaginaAsync(StorageFileQueryResult consulta, uint inicio)
    {
        try
        {
            var pagina = await consulta.GetFilesAsync(inicio, TamanhoPaginaPasta).AsTask().ConfigureAwait(false);
            return [.. pagina];
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
