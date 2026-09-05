using Contenda.Components.Movement;
using Contenda.Components.Targeting;
using Contenda.Core;
using Godot;

namespace Contenda.Characters.Base;

/// <summary>
/// O que um componente pode alcançar dentro do próprio personagem.
/// </summary>
/// <remarks>
/// Substitui o <c>GetNode("../Outro")</c>: dependência explícita, tipada, e que
/// quebra na compilação em vez de em runtime.
///
/// Vários campos são nulos por enquanto — vida, mana, combate e habilidades
/// chegam do M2 em diante. Consumo sempre defensivo: um inimigo simples nunca
/// terá <c>Transformations</c>, e isso é normal, não erro.
/// </remarks>
public sealed class CharacterContext
{
    public CharacterContext(CharacterController dono, CharacterBody3D corpo, Team time)
    {
        Owner = dono;
        Body = corpo;
        Team = time;
    }

    /// <summary>O contêiner que reúne os componentes.</summary>
    public CharacterController Owner { get; }

    /// <summary>O corpo físico. Quem chama <c>MoveAndSlide</c> é o movimento.</summary>
    public CharacterBody3D Body { get; }

    /// <summary>De que lado este personagem está. Decide quem pode ferir quem.</summary>
    public Team Team { get; }

    /// <summary>Locomoção. Consome intenção, venha ela do teclado ou da IA.</summary>
    public MovementComponent? Movement { get; internal set; }

    /// <summary>Para onde o personagem olha e mira.</summary>
    public TargetingComponent? Targeting { get; internal set; }
}
