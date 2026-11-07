using System.Globalization;

namespace MusicPlayer.Nucleo;

// Cada palavra do termo precisa aparecer em alguma informação da música (título, artista, álbum, duração
// ou caminho completo do arquivo), sem diferenciar maiúsculas nem acentos ("joao" encontra "João").
public static class FiltroFila
{
    private const CompareOptions Opcoes = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public static string[] SepararPalavras(string? termo) =>
        termo?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

    public static bool Corresponde(ItemFila item, IReadOnlyCollection<string> palavras)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(palavras);

        var comparador = CultureInfo.CurrentCulture.CompareInfo;
        string[] campos = [item.Titulo, item.Artista, item.Album, item.DuracaoTexto, item.Caminho];
        return palavras.All(palavra => campos.Any(campo => comparador.IndexOf(campo, palavra, Opcoes) >= 0));
    }
}
