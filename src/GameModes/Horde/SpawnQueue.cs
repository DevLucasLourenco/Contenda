using System.Collections.Generic;
using Godot;

namespace Contenda.GameModes.Horde;

/// <summary>
/// Entre várias entradas concorrentes de uma onda, quem nasce a seguir, sem
/// nó nem <see cref="Contenda.Components.AI.EnemyDefinition"/> nenhum
/// envolvido.
/// </summary>
/// <remarks>
/// Opera só em ÍNDICES (posição da entrada no array <c>WaveDefinition.Entries</c>
/// original), nunca no `Resource` em si -- mesmo motivo de
/// <c>SpawnPointMath</c>: testável fora da engine, sem precisar instanciar
/// um `EnemyDefinition` de verdade num teste xUnit. Quem traduz o índice de
/// volta para a definição real é o <c>WaveDirector</c>, na fronteira com a
/// engine.
///
/// Reaproveita <see cref="SpawnPointMath.ChooseWeightedIndex"/> para o
/// sorteio ponderado -- "peso na ordem de spawn" (spec 10 §3,
/// `EnemySpawnEntry.Weight`) é a MESMA forma de problema que "peso menor
/// para o ponto de spawn usado por último" (spec 10 §6), só que entre
/// entradas em vez de entre pontos no chão.
/// </remarks>
public sealed class SpawnQueue
{
    private sealed class Entrada
    {
        public required float Weight { get; init; }
        public float DelayRemaining { get; set; }
        public int Remaining { get; set; }
    }

    private readonly List<Entrada> _entradas;

    /// <param name="entradas">Uma por índice de <c>WaveDefinition.Entries</c>, na mesma ordem.</param>
    public SpawnQueue(IReadOnlyList<(int Count, float Weight, float DelayBeforeFirst)> entradas)
    {
        _entradas = new List<Entrada>(entradas.Count);
        foreach (var (count, weight, delay) in entradas)
            _entradas.Add(new Entrada { Weight = weight, DelayRemaining = delay, Remaining = count });
    }

    /// <summary>Se todas as entradas já esgotaram a própria contagem.</summary>
    public bool IsEmpty
    {
        get
        {
            foreach (var entrada in _entradas)
            {
                if (entrada.Remaining > 0)
                    return false;
            }

            return true;
        }
    }

    /// <summary>Envelhece o atraso inicial de cada entrada pelo quadro.</summary>
    public void Tick(float delta)
    {
        foreach (var entrada in _entradas)
            entrada.DelayRemaining = Mathf.Max(0f, entrada.DelayRemaining - delta);
    }

    /// <summary>
    /// Sorteia, entre as entradas ELEGÍVEIS agora (atraso zerado, contagem
    /// ainda de sobra), qual nasce.
    /// </summary>
    /// <param name="sorteio01">Um número em [0, 1), já sorteado por quem chama.</param>
    /// <returns>O índice (em <c>WaveDefinition.Entries</c>) escolhido, ou -1 se nenhuma estiver pronta ainda.</returns>
    public int ChooseNext(float sorteio01)
    {
        var indicesElegiveis = new List<int>();
        var pesos = new List<float>();

        for (var i = 0; i < _entradas.Count; i++)
        {
            if (_entradas[i].Remaining <= 0 || _entradas[i].DelayRemaining > 0f)
                continue;

            indicesElegiveis.Add(i);
            pesos.Add(_entradas[i].Weight);
        }

        if (indicesElegiveis.Count == 0)
            return -1;

        return indicesElegiveis[SpawnPointMath.ChooseWeightedIndex(pesos, sorteio01)];
    }

    /// <summary>Consome um nascimento da entrada dada -- chamado depois de <see cref="ChooseNext"/> pedir de verdade.</summary>
    public void Consume(int indice) => _entradas[indice].Remaining--;
}
