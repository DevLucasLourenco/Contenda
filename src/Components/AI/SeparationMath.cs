using System.Collections.Generic;
using Godot;

namespace Contenda.Components.AI;

/// <summary>
/// Força de separação entre inimigos próximos, sem nenhum nó envolvido.
/// </summary>
/// <remarks>
/// Ver docs/specs/09-inimigos-e-ia.md §4: "Além da avoidance do agente, uma
/// força de separação leve entre inimigos a menos de 1.2 m, com peso 0.35.
/// Sem isso, a horda vira uma bola de corpos sobrepostos e o combate melee
/// fica ilegível." Separada de <see cref="NavigationMotor"/>/<see cref="EnemyBrain"/>
/// pelo mesmo motivo de <c>MovementMath</c>/<c>CritMath</c>: testável fora da
/// engine, sem <see cref="NavigationAgent3D"/> nem árvore de nós nenhuma.
/// </remarks>
public static class SeparationMath
{
    /// <summary>
    /// Soma um empurrão para longe de cada vizinho dentro do raio, mais forte
    /// quanto mais perto -- não um valor fixo por vizinho, senão dois
    /// inimigos exatamente colados receberiam o mesmo empurrão que dois só
    /// raspando a borda do raio, e a "bola de corpos" nunca se abriria de
    /// verdade.
    /// </summary>
    /// <remarks>
    /// Achatada no plano (Y sempre zero na saída): o resultado alimenta
    /// <c>IntentFrame.Move</c>, que é puramente horizontal, mesmo motivo de
    /// <see cref="NavigationMotor.GetDesiredDirection"/>.
    ///
    /// <c>List&lt;Vector3&gt;</c> concreto, não <c>IEnumerable&lt;Vector3&gt;</c>: chamado
    /// a cada quadro por inimigo em `Chase` (convenções §5/spec 15 §3, zero
    /// alocação no hot path) -- iterar por trás de uma interface faria o
    /// enumerador de `List&lt;T&gt;` (um struct) ser boxed a cada chamada.
    /// </remarks>
    public static Vector3 ComputeForce(Vector3 posicao, List<Vector3> vizinhos, float raio, float peso)
    {
        var soma = Vector3.Zero;

        // Índice, não `foreach`, pelo mesmo motivo do tipo do parâmetro:
        // acessar por índice não passa nem pelo enumerador de `List&lt;T&gt;`.
        for (var i = 0; i < vizinhos.Count; i++)
        {
            var vizinho = vizinhos[i];
            var delta = new Vector3(posicao.X - vizinho.X, 0f, posicao.Z - vizinho.Z);
            var distancia = delta.Length();

            if (distancia <= 0.0001f || distancia >= raio)
                continue;

            // (raio - distancia) / raio: 1.0 quando colados, tendendo a 0 na
            // borda do raio -- um empurrão que se dissolve suavemente em vez
            // de ligar/desligar de repente ao cruzar o limite.
            var intensidade = (raio - distancia) / raio;
            soma += delta.Normalized() * intensidade;
        }

        return soma * peso;
    }
}
