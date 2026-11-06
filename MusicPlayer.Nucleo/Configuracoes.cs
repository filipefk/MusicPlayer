using System.Diagnostics;
using System.Text.Json;

namespace MusicPlayer.Nucleo;

// Preferências do usuário em %LocalAppData%\MusicPlayer\configuracoes.json.
// Arquivo ausente ou inválido = valores padrão.
public sealed class Configuracoes
{
    public const double VolumePadrao = 80;

    private static string Arquivo => Path.Combine(PastasAplicativo.Dados, "configuracoes.json");

    // null = idioma do Windows.
    public string? Idioma { get; set; }

    // 0 a 100.
    public double Volume { get; set; } = VolumePadrao;

    public bool Mudo { get; set; }

    public ModoRepeticao ModoRepeticao { get; set; }

    // Painel "Tocando agora" (capa e dados da música) ao lado da fila.
    public bool PainelTocandoVisivel { get; set; } = true;

    public static Configuracoes Carregar() => Carregar(Arquivo);

    public void Salvar() => Salvar(Arquivo);

    internal static Configuracoes Carregar(string arquivo)
    {
        try
        {
            if (File.Exists(arquivo))
                return JsonSerializer.Deserialize(File.ReadAllText(arquivo), ContextoJson.Default.Configuracoes)?.Normalizar()
                    ?? new Configuracoes();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            Trace.TraceError(ex.ToString());
        }

        return new Configuracoes();
    }

    internal void Salvar(string arquivo)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
            File.WriteAllText(arquivo, JsonSerializer.Serialize(this, ContextoJson.Default.Configuracoes));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            Trace.TraceError(ex.ToString());
        }
    }

    private Configuracoes Normalizar()
    {
        Volume = double.IsFinite(Volume) ? Math.Clamp(Volume, 0, 100) : VolumePadrao;
        if (!Enum.IsDefined(ModoRepeticao))
            ModoRepeticao = ModoRepeticao.Desligado;
        return this;
    }
}
