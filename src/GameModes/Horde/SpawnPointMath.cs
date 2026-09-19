using System.Collections.Generic;

namespace Contenda.GameModes.Horde;

/// <summary>
/// Escolha de ponto de spawn, sem nenhum nó envolvido.
/// </summary>
/// <remarks>
/// Separada de <c>SpawnDirector</c> pelo mesmo motivo de
/// <c>MovementMath</c>/<c>CritMath</c>: testável fora da engine. O sorteio em
/// si (<c>GD.Randf()</c>) fica do lado de fora, um número já sorteado entra
/// como parâmetro -- mesma disciplina de <c>CritMath.Rolar</c>. Distância e
/// frustum de câmera não entram aqui: dependem de um <c>Camera3D</c> de
/// verdade, e o próprio <c>SpawnDirector</c> já reduz isso a uma
/// lista de pesos antes de chamar <see cref="ChooseWeightedIndex"/>.
/// </remarks>
public static class SpawnPointMath
{
    /// <summary>Se a distância entre dois pontos cai dentro de [mínimo, máximo].</summary>
    public static bool IsDistanceValid(float distancia, float minimo, float maximo)
        => distancia >= minimo && distancia <= maximo;

    /// <summary>
    /// Sorteia um índice entre os pesos dados -- peso maior, mais chance.
    /// </summary>
    /// <remarks>
    /// Usado para "aleatório entre os candidatos, com peso menor para o
    /// ponto usado por último" (spec 10 §6): o chamador monta os pesos (1,0
    /// para a maioria, um valor menor para o índice do último usado) e só
    /// pede o sorteio aqui.
    /// </remarks>
    /// <param name="pesos">Um peso por candidato, na mesma ordem dos candidatos.</param>
    /// <param name="sorteio01">Um número em [0, 1), já sorteado por quem chama.</param>
    public static int ChooseWeightedIndex(IReadOnlyList<float> pesos, float sorteio01)
    {
        var total = 0f;
        for (var i = 0; i < pesos.Count; i++)
            total += pesos[i];

        if (total <= 0f)
            return 0;

        var alvo = sorteio01 * total;
        var acumulado = 0f;

        for (var i = 0; i < pesos.Count; i++)
        {
            acumulado += pesos[i];
            if (alvo < acumulado)
                return i;
        }

        // Só chega aqui por imprecisão de ponto flutuante bem na borda
        // (sorteio01 quase 1,0) -- o último candidato é a escolha certa.
        return pesos.Count - 1;
    }
}
