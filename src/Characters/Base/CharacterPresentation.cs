using System;
using System.Collections.Generic;
using Contenda.Weapons;
using Godot;

namespace Contenda.Characters.Base;

public readonly record struct AnimationPresentation(AnimationTree Tree, AnimationPlayer Player);

/// <summary>Monta o rig visual reutilizado pela arena e pela seleção.</summary>
public static class CharacterPresentation
{
    private static readonly StringName AnimationLibraryName = new("motion");
    private static readonly StringName AnimationPlayerName = new("CharacterAnimationPlayer");
    private static readonly StringName AnimationTreeName = new("CharacterAnimationTree");

    public static MeshInstance3D? FindBodyMesh(Node root) => FindMesh(root, "_Body");

    public static Skeleton3D? FindSkeleton(Node root)
    {
        if (root is Skeleton3D skeleton)
            return skeleton;

        foreach (var child in root.GetChildren())
        {
            var found = FindSkeleton(child);
            if (found is not null)
                return found;
        }

        return null;
    }

    public static AnimationPresentation? BuildAnimationTree(Node3D model, CharacterAnimationSet? set)
    {
        if (set is null)
            return null;

        var player = new AnimationPlayer { Name = AnimationPlayerName, RootNode = new NodePath("..") };
        var library = new AnimationLibrary();
        var pendingClips = new HashSet<StringName>(set.EnumerateReferencedClips());
        var pendingSnapshot = new List<StringName>();
        foreach (var bankScene in set.AnimationBanks)
        {
            if (pendingClips.Count == 0)
                break;

            var bank = bankScene.Instantiate();
            model.AddChild(bank);
            var bankPlayer = FindAnimationPlayer(bank);
            if (bankPlayer is not null)
            {
                pendingSnapshot.Clear();
                pendingSnapshot.AddRange(pendingClips);
                foreach (var clip in pendingSnapshot)
                {
                    if (!bankPlayer.HasAnimation(clip))
                        continue;

                    var source = bankPlayer.GetAnimation(clip);
                    var animation = (Animation)source.Duplicate(true);
                    RemoveRootMotionTracks(animation);
                    animation.LoopMode = IsLocomotionClip(set, clip)
                        ? Animation.LoopModeEnum.Linear
                        : Animation.LoopModeEnum.None;
                    library.AddAnimation(clip, animation);
                    pendingClips.Remove(clip);
                }
            }

            model.RemoveChild(bank);
            bank.Free();
        }

        if (pendingClips.Count > 0)
            throw new InvalidOperationException($"AnimationSet: bancos sem os clipes: {string.Join(", ", pendingClips)}.");

        player.AddAnimationLibrary(AnimationLibraryName, library);
        model.AddChild(player);

        var root = BuildTreeRoot(set);
        var tree = new AnimationTree
        {
            Name = AnimationTreeName,
            TreeRoot = root,
            AnimPlayer = new NodePath("../CharacterAnimationPlayer"),
            Active = true,
        };
        model.AddChild(tree);
        return new AnimationPresentation(tree, player);
    }

    public static BoneAttachment3D? MountWeapon(
        Node3D model,
        WeaponDefinition? weapon,
        StringName boneName,
        BoneAttachment3D? previousSocket = null)
    {
        var skeleton = FindSkeleton(model);
        RemoveSocket(previousSocket);
        if (skeleton is null || weapon?.ModelScene is not { } weaponScene)
            return null;

        var boneIndex = skeleton.FindBone(boneName);
        if (boneIndex < 0)
            throw new InvalidOperationException($"{model.Name}: o rig não contém o osso de arma '{boneName}'.");

        var socket = new BoneAttachment3D { Name = "WeaponSocket", BoneName = boneName };
        skeleton.AddChild(socket);
        var visual = weaponScene.Instantiate<Node3D>();
        visual.Position = weapon.ModelPositionOffset;
        visual.RotationDegrees = weapon.ModelRotationOffsetDegrees;
        visual.Scale = weapon.ModelScale;
        socket.AddChild(visual);
        return socket;
    }

