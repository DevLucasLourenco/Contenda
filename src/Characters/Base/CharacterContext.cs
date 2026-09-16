using Contenda.Components.Abilities;
using Contenda.Components.AI;
using Contenda.Components.Combat;
using Contenda.Components.Health;
using Contenda.Components.Mana;
using Contenda.Components.Movement;
using Contenda.Components.Stats;
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

    /// <summary>Atributos. Base para dano, defesa, velocidade e o resto.</summary>
    public StatsComponent? Stats { get; internal set; }

    /// <summary>Vida. Enfileira golpes e resolve num ponto único do quadro.</summary>
    public HealthComponent? Health { get; internal set; }

    /// <summary>Mana. Nulo para quem não usa mana (inimigo simples).</summary>
    public ManaComponent? Mana { get; internal set; }

    /// <summary>Combate. Executa o ataque básico com a arma equipada.</summary>
    public CombatComponent? Combat { get; internal set; }

    /// <summary>Locomoção. Consome intenção, venha ela do teclado ou da IA.</summary>
    public MovementComponent? Movement { get; internal set; }

    /// <summary>Para onde o personagem olha e mira.</summary>
    public TargetingComponent? Targeting { get; internal set; }

    /// <summary>Habilidades. Executa uma sequência de WASD confirmada. Nulo para quem não tem nenhuma.</summary>
    public AbilityComponent? Abilities { get; internal set; }

    /// <summary>Pathing até um ponto do mundo. Nulo para quem não persegue ninguém (o jogador, os manequins).</summary>
    public NavigationMotor? NavigationMotor { get; internal set; }

    /// <summary>Telegrafia visual de golpe. Nulo para quem não avisa antes de bater (o jogador).</summary>
    public AttackTelegraphComponent? AttackTelegraph { get; internal set; }

    /// <summary>
    /// Produz a intenção deste inimigo a partir de percepção e estado. Nulo
    /// para quem não é IA (o jogador).
    /// </summary>
    /// <remarks>
    /// Adicionado no ticket 25: o <c>EnemyPool</c> precisa alcançar o
    /// <see cref="Contenda.Components.AI.EnemyBrain"/> de um inimigo recém-
    /// instanciado para lhe entregar a própria referência (quem devolve este
    /// inimigo ao estoque ao fim da morte) -- sem isto, seria um `GetNode`
    /// por caminho de fora do personagem, proibido pelas convenções §2.
    /// </remarks>
    public EnemyBrain? EnemyBrain { get; internal set; }
}
