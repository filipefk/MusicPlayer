using System.Diagnostics;
using System.Text.Json;

namespace MusicPlayer.Nucleo;

// Fila salva ao fechar (%LocalAppData%\MusicPlayer\fila.json) e restaurada na abertura, pausada.
public sealed class EstadoFila
{
    private static string Arquivo => Path.Combine(PastasAplicativo.Dados, "fila.json");

    public IReadOnlyList<string> Arquivos { get; init; } = [];

    // -1 = nenhum item atual.
    public int IndiceAtual { get; init; } = -1;

    public double PosicaoSegundos { get; init; }

    public static EstadoFila Capturar(FilaReproducao fila, TimeSpan posicao)
    {
        ArgumentNullException.ThrowIfNull(fila);

        return new EstadoFila
        {
            Arquivos = fila.Itens.Select(i => i.Caminho).ToList(),
            IndiceAtual = fila.IndiceAtual,
            PosicaoSegundos = fila.Atual is null ? 0 : Math.Max(0, posicao.TotalSeconds)
        };
    }

    public static EstadoFila Carregar() => Carregar(Arquivo);

    public void Salvar() => Salvar(Arquivo);

    internal static EstadoFila Carregar(string arquivo)
    {
        try
        {
            if (File.Exists(arquivo))
                return JsonSerializer.Deserialize(File.ReadAllText(arquivo), ContextoJson.Default.EstadoFila) ?? new EstadoFila();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            Trace.TraceError(ex.ToString());
        }

        return new EstadoFila();
    }

    internal void Salvar(string arquivo)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
            File.WriteAllText(arquivo, JsonSerializer.Serialize(this, ContextoJson.Default.EstadoFila));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Trace.TraceError(ex.ToString());
        }
    }
}
