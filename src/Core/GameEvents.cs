using System;
using Godot;

namespace Contenda.Core;

/// <summary>
/// Um golpe conectou e precisa de um número flutuante — em quem, quanto e se
/// foi crítico.
/// </summary>
/// <param name="Position">Onde mostrar o número, no mundo.</param>
/// <param name="Amount">Quanto de dano, já com mitigação aplicada.</param>
/// <param name="IsCritical">Se foi crítico. Sempre falso até o ticket 16.</param>
public readonly record struct DamageNumberEvent(Vector3 Position, float Amount, bool IsCritical);

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

    public override void _Ready()
    {
        ServiceLocator.Register(this);
        GD.Print("[boot] GameEvents pronto");
    }

    /// <summary>Dispara <see cref="DamageNumberRequested"/>.</summary>
    public void RaiseDamageNumber(in DamageNumberEvent evento) => DamageNumberRequested?.Invoke(evento);
}
