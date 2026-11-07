using System.Diagnostics;
using System.Globalization;

namespace MusicPlayer.Nucleo;

// Erros inesperados vão para %LocalAppData%\MusicPlayer\erros.log com a pilha completa, para diagnóstico.
public static class RegistroErros
{
    private const long TamanhoMaximo = 1024 * 1024;
    private static readonly Lock Trava = new();

    public static string Caminho => Path.Combine(PastasAplicativo.Dados, "erros.log");

    public static void Gravar(string origem, object? erro)
    {
        var detalhe = erro is Exception ex ? $"HRESULT 0x{ex.HResult:X8}{Environment.NewLine}{ex}" : erro?.ToString();
        var texto = string.Create(CultureInfo.InvariantCulture,
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {origem}{Environment.NewLine}{detalhe}{Environment.NewLine}{Environment.NewLine}");
        Trace.TraceError(texto);

        try
        {
            lock (Trava)
            {
                Directory.CreateDirectory(PastasAplicativo.Dados);
                var arquivo = new FileInfo(Caminho);
                if (arquivo.Exists && arquivo.Length > TamanhoMaximo)
                    arquivo.Delete();
                File.AppendAllText(Caminho, texto);
            }
        }
        catch (Exception falha) when (falha is IOException or UnauthorizedAccessException)
        {
            Trace.TraceError(falha.Message);
        }
    }
}