    private static AnimationNodeBlendTree BuildTreeRoot(CharacterAnimationSet set)
    {
        var tree = new AnimationNodeBlendTree();
        var locomotion = new AnimationNodeBlendSpace1D { MinSpace = 0f, MaxSpace = 1f };
        locomotion.AddBlendPoint(AnimationNode(set.Idle), 0f, -1, "Idle");
        locomotion.AddBlendPoint(AnimationNode(set.Walk), 0.45f, -1, "Walk");
        locomotion.AddBlendPoint(AnimationNode(set.Run), 1f, -1, "Run");
        tree.AddNode("Locomotion", locomotion, Vector2.Zero);

        var previous = new StringName("Locomotion");
        AddOneShot(tree, "Attack", set.MeleeAttacks.Length > 0 ? set.MeleeAttacks[0] : set.Shoot, ref previous);
        AddOneShot(tree, "Ability", FirstAbilityClip(set), ref previous);
        AddOneShot(tree, "Reload", set.Reload, ref previous);
        AddOneShot(tree, "Jump", set.Jump, ref previous);
        AddOneShot(tree, "Fall", set.Fall, ref previous);
        AddOneShot(tree, "Dive", set.Dive, ref previous);
        AddOneShot(tree, "Land", set.Land, ref previous);
        AddOneShot(tree, "Dash", set.Dash, ref previous);
        AddOneShot(tree, "Hit", set.Hit, ref previous);
        AddOneShot(tree, "Transform", set.Transform, ref previous);
        AddOneShot(tree, "Death", set.Death, ref previous);
        tree.ConnectNode("output", 0, previous);
        return tree;
    }

    private static void AddOneShot(AnimationNodeBlendTree tree, StringName name, StringName clip, ref StringName previous)
    {
        if (clip.IsEmpty)
            return;

        tree.AddNode(name, new AnimationNodeOneShot { FadeInTime = 0.1, FadeOutTime = 0.1 }, Vector2.Zero);
        tree.AddNode($"{name}Motion", AnimationNode(clip), Vector2.Zero);
        tree.ConnectNode(name, 0, previous);
        tree.ConnectNode(name, 1, $"{name}Motion");
        previous = name;
    }

    private static AnimationNodeAnimation AnimationNode(StringName clip) => new()
    {
        Animation = $"{AnimationLibraryName}/{clip}",
    };

    private static StringName FirstAbilityClip(CharacterAnimationSet set) =>
        set.AbilityAnimations.Length > 0 ? set.AbilityAnimations[0].Animation : set.Dive;

    private static void RemoveSocket(BoneAttachment3D? socket)
    {
        if (socket is not null
            && GodotObject.IsInstanceValid(socket)
            && socket.GetParent() is { } parent)
        {
            parent.RemoveChild(socket);
            socket.Free();
        }
    }

    private static AnimationPlayer? FindAnimationPlayer(Node root)
    {
        if (root is AnimationPlayer player)
            return player;

        foreach (var child in root.GetChildren())
        {
            var found = FindAnimationPlayer(child);
            if (found is not null)
                return found;
        }

        return null;
    }

    private static MeshInstance3D? FindMesh(Node root, string suffix)
    {
        if (root is MeshInstance3D mesh && mesh.Name.ToString().EndsWith(suffix, StringComparison.Ordinal))
            return mesh;

        foreach (var child in root.GetChildren())
        {
            var found = FindMesh(child, suffix);
            if (found is not null)
                return found;
        }

        return null;
    }

    private static bool IsLocomotionClip(CharacterAnimationSet set, StringName clip) =>
        clip == set.Idle || clip == set.Walk || clip == set.Run || clip == set.Fall;

    private static void RemoveRootMotionTracks(Animation animation)
    {
        for (var index = animation.GetTrackCount() - 1; index >= 0; index--)
        {
            var path = animation.TrackGetPath(index).ToString();
            if (animation.TrackGetType(index) == Animation.TrackType.Position3D
                && path.EndsWith(":root", StringComparison.Ordinal))
            {
                animation.RemoveTrack(index);
            }
        }
    }
}
