using System;
using Contenda.Characters.Base;
using Contenda.Components.Health;
using Contenda.Components.Stats;
using Contenda.Core;
using Godot;

namespace Contenda.Weapons;

/// <summary>
/// Tiro instantâneo: acerta onde o cursor aponta, gasta munição e recarrega
/// sozinho ao esvaziar.
/// </summary>
/// <remarks>
/// Nasceu depois do <see cref="MeleeWeapon"/>, provando o mesmo ponto do
/// ticket 08: o <c>CombatComponent</c> não sabe que existe. Ver ticket 09 e
/// spec 07 §5.
///
/// Como o corpo a corpo, não usa hurtbox física: o acerto é geometria contra a
/// posição do alvo (<see cref="HitscanMath"/>), não colisão de camadas — o
/// projeto ainda não tem <c>Area3D</c> de dano em lugar nenhum.
/// </remarks>
public sealed class HitscanWeapon : IWeapon
{
    /// <summary>Tolerância lateral para contar como acerto — raio da cápsula do personagem mais folga.</summary>
    private const float HitRadius = 0.5f;

    private const float TracerLifetime = 0.06f;
    private const float TracerThickness = 0.02f;

    private readonly WeaponDefinition _arma;
    private readonly CharacterContext _contexto;
    private readonly Node _dono;
    private readonly StringName _targetGroup;
    private readonly float _verticalReach;
    private readonly RevolverState _estado;

    public HitscanWeapon(
        WeaponDefinition arma,
        CharacterContext contexto,
        Node dono,
        StringName targetGroup,
        float verticalReach)
    {
        ArgumentNullException.ThrowIfNull(arma);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(dono);

        _arma = arma;
        _contexto = contexto;
        _dono = dono;
        _targetGroup = targetGroup;
        _verticalReach = verticalReach;
        _estado = new RevolverState(arma.MagazineSize, arma.ReloadTime);
    }

    /// <remarks>
    /// Sempre falso, de propósito: um tiro é instantâneo, sem wind-up para
    /// travar. Diferente do corpo a corpo, atirar não deveria imobilizar quem
    /// atira — nada na spec 07 §5 ou no ticket 09 pede isso, e um revólver que
    /// prende o jogador no lugar a cada disparo jogaria mal. É a mesma
    /// <see cref="Components.Combat.ActionLockSet"/> do ticket 08 recusando
    /// travar sozinha quando <c>IsAttacking</c> nunca fica verdadeiro.
    /// </remarks>
    public bool IsAttacking => false;

    /// <summary>Sempre zero: hitscan não tem cadeia de combo.</summary>
    public int ComboStep => 0;

    public event Action<int>? AttackStarted;
    public event Action<Node3D>? HitLanded;

    public void RequestBasicAttack() => TentarDisparo();

    /// <summary>Segurar o botão mantém a cadência — spec 07 §5.</summary>
    public void Tick(float delta, bool triggerHeld)
    {
        _estado.Advance(delta);

        if (triggerHeld)
            TentarDisparo();
    }

    /// <remarks>
    /// Não interrompe recarga em andamento: a spec 07 §5 só torna a recarga
    /// cancelável por uma habilidade específica, nunca por morte, atordoamento
    /// ou o resto do que <see cref="Cancel"/> genericamente representa — e não
    /// há disparo "em voo" para interromper, já que o tiro resolve na hora.
    /// </remarks>
    public void Cancel()
    {
    }

    public void ResetForSpawn() => _estado.ResetForSpawn();

    /// <remarks>
    /// Sem mira válida (cursor além do horizonte, ou o primeiro quadro antes
    /// de qualquer projeção), o gatilho não engata: não há direção para
    /// gastar o cartucho.
    /// </remarks>
    private void TentarDisparo()
    {
        if (_contexto.Targeting is not { HasAim: true } mira)
            return;

        var cadenciaBase = _arma.AttackInterval;
        var attackSpeed = Mathf.Max(0.01f, _contexto.Stats?.Get(StatId.AttackSpeed) ?? 1f);
        if (!_estado.TryFire(cadenciaBase / attackSpeed))
            return;

        AttackStarted?.Invoke(1);
        Disparar(mira.AimPoint);
    }

