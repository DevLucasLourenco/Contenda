using Contenda.Characters.Base;
using Godot;

namespace Contenda.Components.AI;

/// <summary>Traduz um ponto do mundo num caminho até ele, em direção de movimento.</summary>
/// <remarks>
/// Ver docs/specs/09-inimigos-e-ia.md §4. **Não move o corpo.** Só devolve a
/// direção do próximo passo do caminho — quem realmente anda é o mesmo
/// <c>MovementComponent</c> do jogador, através do <c>IntentFrame.Move</c>
/// que o <c>EnemyBrain</c> monta a partir daqui. Um único caminho de
/// locomoção no projeto inteiro, exatamente como a spec exige.
///
/// Reaproveita a malha de navegação já existente da arena
/// (<c>data/navmesh/arena_nav.tres</c>, cozida por <c>NavmeshBaker</c>) —
/// um <see cref="NavigationAgent3D"/> encontra sozinho a
/// <see cref="NavigationRegion3D"/> que o envolve, sem precisar de
/// referência explícita nenhuma.
/// </remarks>
public sealed partial class NavigationMotor : Node, ICharacterComponent
{
    /// <summary>
    /// O <see cref="NavigationAgent3D"/> que faz o pathing de verdade.
    /// </summary>
    /// <remarks>
    /// Único `NodePath` que sobra aqui: <see cref="NavigationAgent3D"/> é um
    /// nó nativo da engine, nunca vai implementar <c>ICharacterComponent</c>
    /// nem passar pelo <c>CharacterContext</c> -- quem alcança ESTE Node por
    /// caminho é quem já é dono direto dele. Quem quiser alcançar o
    /// `NavigationMotor` em si, de fora, usa `CharacterContext.NavigationMotor`.
    /// </remarks>
    [Export] public NodePath AgentPath { get; set; } = new();

    /// <summary>Distância do alvo do caminho para considerar "chegou". Ver spec 09 §4.</summary>
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float StoppingDistance { get; set; } = 0.4f;

    private NavigationAgent3D? _agente;

    /// <remarks>
    /// Vazio de propósito: quem me registra em <c>CharacterContext.NavigationMotor</c>
    /// é o <c>Registrar</c> do <c>CharacterController</c>, na MESMA passada
    /// que já resolve <c>Health</c>/<c>Movement</c>/etc -- antes de QUALQUER
    /// <c>Bind</c> rodar. Fazer isso aqui dentro, no próprio <c>Bind</c>,
    /// deixaria a ordem entre os dois `Bind` (deste nó e do que precisa
    /// enxergá-lo) importar, e a doc do container promete que a primeira
    /// passada não tem ordem garantida.
    /// </remarks>
    public void Bind(CharacterContext contexto)
    {
    }

    public void Configure(CharacterDefinition definicao)
    {
    }

    public override void _Ready()
    {
        _agente = GetNodeOrNull<NavigationAgent3D>(AgentPath);
        if (_agente is null)
        {
            GD.PushError($"{Name}: AgentPath não resolveu para um NavigationAgent3D.");
            return;
        }

        _agente.TargetDesiredDistance = StoppingDistance;
    }

    /// <summary>Onde o agente deveria estar tentando chegar.</summary>
    public void SetTarget(Vector3 posicaoNoMundo)
    {
        if (_agente is not null)
            _agente.TargetPosition = posicaoNoMundo;
    }

    /// <summary>
    /// Para onde andar AGORA para seguir o caminho até o alvo mais recente.
    /// </summary>
    /// <remarks>
    /// Achatada no plano: o agente pensa em 3D (rampas, plataformas), mas
    /// quem consome isto é <c>IntentFrame.Move</c>, que é puramente
    /// horizontal -- a altura já é resolvida à parte, pela gravidade do
    /// <c>MovementComponent</c>.
    /// </remarks>
    public Vector3 GetDesiredDirection(Vector3 posicaoAtual)
    {
        if (_agente is null || _agente.IsNavigationFinished())
            return Vector3.Zero;

        var proximoPonto = _agente.GetNextPathPosition();
        var direcao = proximoPonto - posicaoAtual;
        direcao.Y = 0f;

        return direcao.LengthSquared() > 0.0001f ? direcao.Normalized() : Vector3.Zero;
    }
}
