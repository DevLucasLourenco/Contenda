using Contenda.Characters.Base;
using Contenda.Components.Health;
using Godot;

namespace Contenda.Components.Combat;

/// <summary>
/// Aplica dano em quem estiver por perto ao apertar o ataque básico.
/// </summary>
/// <remarks>
/// **Isto é andaime, não gameplay.** Existe só para tornar o ticket 07
/// demonstrável antes de existirem armas: o ticket 08 traz o `IWeapon` de
/// verdade, com janelas de golpe e área de dano, e este componente sai da cena
/// no mesmo commit.
///
/// Nada aqui deve ser reaproveitado: alcance esférico, dano fixo em código e
/// varredura por grupo são justamente o que a arquitetura evita.
/// </remarks>
public sealed partial class DebugDamageDealer : Node, ICharacterComponent
{
    /// <summary>Alcance da varredura, em metros.</summary>
    [Export(PropertyHint.Range, "1,10,0.5")] public float Range { get; set; } = 3f;

    /// <summary>Dano por golpe.</summary>
    [Export(PropertyHint.Range, "1,100,1")] public float Damage { get; set; } = 20f;

    /// <summary>Grupo dos alvos válidos.</summary>
    [Export] public StringName TargetGroup { get; set; } = new("damageable");

    private CharacterContext? _contexto;

    public void Bind(CharacterContext contexto) => _contexto = contexto;

    public void Configure(CharacterDefinition definicao)
    {
    }

    /// <summary>Golpeia quem estiver no alcance. Chamado pelo contêiner.</summary>
    public void TryStrike()
    {
        if (_contexto is null)
            return;

        var origem = _contexto.Body.GlobalPosition;
        var alcanceQuadrado = Range * Range;

        // GetNodesInGroup devolve Godot.Collections.Array; convenções §2 permitem
        // a exceção na fronteira com a engine. Aceitável aqui por ser andaime de
        // depuração, disparado só ao apertar a tecla — nunca a cada quadro.
        foreach (var no in GetTree().GetNodesInGroup(TargetGroup))
        {
            if (no is not Node3D alvo)
                continue;

            if (alvo == _contexto.Body)
                continue;

            if (origem.DistanceSquaredTo(alvo.GlobalPosition) > alcanceQuadrado)
                continue;

            if (alvo is not CharacterController controlador || controlador.Context?.Health is null)
                continue;

            var direcao = (alvo.GlobalPosition - origem).Normalized();

            controlador.Context.Health.ApplyDamage(new DamageInfo(
                Amount: Damage,
                Type: DamageType.Physical,
                HitPoint: alvo.GlobalPosition,
                Direction: direcao,
                Knockback: 0f,
                SourceId: _contexto.Body.GetInstanceId(),
                SourceTag: "debug.strike",
                IsCritical: false));
        }
    }
}
