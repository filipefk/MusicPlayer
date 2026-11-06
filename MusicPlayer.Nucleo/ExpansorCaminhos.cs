namespace MusicPlayer.Nucleo;

// Transforma o que o usuário escolheu ou soltou (arquivos e pastas) na lista de músicas a enfileirar:
// arquivos na ordem recebida; pastas com as subpastas, em ordem alfabética do caminho.
public static class ExpansorCaminhos
{
    private static readonly EnumerationOptions OpcoesPasta = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System
    };

    public static IReadOnlyList<string> Expandir(IEnumerable<string> caminhos)
    {
        ArgumentNullException.ThrowIfNull(caminhos);

        var resultado = new List<string>();
        foreach (var caminho in caminhos)
        {
            if (string.IsNullOrWhiteSpace(caminho))
                continue;

            if (File.Exists(caminho))
            {
                if (FormatosAudio.EhSuportado(caminho))
                    resultado.Add(Path.GetFullPath(caminho));
            }
            else if (Directory.Exists(caminho))
            {
                resultado.AddRange(ListarPasta(caminho));
            }
        }

        return resultado;
    }

    private static List<string> ListarPasta(string pasta)
    {
        try
        {
            return Directory.EnumerateFiles(pasta, "*", OpcoesPasta)
                .Where(FormatosAudio.EhSuportado)
                .Order(StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
