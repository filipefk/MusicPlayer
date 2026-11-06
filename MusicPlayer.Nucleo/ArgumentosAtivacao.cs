namespace MusicPlayer.Nucleo;

// Linha de comando: MusicPlayer.exe [--enfileirar] <arquivo ou pasta>...
// Sem --enfileirar (duplo clique / "Abrir"), o primeiro arquivo toca na hora; com ele ("Adicionar à fila"),
// tudo vai para o fim da fila. O próprio executável e argumentos desconhecidos são ignorados.
public sealed record ArgumentosAtivacao(IReadOnlyList<string> Caminhos, bool Enfileirar)
{
    public const string OpcaoEnfileirar = "--enfileirar";

    public static ArgumentosAtivacao Interpretar(IEnumerable<string> argumentos)
    {
        ArgumentNullException.ThrowIfNull(argumentos);

        var caminhos = new List<string>();
        var enfileirar = false;
        foreach (var argumento in argumentos)
        {
            var texto = argumento?.Trim().Trim('"');
            if (string.IsNullOrEmpty(texto))
                continue;

            if (string.Equals(texto, OpcaoEnfileirar, StringComparison.OrdinalIgnoreCase))
                enfileirar = true;
            else if (Directory.Exists(texto) || (File.Exists(texto) && FormatosAudio.EhSuportado(texto)))
                caminhos.Add(texto);
        }

        return new ArgumentosAtivacao(caminhos, enfileirar);
    }
}