    private void Disparar(Vector3 aimPoint)
    {
        var corpo = _contexto.Body;

        // O plano de mira já fica na altura do torso (TargetingComponent); usar
        // a mesma altura para a origem do tiro mantém a trajetória horizontal,
        // sem queda de projétil -- spec 07 §5.
        var origem = new Vector3(corpo.GlobalPosition.X, aimPoint.Y, corpo.GlobalPosition.Z);
        var ate = aimPoint - origem;

        // Mira degenerada (o alvo projetado cai em cima do cano): usa a frente
        // do corpo em vez de uma direção de comprimento zero.
        var direcaoBase = ate.LengthSquared() > 0.0001f
            ? ate.Normalized()
            : -corpo.GlobalTransform.Basis.Z;

        var direcao = AplicarDispersao(direcaoBase);
        var alcanceEfetivo = AlcanceAteParede(origem, direcao);
        var alvo = EncontrarAlvo(origem, direcao, alcanceEfetivo);
        var destino = origem + (direcao * alcanceEfetivo);

        if (alvo is not null)
        {
            destino = alvo.GlobalPosition;

            // Sorteado aqui, uma vez por tiro -- um hitscan só atinge um
            // alvo por disparo, então não existe o problema de "loteria" do
            // corpo a corpo em área. Ticket 18, spec 16 §5.
            var critico = CritMath.RolarNaStats(_contexto.Stats);
            var dano = CritMath.AplicarNaStats(
                _contexto.Stats,
                _arma.BaseDamage * (_contexto.Stats?.Get(StatId.DamageMultiplier) ?? 1f),
                critico);

            alvo.Context?.Health?.ApplyDamage(new DamageInfo(
                Amount: dano,
                Type: DamageType.Physical,
                HitPoint: destino,
                Direction: direcao,
                Knockback: _arma.Knockback,
                SourceId: corpo.GetInstanceId(),
                SourceTag: _arma.Id,
                IsCritical: critico));

            alvo.Context?.Movement?.ApplyKnockback(direcao * _arma.Knockback);

            // Hitstop nos dois envolvidos -- ticket 11. Sem cadeia de combo
            // aqui, então é sempre a mesma duração; nenhum "finalizador" para
            // o revólver -- exceto o bônus de crítico, ticket 18.
            var hitstop = _arma.HitstopSeconds + (critico ? _arma.CriticalHitstopBonus : 0f);
            _contexto.Health?.ApplyHitstop(hitstop);
            alvo.Context?.Health?.ApplyHitstop(hitstop);

            HitLanded?.Invoke(alvo);
        }

        DesenharRastro(origem, destino);
    }

    /// <summary>Dispersão aleatória em torno de Y — a mira é sempre horizontal.</summary>
    private Vector3 AplicarDispersao(Vector3 direcaoBase)
    {
        if (_arma.SpreadDegrees <= 0f)
            return direcaoBase;

        var anguloGraus = (float)GD.RandRange(-_arma.SpreadDegrees, _arma.SpreadDegrees);
        return direcaoBase.Rotated(Vector3.Up, Mathf.DegToRad(anguloGraus));
    }

    /// <summary>
    /// Até onde o tiro alcança antes de bater em parede.
    /// </summary>
    /// <remarks>
    /// Raycast real contra <see cref="PhysicsLayers.World"/> — a única parte
    /// do acerto que usa física de verdade. Corpos de personagem não entram
    /// nesta máscara de propósito: quem eles atingem é resolvido à parte, por
    /// <see cref="HitscanMath"/>, igual ao corpo a corpo do ticket 08.
    /// </remarks>
    private float AlcanceAteParede(Vector3 origem, Vector3 direcao)
    {
        var espaco = _dono.GetViewport().World3D.DirectSpaceState;
        var parametros = PhysicsRayQueryParameters3D.Create(
            origem, origem + (direcao * _arma.Range), PhysicsLayers.World);

        // Fronteira com a engine: Godot.Collections.Dictionary só aqui, e só
        // uma vez por tiro -- a cadência mínima de uma arma é 0,05 s.
        var resultado = espaco.IntersectRay(parametros);
        if (resultado.Count == 0)
            return _arma.Range;

        var ponto = resultado["position"].AsVector3();
        return origem.DistanceTo(ponto);
    }

    /// <summary>O alvo mais próximo do atirador que o segmento do tiro alcança.</summary>
    private CharacterController? EncontrarAlvo(Vector3 origem, Vector3 direcao, float alcanceEfetivo)
    {
        var corpo = _contexto.Body;
        CharacterController? melhor = null;
        var melhorDistancia = float.MaxValue;

        // Fronteira com a engine: mesma varredura por grupo do MeleeWeapon,
        // uma vez por tiro -- não por quadro.
        foreach (var no in _dono.GetTree().GetNodesInGroup(_targetGroup))
        {
            if (no is not CharacterController alvo || alvo == corpo || !GodotObject.IsInstanceValid(alvo))
                continue;

            if (alvo.Team == _contexto.Team)
                continue;

            var vida = alvo.Context?.Health;
            if (vida is null || !vida.IsAlive)
                continue;

            if (Mathf.Abs(alvo.GlobalPosition.Y - origem.Y) > _verticalReach)
                continue;

            if (!HitscanMath.TryHitSegment(origem, direcao, alcanceEfetivo, alvo.GlobalPosition, HitRadius, out var distancia))
                continue;

            if (distancia >= melhorDistancia)
                continue;

            melhorDistancia = distancia;
            melhor = alvo;
        }

        return melhor;
    }

    /// <remarks>
    /// Placeholder deliberado: VFX pooled é o ticket 36. Um <c>MeshInstance3D</c>
    /// novo por tiro é aceitável na cadência de um revólver (mínimo 0,05 s
    /// entre disparos); não seria numa metralhadora automática.
    /// </remarks>
    private void DesenharRastro(Vector3 origem, Vector3 destino)
    {
        var comprimento = origem.DistanceTo(destino);
        if (comprimento < 0.01f)
            return;

        var rastro = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(TracerThickness, TracerThickness, comprimento) },
        };

        var meio = origem.Lerp(destino, 0.5f);

        // -Z do basis aponta de volta para a origem, então +Z -- o eixo do
        // comprimento do BoxMesh -- aponta para o destino.
        var basis = Basis.LookingAt(origem - destino, Vector3.Up);
        rastro.GlobalTransform = new Transform3D(basis, meio);

        var arvore = _dono.GetTree();
        arvore.Root.AddChild(rastro);
        arvore.CreateTimer(TracerLifetime).Timeout += rastro.QueueFree;
    }
}
