using System.Globalization;

namespace MusicPlayer.Nucleo;

public static class FormatoTempo
{
    // "3:07" ou, a partir de uma hora, "1:02:09".
    public static string Formatar(TimeSpan tempo)
    {
        if (tempo < TimeSpan.Zero)
            tempo = TimeSpan.Zero;

        return tempo.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)tempo.TotalHours}:{tempo.Minutes:00}:{tempo.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{tempo.Minutes}:{tempo.Seconds:00}");
    }
}
