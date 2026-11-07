using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace MusicPlayer.Nucleo;

// O item atual é guardado por referência (não por índice): reordenar a lista arrastando com o mouse
// não muda a música em reprodução, e o "próximo" passa a seguir a nova ordem.
public sealed class FilaReproducao
{
    public ObservableCollection<ItemFila> Itens { get; } = [];

    public ItemFila? Atual { get; private set; }

    public ModoRepeticao ModoRepeticao { get; set; }

    public int IndiceAtual => Atual is null ? -1 : Itens.IndexOf(Atual);

    public TimeSpan DuracaoTotal => Itens.Aggregate(TimeSpan.Zero, (total, item) => total + item.Duracao);

    public IReadOnlyList<ItemFila> Adicionar(IEnumerable<string> caminhos)
    {
        ArgumentNullException.ThrowIfNull(caminhos);

        var novos = caminhos.Select(c => new ItemFila(c)).ToList();
        foreach (var item in novos)
            Itens.Add(item);
        return novos;
    }

    // Duplo clique em um arquivo com o programa aberto: entra logo depois da música atual.
    public IReadOnlyList<ItemFila> InserirAposAtual(IEnumerable<string> caminhos)
    {
        ArgumentNullException.ThrowIfNull(caminhos);

        var novos = caminhos.Select(c => new ItemFila(c)).ToList();
        var posicao = IndiceAtual + 1;
        foreach (var item in novos)
            Itens.Insert(posicao++, item);
        return novos;
    }

    public void DefinirAtual(ItemFila? item)
    {
        if (item is not null && !Itens.Contains(item))
            throw new ArgumentException("O item não pertence à fila.", nameof(item));

        if (ReferenceEquals(Atual, item))
            return;

        if (Atual is not null)
            Atual.EhAtual = false;
        Atual = item;
        if (Atual is not null)
            Atual.EhAtual = true;
    }

    // automatico = a música terminou sozinha (o modo "Uma" só vale nesse caso; o botão Próximo sempre avança).
    public ItemFila? ObterProximo(bool automatico)
    {
        if (Itens.Count == 0)
            return null;

        var indice = IndiceAtual;
        if (indice < 0)
            return Itens[0];

        if (automatico && ModoRepeticao == ModoRepeticao.Uma)
            return Atual;

        if (indice + 1 < Itens.Count)
            return Itens[indice + 1];

        return ModoRepeticao == ModoRepeticao.Todas ? Itens[0] : null;
    }

    public ItemFila? ObterAnterior()
    {
        if (Itens.Count == 0)
            return null;

        var indice = IndiceAtual;
        if (indice < 0)
            return Itens[0];

        if (indice > 0)
            return Itens[indice - 1];

        return ModoRepeticao == ModoRepeticao.Todas ? Itens[^1] : null;
    }

    // Se o atual for removido, Seguinte é o item que passaria a tocar (o primeiro que estava depois dele).
    public ResultadoRemocao Remover(IEnumerable<ItemFila> itens)
    {
        ArgumentNullException.ThrowIfNull(itens);

        var remover = itens.Where(Itens.Contains).ToHashSet();
        if (remover.Count == 0)
            return new ResultadoRemocao(false, null);

        var atualRemovido = Atual is not null && remover.Contains(Atual);
        ItemFila? seguinte = null;
        if (atualRemovido)
        {
            seguinte = Itens.Skip(IndiceAtual + 1).FirstOrDefault(i => !remover.Contains(i));
            if (seguinte is null && ModoRepeticao == ModoRepeticao.Todas)
                seguinte = Itens.FirstOrDefault(i => !remover.Contains(i));
            DefinirAtual(null);
        }

        foreach (var item in remover)
            Itens.Remove(item);

        return new ResultadoRemocao(atualRemovido, seguinte);
    }

    // Embaralhar em duas etapas, para o app aplicar aos poucos (com progresso): primeiro a ordem nova,
    // com a música atual no topo e as demais em ordem aleatória; depois um MoverPara por item, na ordem.
    // Move (e não Clear + Add) faz o ListView manter os itens e a seleção.
    public IReadOnlyList<ItemFila> SortearOrdem()
    {
        var restantes = Itens.Where(i => !ReferenceEquals(i, Atual)).ToArray();
        RandomNumberGenerator.Shuffle(restantes.AsSpan());
        return Atual is null ? restantes : [Atual, .. restantes];
    }

    // false = o item saiu da fila (removido durante o embaralhamento).
    public bool MoverPara(ItemFila item, int destino)
    {
        var origem = Itens.IndexOf(item);
        if (origem < 0)
            return false;

        destino = Math.Clamp(destino, 0, Itens.Count - 1);
        if (origem != destino)
            Itens.Move(origem, destino);
        return true;
    }

    public void Limpar()
    {
        DefinirAtual(null);
        Itens.Clear();
    }
}
