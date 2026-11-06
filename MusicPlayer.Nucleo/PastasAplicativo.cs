namespace MusicPlayer.Nucleo;

public static class PastasAplicativo
{
    // App sem pacote: a pasta do programa (Program Files) não tem permissão de escrita.
    public static string Dados =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MusicPlayer");
}
