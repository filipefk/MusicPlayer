using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using MusicPlayer.Nucleo;

namespace MusicPlayer;

public sealed partial class JanelaPrincipal
{
    // Com vários arquivos selecionados no Explorer, o Windows abre um processo por arquivo; cada um redireciona
    // a sua ativação para cá. As que chegam dentro deste intervalo são tratadas juntas, na ordem de chegada.
    private const int IntervaloLoteAtivacaoMs = 400;

    private DispatcherQueueTimer _temporizadorAtivacao = null!;
    private readonly List<string> _recebidosParaTocar = [];
    private readonly List<string> _recebidosParaEnfileirar = [];

    private void ConfigurarAtivacao()
    {
        _temporizadorAtivacao = _filaInterface.CreateTimer();
        _temporizadorAtivacao.Interval = TimeSpan.FromMilliseconds(IntervaloLoteAtivacaoMs);
        _temporizadorAtivacao.IsRepeating = false;
        _temporizadorAtivacao.Tick += (_, _) => Disparar(ProcessarAtivacoesAsync());
    }

    // Chamado na thread da interface (App.ReceberAtivacao e IniciarAsync).
    public void ReceberAtivacao(ArgumentosAtivacao argumentos)
    {
        ArgumentNullException.ThrowIfNull(argumentos);

        TrazerParaFrente();
        if (argumentos.Caminhos.Count == 0)
            return;

        (argumentos.Enfileirar ? _recebidosParaEnfileirar : _recebidosParaTocar).AddRange(argumentos.Caminhos);
        _temporizadorAtivacao.Stop();
        _temporizadorAtivacao.Start();
    }

    // "Abrir" (duplo clique): entram logo depois da música atual e o primeiro começa a tocar.
    // "Adicionar à fila": vão para o fim, sem interromper o que está tocando.
    private async Task ProcessarAtivacoesAsync()
    {
        var paraTocar = _recebidosParaTocar.ToList();
        var paraEnfileirar = _recebidosParaEnfileirar.ToList();
        _recebidosParaTocar.Clear();
        _recebidosParaEnfileirar.Clear();

        if (paraTocar.Count > 0)
        {
            var (arquivos, pastas) = await ExpandirAsync(paraTocar);
            if (arquivos.Count > 0)
            {
                var novos = _fila.InserirAposAtual(arquivos);
                CarregarMetadados(novos, pastas);
                await TocarAsync(novos[0]);
            }
        }

        if (paraEnfileirar.Count > 0)
            await AdicionarAoFimAsync(paraEnfileirar);
    }

    private void TrazerParaFrente()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } apresentador)
            apresentador.Restore();
        Activate();
    }
}
