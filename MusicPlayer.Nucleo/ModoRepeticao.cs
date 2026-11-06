using System.Text.Json.Serialization;

namespace MusicPlayer.Nucleo;

[JsonConverter(typeof(JsonStringEnumConverter<ModoRepeticao>))]
public enum ModoRepeticao
{
    Desligado,
    Todas,
    Uma
}
