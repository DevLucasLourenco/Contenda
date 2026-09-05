using System;
using System.Collections.Generic;

namespace Contenda.Input;

/// <summary>
/// Trie de prefixos: casa uma sequência de <see cref="CommandDirection"/> com
/// o item que a declarou.
/// </summary>
/// <typeparam name="T">
/// O que uma sequência resolve para — <c>AbilityDefinition</c> no jogo de
/// verdade, a partir do ticket 14. Genérico de propósito: esta classe nasce
/// ANTES de <c>AbilityDefinition</c> existir, e <c>AbilityDefinition</c> é um
/// <c>Resource</c> — construí-lo fora do processo do Godot derruba o processo
/// (mesmo motivo do <c>StringName</c>). Manter o resolver genérico é o que
/// permite testá-lo inteiro em xUnit, sem esperar pelo ticket 14 nem tocar
/// nesta classe quando ele chegar.
/// </typeparam>
/// <remarks>
/// Construído uma vez, a partir da lista de habilidades do personagem
/// equipado — spec 03 §6. Não há como adicionar uma entrada depois de
/// construído; uma transformação que traz habilidades novas (M4) reconstrói o
/// resolver inteiro, não remonta esta instância.
/// </remarks>
public sealed class AbilityComboResolver<T>
    where T : class
{
    private sealed class No
    {
        public T? Item;
        public bool TemItem;
        public Dictionary<CommandDirection, No>? Filhos;
    }

    private readonly No _raiz = new();

    /// <param name="itens">As entradas a indexar.</param>
    /// <param name="sequenciaDe">Extrai a sequência de comando de um item.</param>
    /// <param name="descreverPara">
    /// Como nomear um item numa mensagem de erro. Sem isto, cai para
    /// <c>ToString()</c> — funciona, mas "DashSlash" é mais claro que o nome
    /// da classe.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Duas entradas declaram a mesma sequência — falha alto no carregamento,
    /// nunca em silêncio. Ver ticket 13.
    /// </exception>
    public AbilityComboResolver(
        IEnumerable<T> itens,
        Func<T, IReadOnlyList<CommandDirection>> sequenciaDe,
        Func<T, string>? descreverPara = null)
    {
        ArgumentNullException.ThrowIfNull(itens);
        ArgumentNullException.ThrowIfNull(sequenciaDe);

        foreach (var item in itens)
        {
            var sequencia = sequenciaDe(item);
            var atual = _raiz;

            foreach (var direcao in sequencia)
            {
                atual.Filhos ??= [];

                if (!atual.Filhos.TryGetValue(direcao, out var proximo))
                {
                    proximo = new No();
                    atual.Filhos[direcao] = proximo;
                }

                atual = proximo;
            }

            if (atual.TemItem)
            {
                // `!`: TemItem só fica true junto com a atribuição de Item, logo abaixo.
                var nomeAntigo = Descrever(atual.Item!, descreverPara);
                var nomeNovo = Descrever(item, descreverPara);
                throw new InvalidOperationException(
                    $"'{nomeAntigo}' e '{nomeNovo}' declaram a mesma sequência de comando "
                    + $"({string.Join(" ", sequencia)}). Sequências têm que ser únicas.");
            }

            atual.Item = item;
            atual.TemItem = true;
        }
    }

    /// <summary>Match exato no nó atual da trie. Nulo se não há item ali.</summary>
    public T? Resolve(ReadOnlySpan<CommandDirection> sequencia)
    {
        var no = Navegar(sequencia);
        return no is { TemItem: true } ? no.Item : null;
    }

    /// <summary>Itens cujo prefixo é <paramref name="sequencia"/> — alimenta o HUD ao vivo.</summary>
    public IReadOnlyList<T> Candidates(ReadOnlySpan<CommandDirection> sequencia)
    {
        var no = Navegar(sequencia);
        if (no is null)
            return [];

        List<T> resultado = [];
        Coletar(no, resultado);
        return resultado;
    }

    private No? Navegar(ReadOnlySpan<CommandDirection> sequencia)
    {
        var atual = _raiz;

        foreach (var direcao in sequencia)
        {
            if (atual.Filhos is null || !atual.Filhos.TryGetValue(direcao, out var proximo))
                return null;

            atual = proximo;
        }

        return atual;
    }

    private static void Coletar(No no, List<T> destino)
    {
        // `!`: mesma garantia do construtor -- TemItem e Item andam juntos.
        if (no.TemItem)
            destino.Add(no.Item!);

        if (no.Filhos is null)
            return;

        foreach (var filho in no.Filhos.Values)
            Coletar(filho, destino);
    }

    private static string Descrever(T item, Func<T, string>? descreverPara)
        => descreverPara?.Invoke(item) ?? item.ToString() ?? "?";
}
