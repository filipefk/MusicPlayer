using System.Text.Json.Serialization;

namespace MusicPlayer.Nucleo;

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Configuracoes))]
[JsonSerializable(typeof(EstadoFila))]
internal sealed partial class ContextoJson : JsonSerializerContext
{
}
