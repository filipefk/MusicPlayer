using System.Runtime.InteropServices;

namespace MusicPlayer;

internal static class LinhaComando
{
    // Mesmas regras de aspas e barras do Windows (CommandLineToArgvW), para caminhos com espaços.
    public static IReadOnlyList<string> Separar(string? linha)
    {
        if (string.IsNullOrWhiteSpace(linha))
            return [];

        var argumentos = NativeMethods.CommandLineToArgvW(linha, out var quantidade);
        if (argumentos == 0)
            return [];

        try
        {
            var resultado = new string[quantidade];
            for (var i = 0; i < quantidade; i++)
                resultado[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argumentos, i * IntPtr.Size)) ?? string.Empty;
            return resultado;
        }
        finally
        {
            NativeMethods.LocalFree(argumentos);
        }
    }
}
