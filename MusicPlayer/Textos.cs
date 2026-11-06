using Microsoft.Windows.ApplicationModel.Resources;
using System.Diagnostics;
using System.Globalization;

namespace MusicPlayer;

// Textos de interface (Strings\<idioma>\Resources.resw, compilados no MusicPlayer.pri).
// Usa um ResourceContext próprio, com o idioma definido pelo programa: trocar o idioma vale na hora.
public static class Textos
{
    public const string Portugues = "pt-BR";
    public const string Ingles = "en-US";
    public const string Espanhol = "es-ES";

    public static IReadOnlyList<string> IdiomasSuportados { get; } = [Portugues, Ingles, Espanhol];

    // Lido antes de qualquer troca: depois, o CurrentUICulture passa a ser o idioma escolhido.
    private static readonly string IdiomaInterfaceWindows = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    private static readonly ResourceManager? Gerenciador = CriarGerenciador();
    private static readonly ResourceMap? Mapa = Gerenciador?.MainResourceMap.TryGetSubtree("Resources");
    private static readonly ResourceContext? Contexto = Gerenciador?.CreateResourceContext();

    public static string IdiomaAtual { get; private set; } = Portugues;

    public static void DefinirIdioma(string idioma)
    {
        IdiomaAtual = IdiomasSuportados.Contains(idioma) ? idioma : Ingles;
        if (Contexto is not null)
            Contexto.QualifierValues["Language"] = IdiomaAtual;

        var cultura = CultureInfo.GetCultureInfo(IdiomaAtual);
        CultureInfo.CurrentUICulture = cultura;
        CultureInfo.DefaultThreadCurrentUICulture = cultura;
    }

    // Idioma do Windows, se for um dos suportados; nos demais casos, inglês.
    public static string IdiomaDoWindows() =>
        IdiomaInterfaceWindows switch
        {
            "pt" => Portugues,
            "es" => Espanhol,
            _ => Ingles
        };

    public static string Obter(string chave)
    {
        try
        {
            if (Mapa is not null && Contexto is not null)
            {
                var valor = Mapa.TryGetValue(chave, Contexto)?.ValueAsString;
                if (!string.IsNullOrEmpty(valor))
                    return valor;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError(ex.ToString());
        }

        Trace.TraceWarning($"Texto não encontrado: {chave}");
        return chave;
    }

    public static string Formatar(string chave, params object?[] valores) =>
        string.Format(CultureInfo.CurrentCulture, Obter(chave), valores);

    private static ResourceManager? CriarGerenciador()
    {
        try
        {
            return new ResourceManager();
        }
        catch (Exception ex)
        {
            Trace.TraceError(ex.ToString());
            return null;
        }
    }
}
