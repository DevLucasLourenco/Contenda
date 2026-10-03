using System;
using System.IO;
using Contenda.Core;
using Contenda.Characters.Base;
using Contenda.Persistence;
using Contenda.UI.Menus;
using Godot;

namespace Contenda.Tools;

/// <summary>Verifica o menu data driven, os previews e a definição entregue à arena.</summary>
public sealed partial class CharacterSelectProbe : Node
{
    public override void _Ready()
    {
        var path = "user://character_select_probe_profile.cfg";
        ServiceLocator.Session.ProfilePath = path;
        var diskPath = ProjectSettings.GlobalizePath(path);

        try
        {
            new ProfileStore(diskPath).RecordMatch("gunslinger", 7890, 4, 12, 90);

            var scene = GD.Load<PackedScene>("res://tests/fixtures/CharacterSelectThree.tscn");
            if (scene is null)
                throw new InvalidOperationException("recursos da seleção ausentes");

            var menu = scene.Instantiate<CharacterSelectMenu>();
            AddChild(menu);
            Check(menu.CardCount == 3, "o terceiro personagem não apareceu ao entrar no elenco");
            Check(CountNodes<SubViewport>(menu) == 3,
                "os cards não possuem preview em SubViewport");
            Check(CountNodes<MeshInstance3D>(menu) >= 3,
                "os previews não instanciaram modelos 3D reais");
            Check(CountNodes<AnimationTree>(menu) == 3,
                "os previews não carregaram os AnimationSets dos personagens");
            Check(CountNodes<BoneAttachment3D>(menu) == 3,
                "as armas não foram presas ao osso da mão nos previews");
            Check(ContainsLabel(menu, "7890") || ContainsLabel(menu, "7.890") || ContainsLabel(menu, "7,890"),
                "o recorde do perfil não apareceu");
            Check(ContainsLabel(menu, "W W") && ContainsLabel(menu, "Deadeye"),
                "as sequências e habilidades não vieram dos dados");
            Check(ContainsLabel(menu, "Berserker:") && ContainsLabel(menu, "Overdrive:"),
                "as transformações não explicam o que alteram");

            menu.Select(1);
            var gunslingerAnimations = menu.SelectedDefinition?.AnimationSet
                ?? throw new InvalidOperationException("AnimationSet da Gunslinger ausente");
            Check(gunslingerAnimations.AnimationForAbility(new StringName("gunslinger.deadeye")) == new StringName("Ranged_1H_Aiming"),
                "a habilidade Deadeye não resolveu para sua animação configurada");
            Check(gunslingerAnimations.AnimationForAbility(new StringName("ability.missing")).IsEmpty,
                "uma habilidade sem associação não retornou animação vazia");

            Check(menu.SelectedDefinition?.Id.ToString() == "gunslinger", "a seleção não mudou");
            QuitAfterSceneChange(GetTree());
            menu.Confirm();
            Check(ServiceLocator.Session.SelectedCharacter?.Id.ToString() == "gunslinger",
                "a confirmação não gravou GameSession.SelectedCharacter");
        }
        catch (Exception exception)
        {
            GD.PushError($"[selecao] FALHOU: {exception}");
            GetTree().Quit(1);
        }
        finally
        {
            if (File.Exists(diskPath)) File.Delete(diskPath);
            if (File.Exists(diskPath + ".bak")) File.Delete(diskPath + ".bak");
            ServiceLocator.Session.ProfilePath = "user://profile.cfg";
        }
    }

    private static bool ContainsLabel(Node root, string value)
    {
        if (root is Label label && label.Text.Contains(value, StringComparison.Ordinal))
            return true;

        foreach (var node in root.GetChildren())
        {
            if (ContainsLabel(node, value))
                return true;
        }
        return false;
    }

    private static int CountNodes<T>(Node root) where T : Node
    {
        var count = root is T ? 1 : 0;
        foreach (var node in root.GetChildren())
            count += CountNodes<T>(node);
        return count;
    }

    private static bool HasAnimationPresentation(CharacterController character)
    {
        if (character.CurrentModel is not { } model
            || character.Definition?.AnimationSet is not { } animationSet
            || character.Context?.Animator?.Tree is not { Active: true } tree)
            return false;

        var animationPlayer = model.GetNodeOrNull<AnimationPlayer>(new NodePath("CharacterAnimationPlayer"));
        var weaponSocket = CharacterPresentation.FindSkeleton(model)?
            .GetNodeOrNull<BoneAttachment3D>(new NodePath("WeaponSocket"));
        return tree.TreeRoot is AnimationNodeBlendTree
            && animationPlayer?.HasAnimation($"motion/{animationSet.Idle}") == true
            && weaponSocket is not null
            && weaponSocket.GetChildCount() > 0;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void QuitAfterSceneChange(SceneTree tree)
    {
        void OnChanged()
        {
            tree.SceneChanged -= OnChanged;
            var player = ServiceLocator.Session.PlayerBody;
            if (player?.Definition?.Id.ToString() != "gunslinger"
                || player.Context?.Abilities?.Abilities.Count != 4
                || player.CurrentModel is null
                || CharacterPresentation.FindSkeleton(player.CurrentModel) is null
                || CharacterPresentation.FindBodyMesh(player.CurrentModel) is null
                || !HasAnimationPresentation(player))
            {
                GD.PushError("[selecao] FALHOU: a arena não montou o Gunslinger escolhido.");
                tree.Quit(1);
                return;
            }

            GD.Print("[selecao] PASSOU: elenco extensível, 3D, ficha, recorde e personagem na arena.");
            tree.Quit();
        }

        tree.SceneChanged += OnChanged;
    }
}
