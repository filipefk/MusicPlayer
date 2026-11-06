using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Storage;

namespace MusicPlayer.Nucleo;

// Tags lidas pelo próprio Windows (as mesmas colunas do Explorer): ID3, MP4, FLAC/Vorbis, ASF...
public static class LeitorMetadados
{
    public static async Task<MetadadosMusica?> LerAsync(string caminho)
    {
        try
        {
            var arquivo = await StorageFile.GetFileFromPathAsync(caminho);
            var musica = await arquivo.Properties.GetMusicPropertiesAsync();
            var artista = string.IsNullOrWhiteSpace(musica.Artist) ? musica.AlbumArtist : musica.Artist;
            return new MetadadosMusica(musica.Title, artista, musica.Album, musica.Duration);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or COMException)
        {
            Trace.TraceWarning($"Metadados não lidos de {caminho}: {ex.Message}");
            return null;
        }
    }
}
