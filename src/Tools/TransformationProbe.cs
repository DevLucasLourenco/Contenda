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
    private readonly List<string> _failures = [];
    private CharacterController? _player;
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

        AddChild(arena.Instantiate());

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
    }

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

        if (_failures.Count == 0)
            GD.Print("[transformacao] PASSOU: Berserker, Overdrive, troca de arma e reversão antes da morte.");
        GetTree().Quit(_failures.Count == 0 ? 0 : 1);
    }
}
