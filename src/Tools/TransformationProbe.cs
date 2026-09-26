using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.Transformations;
using Contenda.Components.Health;
using Contenda.Core;
using Godot;

namespace Contenda.Tools;

/// <summary>Verifica a fiação das formas numa partida e antes de emitir morte.</summary>
public sealed partial class TransformationProbe : Node
{
    private static readonly Vector3 StartPosition = new(13f, 0f, 6f);
    [Export] public NodePath ImpactDummyPath { get; set; } = new("Manequim1");
    [Export] public NodePath EdgeDummyPath { get; set; } = new("Manequim2");

    private readonly List<string> _failures = [];
    private CharacterController? _player;
    private CharacterController? _impactDummy;
    private CharacterController? _edgeDummy;
    private DamageInfo? _impactHit;
    private DamageInfo? _edgeHit;
    private ProjectileFireEvent? _lastShot;
    private bool _deathObservedActiveForm = true;
    private bool _manaDepletedReverted;
    private bool _finished;
    private int _frame;

    public override void _Ready()
    {
        var arena = GD.Load<PackedScene>("res://scenes/arena/Arena.tscn");
        if (arena is null)
        {
            Fail("Arena.tscn não carregou.");
            Finish();
            return;
        }

        var arenaNode = arena.Instantiate();
        AddChild(arenaNode);
        _impactDummy = arenaNode.GetNodeOrNull<CharacterController>(ImpactDummyPath);
        _edgeDummy = arenaNode.GetNodeOrNull<CharacterController>(EdgeDummyPath);
        if (_impactDummy?.Context?.Health is null || _edgeDummy?.Context?.Health is null)
        {
            Fail("Manequins do teste de explosão não foram encontrados.");
            Finish();
            return;
        }

        _player = GetTree().GetFirstNodeInGroup(NodeGroups.Player) as CharacterController;
        if (_player?.Context is null)
        {
            Fail("Personagem de teste não foi encontrado.");
            Finish();
            return;
        }

        _player.GlobalPosition = StartPosition;
    }

    public override void _PhysicsProcess(double delta)
    {
        _frame++;
        if (_frame == 12)
            VerificarBerserker();
        else if (_frame == 90)
            ReverterBerserker();
        else if (_frame == 100)
            AtivarOverdrive();
        else if (_frame == 130)
            VerificarImpactoDoCanhao();
        else if (_frame == 140)
            VerificarTiroNoEspacoVazio();
        else if (_frame == 190)
            VerificarReversaoAntesDaMorte();
        else if (_frame == 195)
            Finish();
    }

    private void VerificarBerserker()
    {
        // _Ready só avança quando player/contexto existem; os templates de jogador fornecem estes componentes.
        var context = _player!.Context!;
        var forms = context.Transformations!;
        forms.SelectNext();
        Verificar(forms.TryActivateSelected(), "Berserker não ativou.");
        Verificar(forms.Active?.Id == new StringName("berserker"), "A forma ativa não é Berserker.");
        Verificar(context.Combat!.EquippedWeapon.Id == new StringName("weapon.sword_berserker"), "Berserker não trocou a arma.");
        Verificar(context.Movement!.ExtraAirJumps == 1, "Berserker não adicionou o pulo aéreo.");
        Verificar(Mathf.IsEqualApprox(context.Stats!.Get(Contenda.Components.Stats.StatId.CritChance), 0.45f), "Berserker não levou o crítico a 45%.");
        Verificar(Mathf.IsEqualApprox(context.Stats.Get(Contenda.Components.Stats.StatId.DamageMultiplier), 1.6f), "Berserker não aplicou o dano ×1,6.");
    }

    private void ReverterBerserker()
    {
        // _Ready só avança quando player/contexto existem; os templates de jogador fornecem estes componentes.
        var context = _player!.Context!;
        var forms = context.Transformations!;
        forms.Revert(RevertReason.Manual);
        Verificar(forms.Active is null, "Berserker não reverteu.");
        Verificar(context.Combat!.EquippedWeapon.Id == new StringName("weapon.sword"), "Reverter Berserker não devolveu a espada base.");
        Verificar(context.Movement!.ExtraAirJumps == 0, "Reverter Berserker não removeu o pulo extra.");
        Verificar(context.Stats!.Get(Contenda.Components.Stats.StatId.DamageMultiplier) == 1f, "Dano não voltou exatamente à base.");

        var berserker = forms.Available[forms.SelectedIndex - 1];
        var manaRestante = berserker.ManaActivationCost + (berserker.ManaDrainPerSecond * 0.25f);
        Verificar(context.Mana!.TryConsume(context.Mana.Current - manaRestante), "Não foi possível preparar a reserva de mana do teste.");
        forms.Reverted += AoReverterForma;
        Verificar(forms.TryActivateSelected(), "Berserker não reativou para o teste de esgotamento de mana.");
        forms.Tick(0.25f, default);
        forms.Reverted -= AoReverterForma;
        Verificar(_manaDepletedReverted, "Zerar mana não reverteu a transformação pelo motivo correto.");
        Verificar(forms.Active is null, "A forma continuou ativa depois de zerar mana.");
        Verificar(context.Stats.Get(Contenda.Components.Stats.StatId.DamageMultiplier) == 1f, "O dano da forma permaneceu após zerar mana.");
    }

    private void AoReverterForma(TransformationDefinition _, RevertReason reason) =>
        _manaDepletedReverted = reason == RevertReason.ManaDepleted;

