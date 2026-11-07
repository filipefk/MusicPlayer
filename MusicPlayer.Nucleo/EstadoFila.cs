using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MusicPlayer.Nucleo;

// Fila salva ao fechar (%LocalAppData%\MusicPlayer\fila.json) e restaurada na abertura, pausada.
public sealed class EstadoFila
{
    private static string Arquivo => Path.Combine(PastasAplicativo.Dados, "fila.json");

    // Tags junto com o caminho: a fila restaurada aparece pronta, sem reler cada arquivo.
    // O gerador do System.Text.Json passa null para propriedades init ausentes no JSON (formato antigo).
    public IReadOnlyList<MusicaSalva> Itens { get; init => field = value ?? []; } = [];

    // Formato antigo (só caminhos): lido para converter em Itens, nunca gravado.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Arquivos { get; init; }

    // -1 = nenhum item atual.
    public int IndiceAtual { get; init; } = -1;

    public double PosicaoSegundos { get; init; }

    public static EstadoFila Capturar(FilaReproducao fila, TimeSpan posicao)
    {
        ArgumentNullException.ThrowIfNull(fila);

        return new EstadoFila
        {
            Itens = fila.Itens.Select(MusicaSalva.De).ToList(),
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
                return Converter(JsonSerializer.Deserialize(File.ReadAllText(arquivo), ContextoJson.Default.EstadoFila));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            Trace.TraceError(ex.ToString());
        }

        return new EstadoFila();
    }

    private static EstadoFila Converter(EstadoFila? estado) => estado switch
    {
        null => new EstadoFila(),
        { Itens.Count: 0, Arquivos: { Count: > 0 } arquivos } => new EstadoFila
        {
            Itens = arquivos.Select(a => new MusicaSalva(a, null, null, null, 0, null)).ToList(),
            IndiceAtual = estado.IndiceAtual,
            PosicaoSegundos = estado.PosicaoSegundos
        },
        _ => estado
    };

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
