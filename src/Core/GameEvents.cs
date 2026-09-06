using System;
using Godot;

namespace Contenda.Core;

/// <summary>
/// Um golpe conectou e precisa de um número flutuante — em quem, quanto e se
/// foi crítico.
/// </summary>
/// <param name="Position">Onde mostrar o número, no mundo.</param>
/// <param name="Amount">Quanto de dano, já com mitigação aplicada.</param>
/// <param name="IsCritical">Se foi crítico. Ticket 18, spec 16 §5.</param>
public readonly record struct DamageNumberEvent(Vector3 Position, float Amount, bool IsCritical);

/// <summary>Um projétil de habilidade precisa nascer, sem que quem pediu segure o nó.</summary>
/// <param name="Origin">De onde o projétil parte.</param>
/// <param name="Direction">Direção do voo, já normalizada e no plano horizontal.</param>
/// <param name="Speed">Velocidade de voo, em metros por segundo.</param>
/// <param name="LifeTime">Quanto tempo voa antes de se autodestruir sem acertar nada.</param>
/// <param name="Damage">Dano da explosão, já com multiplicadores aplicados.</param>
/// <param name="ExplosionRadius">Raio da explosão — tanto o gatilho de impacto quanto o de área.</param>
/// <param name="MaxTargets">Quantos alvos a explosão atinge. Zero é ilimitado.</param>
/// <param name="Knockback">Repulsão aplicada a cada alvo atingido.</param>
/// <param name="SourceId">Identidade de quem disparou, sem manter o objeto vivo.</param>
/// <param name="SourceTag">O que disparou: `ability.explosive_shot`.</param>
/// <param name="ShooterTeam">Time de quem disparou. Decide quem a explosão pode ferir.</param>
/// <param name="TargetGroup">Grupo varrido em busca de alvos ao explodir.</param>
/// <param name="IsCritical">
/// Se o disparo saiu crítico. Sorteado no disparo, não na explosão -- a
/// explosão em área reaproveita o mesmo valor para todo mundo que ela
/// atinge, mesma disciplina de um golpe corpo a corpo em área. Ticket 18.
/// </param>
public readonly record struct ProjectileFireEvent(
    Vector3 Origin,
    Vector3 Direction,
    float Speed,
    float LifeTime,
    float Damage,
    float ExplosionRadius,
    int MaxTargets,
    float Knockback,
    ulong SourceId,
    string SourceTag,
    Team ShooterTeam,
    StringName TargetGroup,
    bool IsCritical);

/// <summary>
/// Barramento para eventos entre sistemas sem relação direta — o abate de um
/// inimigo chegando ao placar, por exemplo.
/// </summary>
/// <remarks>
/// Regra do projeto: este barramento transporta **apenas dados por valor**
/// (<c>readonly record struct</c>), nunca referências a nós. Um evento que
/// carrega um nó mantém vivo algo que o pool achou que tinha reciclado.
///
/// Comunicação dentro do mesmo personagem NÃO passa por aqui: usa chamada direta
/// pelo <c>CharacterContext</c> ou <c>event</c> do próprio componente.
/// Ver docs/specs/01-arquitetura-tecnica.md §5.
///
/// Os eventos concretos entram conforme os sistemas que os disparam — o de
/// abate chega no M6, com o modo horda. Adicioná-los antes seria abstração para
/// necessidade que a spec ainda não tem. <see cref="DamageNumberEvent"/> é o
/// primeiro: o número flutuante do ticket 11 é pooled e genérico — não sabe
/// (nem devia saber) o que é uma arma, um `CombatComponent` ou um `HealthState`.
/// </remarks>
public sealed partial class GameEvents : Node
{
    /// <summary>Avisa que um golpe conectou em algum lugar do mundo.</summary>
    public event Action<DamageNumberEvent>? DamageNumberRequested;

    /// <summary>Avisa que um projétil de habilidade precisa nascer.</summary>
    public event Action<ProjectileFireEvent>? ProjectileFireRequested;

    public override void _Ready()
    {
        ServiceLocator.Register(this);
        GD.Print("[boot] GameEvents pronto");
    }

    /// <summary>Dispara <see cref="DamageNumberRequested"/>.</summary>
    public void RaiseDamageNumber(in DamageNumberEvent evento) => DamageNumberRequested?.Invoke(evento);

    /// <summary>Dispara <see cref="ProjectileFireRequested"/>.</summary>
    public void RaiseProjectileFire(in ProjectileFireEvent evento) => ProjectileFireRequested?.Invoke(evento);
}
