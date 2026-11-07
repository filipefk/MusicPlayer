using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MusicPlayer.Nucleo;

namespace MusicPlayer;

// Leitura das tags: alguns leitores trabalham sobre uma fila de pendentes, sempre começando pelo que
// está visível na lista; pastas adicionadas também são lidas em lote pelo Windows (LerPastaAsync).
// Enquanto houver pendentes, a pesquisa e o embaralhar ficam desabilitados.
public sealed partial class JanelaPrincipal
{
    private const int LeiturasSimultaneasMetadados = 8;
    // Resumo, filtro e progresso são atualizados no máximo a esta frequência durante a carga.
    private static readonly TimeSpan IntervaloAtualizacaoFila = TimeSpan.FromMilliseconds(200);

    private readonly Queue<ItemFila> _filaLeitura = new();
    private readonly HashSet<ItemFila> _pendentes = [];
    private DispatcherQueueTimer _temporizadorAtualizacao = null!;
    private int _leitoresAtivos;
    private int _emLeitura;
    private int _totalCarga;
    private int _concluidosCarga;
    // Muda ao limpar a fila: leituras já em andamento não contam para a carga nova.
    private int _geracaoCarga;

    private bool EmCarga => _pendentes.Count > 0 || _emLeitura > 0;

    private void ConfigurarCarga()
    {
        _temporizadorAtualizacao = _filaInterface.CreateTimer();
        _temporizadorAtualizacao.Interval = IntervaloAtualizacaoFila;
        _temporizadorAtualizacao.IsRepeating = false;
        _temporizadorAtualizacao.Tick += (_, _) => AtualizarFilaEmLote();
    }

    // Limita a frequência sem adiar: chamadas seguidas não reiniciam a espera.
    private void AgendarAtualizacaoFila()
    {
        if (!_temporizadorAtualizacao.IsRunning)
            _temporizadorAtualizacao.Start();
    }

    private void AtualizarFilaEmLote()
    {
        _temporizadorAtualizacao.Stop();
        AtualizarEstadoCarga();
        AtualizarResumoFila();
        AtualizarPainelVazio();
        if (FiltroAtivo)
            AplicarFiltro();
    }

    private void CarregarMetadados(IReadOnlyList<ItemFila> itens, IReadOnlyCollection<string>? pastas = null)
    {
        var novos = new List<ItemFila>();
        foreach (var item in itens)
        {
            if (!item.MetadadosCarregados && _pendentes.Add(item))
            {
                _filaLeitura.Enqueue(item);
                novos.Add(item);
            }
        }
        if (novos.Count == 0)
            return;

        _totalCarga += novos.Count;
        var iniciar = Math.Min(LeiturasSimultaneasMetadados - _leitoresAtivos, novos.Count);
        for (var i = 0; i < iniciar; i++)
        {
            _leitoresAtivos++;
            Disparar(LerPendentesAsync());
        }

        if (pastas is { Count: > 0 })
        {
            var porCaminho = novos.ToLookup(i => i.Caminho, StringComparer.OrdinalIgnoreCase);
            foreach (var pasta in pastas)
                Disparar(LerPastaAsync(pasta, porCaminho, _geracaoCarga));
        }

        AtualizarEstadoCarga();
        AgendarAtualizacaoFila();
    }

    private async Task LerPendentesAsync()
    {
        try
        {
            while (ProximoPendente() is { } item)
            {
                var geracao = _geracaoCarga;
                _emLeitura++;
                MetadadosMusica? metadados;
                try
                {
                    metadados = await LeitorMetadados.LerAsync(item.Caminho);
                }
                finally
                {
                    _emLeitura--;
                }

                if (geracao != _geracaoCarga)
                {
                    AgendarAtualizacaoFila();
                    continue;
                }

                // Tocar ou editar pode ter lido as tags enquanto isso.
                if (!item.MetadadosCarregados)
                    item.AplicarMetadados(metadados);
                ConcluirLeitura(item);
            }
        }
        finally
        {
            _leitoresAtivos--;
        }
    }

    private async Task LerPastaAsync(string pasta, ILookup<string, ItemFila> porCaminho, int geracao)
    {
        await foreach (var (caminho, metadados) in LeitorMetadados.LerPastaAsync(pasta))
        {
            if (geracao != _geracaoCarga || _pendentes.Count == 0)
                return;

            foreach (var item in porCaminho[caminho])
            {
                if (_pendentes.Remove(item))
                {
                    item.AplicarMetadados(metadados);
                    ConcluirLeitura(item);
                }
            }
        }
    }

    // Primeiro um pendente que esteja na tela; senão, o próximo na ordem em que entrou.
    private ItemFila? ProximoPendente()
    {
        if (_pendentes.Count == 0)
            return null;

        if (lvwFila.ItemsPanelRoot is ItemsStackPanel painel && painel.FirstVisibleIndex >= 0)
        {
            var ultimo = Math.Min(painel.LastVisibleIndex, lvwFila.Items.Count - 1);
            for (var i = painel.FirstVisibleIndex; i <= ultimo; i++)
            {
                if (lvwFila.Items[i] is ItemFila visivel && _pendentes.Remove(visivel))
                    return visivel;
            }
        }

        // Os que já saíram pelo caminho da tela continuam na Queue e são só descartados aqui.
        while (_filaLeitura.TryDequeue(out var item))
        {
            if (_pendentes.Remove(item))
                return item;
        }
        return null;
    }

    private void ConcluirLeitura(ItemFila item)
    {
        _concluidosCarga++;
        if (ReferenceEquals(item, _fila.Atual))
            ExibirTocandoAgora(item);
        AgendarAtualizacaoFila();
    }

    // Tags lidas fora dos leitores (música tocada ou editada antes da vez): sai dos pendentes.
    private void MarcarLidoForaDaCarga(ItemFila item)
    {
        if (_pendentes.Remove(item))
            ConcluirLeitura(item);
    }

    private void CancelarCarga()
    {
        _geracaoCarga++;
        _filaLeitura.Clear();
        _pendentes.Clear();
        _totalCarga = _concluidosCarga = 0;
        AtualizarEstadoCarga();
    }

    private void AtualizarEstadoCarga()
    {
        var emCarga = EmCarga;
        // Pesquisa ativa quando chega uma carga nova: volta à lista completa antes de desabilitar a caixa.
        if (emCarga && FiltroAtivo)
            LimparFiltro();

        asbFiltro.IsEnabled = !emCarga;
        asbFiltro.PlaceholderText = Textos.Obter(emCarga ? "PesquisarCarregando" : "Pesquisar");
        abbEmbaralhar.IsEnabled = !emCarga && _fila.Itens.Count > 1;
        prbCarga.Visibility = emCarga ? Visibility.Visible : Visibility.Collapsed;

        if (emCarga)
        {
            prbCarga.Maximum = Math.Max(1, _totalCarga);
            prbCarga.Value = _concluidosCarga;
        }
        else
        {
            _totalCarga = _concluidosCarga = 0;
        }
    }
}