    private void AtivarOverdrive()
    {
        var gunslinger = GD.Load<CharacterDefinition>("res://data/characters/gunslinger.tres");
        if (gunslinger is null)
        {
            Fail("Definição Gunslinger não carregou.");
            return;
        }

        // _Ready validou o jogador e Gunslinger contém os componentes usados abaixo.
        _player!.SwitchDefinition(gunslinger);
        var context = _player.Context!;
        var forms = context.Transformations!;
        forms.SelectNext();
        Verificar(forms.TryActivateSelected(), "Overdrive não ativou.");
        var cannon = context.Combat!.EquippedWeapon;
        Verificar(cannon.Id == new StringName("weapon.arm_cannon"), "Overdrive não equipou o braço-canhão.");
        Verificar(cannon.ExplosionRadius == 2.5f && cannon.EdgeDamageMultiplier == 0.6f, "Braço-canhão não tem explosão e falloff previstos.");
        Verificar(cannon.InfiniteAmmo && cannon.AttackInterval > 0.3f, "Braço-canhão recarrega ou dispara mais rápido que o revólver.");
        Verificar(context.Transformations!.CannonVisible, "A representação visual do canhão não apareceu.");

        // Um impacto direto e um alvo a 2 m revelam se a explosão começa cedo demais.
        _impactDummy!.GlobalPosition = _player.GlobalPosition + new Vector3(0f, 0f, -8f);
        _edgeDummy!.GlobalPosition = _impactDummy.GlobalPosition + new Vector3(2f, 0f, 0f);
        _impactDummy.Context!.Health!.Damaged += AoAcertarCentro;
        _edgeDummy.Context!.Health!.Damaged += AoAcertarBorda;
        var pontoDeMira = _impactDummy.GlobalPosition + Vector3.Up;
        context.Targeting!.SetAim(pontoDeMira, Vector3.Forward, true);
        context.Combat.RequestBasicAttack();
    }

    private void VerificarImpactoDoCanhao()
    {
        Verificar(_impactHit is not null, "O projétil não atingiu o alvo direto.");
        Verificar(_edgeHit is not null, "A explosão não atingiu o alvo vizinho.");
        if (_impactHit is not { } centro || _edgeHit is not { } borda || _player?.Context?.Stats is not { } stats)
            return;

        var arma = _player.Context!.Combat!.EquippedWeapon;
        var critico = centro.IsCritical ? stats.Get(Contenda.Components.Stats.StatId.CritMultiplier) : 1f;
        var danoDireto = arma.BaseDamage * stats.Get(Contenda.Components.Stats.StatId.DamageMultiplier) * critico;
        Verificar(Mathf.IsEqualApprox(centro.Amount, danoDireto), "O alvo direto não recebeu o dano integral do canhão.");
        var danoBorda = danoDireto * Contenda.Weapons.ExplosionMath.DamageMultiplier(2f, arma.ExplosionRadius, arma.EdgeDamageMultiplier);
        Verificar(Mathf.IsEqualApprox(borda.Amount, danoBorda), "O alvo vizinho não recebeu o falloff linear previsto.");
    }

    private void VerificarTiroNoEspacoVazio()
    {
        var context = _player!.Context!;
        var origem = _player.GlobalPosition;
        var pontoDeMira = origem + new Vector3(4f, 1f, 0f);
        context.Targeting!.SetAim(pontoDeMira, Vector3.Right, true);
        ServiceLocator.Events.ProjectileFireRequested += AoDispararCanhao;
        context.Combat!.RequestBasicAttack();
        ServiceLocator.Events.ProjectileFireRequested -= AoDispararCanhao;

        Verificar(_lastShot is { } shot && Mathf.IsEqualApprox(shot.LifeTime * shot.Speed, 4f),
            "O tiro no espaço vazio não termina no ponto escolhido.");
    }

    private void AoAcertarCentro(DamageInfo info) => _impactHit ??= info;
    private void AoAcertarBorda(DamageInfo info) => _edgeHit ??= info;
    private void AoDispararCanhao(ProjectileFireEvent info) => _lastShot = info;

    private void VerificarReversaoAntesDaMorte()
    {
        // _Ready só avança quando player/contexto existem; os templates de jogador fornecem estes componentes.
        var context = _player!.Context!;
        context.Health!.Died += AoObservarMorte;
        context.Health.Kill("probe.forma");
        context.Health.ResolveQueue(0f);
        Verificar(!_deathObservedActiveForm, "O evento de morte observou a forma ainda ativa.");
        Verificar(context.Combat!.EquippedWeapon.Id == new StringName("weapon.revolver"), "A morte deixou o braço-canhão equipado.");
        Verificar(context.Transformations!.CannonVisible is false, "A morte deixou a representação do canhão visível.");
    }

    private void AoObservarMorte(DamageInfo _) =>
        _deathObservedActiveForm = _player?.Context?.Transformations?.Active is not null;

    private void Verificar(bool condition, string message)
    {
        if (!condition)
            Fail(message);
    }

    private void Fail(string message)
    {
        _failures.Add(message);
        GD.PrintErr("[transformacao] FALHA: " + message);
    }

    private void Finish()
    {
        if (_finished)
            return;

        _finished = true;
        if (_player?.Context?.Health is { } health)
            health.Died -= AoObservarMorte;
        if (_impactDummy?.Context?.Health is { } centerHealth)
            centerHealth.Damaged -= AoAcertarCentro;
        if (_edgeDummy?.Context?.Health is { } edgeHealth)
            edgeHealth.Damaged -= AoAcertarBorda;

        if (_failures.Count == 0)
            GD.Print("[transformacao] PASSOU: Berserker, Overdrive, troca de arma e reversão antes da morte.");
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }
}
