using System;
using System.Collections.Generic;
using Contenda.Components.Health;
using Contenda.Weapons;
using Godot;

namespace Contenda.Core;

/// <summary>
/// Reproduz música, ambiência e efeitos com pools fixos por som.
/// </summary>
/// <remarks>
/// Cada cue tem no máximo três vozes simultâneas. Sons 3D recebem uma variação
/// de pitch de ±8%; os buses existentes são os responsáveis por aplicar os
/// volumes salvos em SettingsStore.
/// </remarks>
public sealed partial class AudioDirector : Node
{
    public static class CueIds
    {
        public static readonly StringName CombatSwing = new("combat.swing");
        public static readonly StringName CombatRevolver = new("combat.revolver");
        public static readonly StringName CombatCannon = new("combat.cannon");
        public static readonly StringName CombatReload = new("combat.reload");
        public static readonly StringName CombatImpact = new("combat.impact");
        public static readonly StringName CombatCritical = new("combat.critical");
        public static readonly StringName CombatExplosion = new("combat.explosion");
        public static readonly StringName CombatPlayerDamage = new("combat.player_damage");
        public static readonly StringName CombatTransform = new("combat.transform");
        public static readonly StringName CombatSpawn = new("combat.spawn");
        public static readonly StringName CombatDeath = new("combat.death");
        public static readonly StringName CombatWarning = new("combat.warning");
        public static readonly StringName CombatFootstep = new("combat.footstep");
        public static readonly StringName AbilityDashSlash = new("ability.dash_slash");
        public static readonly StringName AbilityDeadeye = new("ability.deadeye");
        public static readonly StringName AbilitySpinSlash = new("ability.spin_slash");
        public static readonly StringName AbilityRisingSlash = new("ability.rising_slash");
        public static readonly StringName AbilityQuickStepShot = new("ability.quick_step_shot");
        public static readonly StringName AbilityHeavyLunge = new("ability.heavy_lunge");
        public static readonly StringName AbilityFanTheHammer = new("ability.fan_the_hammer");
        public static readonly StringName AbilityExplosiveShot = new("ability.explosive_shot");
        public static readonly StringName UiClick = new("ui.click");
        public static readonly StringName UiBack = new("ui.back");
        public static readonly StringName UiConfirm = new("ui.confirm");
        public static readonly StringName MusicMenu = new("menu");
        public static readonly StringName MusicCombat = new("combat");
        public static readonly StringName MusicBoss = new("boss");
    }

    private const int VoicesPerCue = 3;
    private const float PitchVariation = 0.08f;
    private const float MusicFadeDuration = 0.8f;
    private static readonly CueSpec[] CueSpecs =
    [
        new(CueIds.CombatSwing, "SFX", true,
            ["res://assets/audio/kenney/rpg/knifeSlice.ogg", "res://assets/audio/kenney/rpg/knifeSlice2.ogg"]),
        new(CueIds.CombatRevolver, "SFX", true,
            ["res://assets/audio/kenney/impact/impactMetal_light_000.ogg", "res://assets/audio/kenney/impact/impactMetal_light_001.ogg"]),
        new(CueIds.CombatCannon, "SFX", true,
            ["res://assets/audio/kenney/impact/impactMetal_heavy_000.ogg", "res://assets/audio/kenney/impact/impactMetal_heavy_001.ogg"]),
        new(CueIds.CombatReload, "SFX", true,
            ["res://assets/audio/kenney/rpg/drawKnife1.ogg", "res://assets/audio/kenney/rpg/drawKnife2.ogg"]),
        new(CueIds.CombatImpact, "SFX", true,
            ["res://assets/audio/kenney/impact/impactGeneric_light_000.ogg", "res://assets/audio/kenney/impact/impactGeneric_light_001.ogg", "res://assets/audio/kenney/impact/impactGeneric_light_002.ogg", "res://assets/audio/kenney/impact/impactGeneric_light_003.ogg"]),
        new(CueIds.CombatCritical, "SFX", true,
            ["res://assets/audio/kenney/impact/impactMetal_heavy_000.ogg", "res://assets/audio/kenney/impact/impactMetal_heavy_001.ogg", "res://assets/audio/kenney/impact/impactMetal_heavy_002.ogg"]),
        new(CueIds.CombatExplosion, "SFX", true,
            ["res://assets/audio/kenney/impact/impactMining_000.ogg", "res://assets/audio/kenney/impact/impactMining_001.ogg", "res://assets/audio/kenney/impact/impactMining_002.ogg"]),
        new(CueIds.CombatPlayerDamage, "SFX", true,
            ["res://assets/audio/kenney/impact/impactGeneric_light_002.ogg", "res://assets/audio/kenney/impact/impactMetal_light_000.ogg"]),
        new(CueIds.CombatTransform, "SFX", true,
            ["res://assets/audio/kenney/interface/bong_001.ogg", "res://assets/audio/kenney/interface/confirmation_001.ogg"]),
        new(CueIds.CombatSpawn, "SFX", true,
            ["res://assets/audio/kenney/interface/maximize_001.ogg"]),
        new(CueIds.CombatDeath, "SFX", true,
            ["res://assets/audio/kenney/impact/impactGeneric_light_003.ogg"]),
        new(CueIds.CombatWarning, "SFX", true,
            ["res://assets/audio/kenney/interface/error_001.ogg"]),
        new(CueIds.CombatFootstep, "SFX", true,
            ["res://assets/audio/kenney/rpg/footstep00.ogg", "res://assets/audio/kenney/rpg/footstep01.ogg", "res://assets/audio/kenney/rpg/footstep02.ogg"]),
        new(CueIds.AbilityDashSlash, "SFX", true,
            ["res://assets/audio/kenney/rpg/knifeSlice.ogg"]),
        new(CueIds.AbilityDeadeye, "SFX", true,
            ["res://assets/audio/kenney/impact/impactMetal_heavy_000.ogg"]),
        new(CueIds.AbilitySpinSlash, "SFX", true,
            ["res://assets/audio/kenney/rpg/knifeSlice2.ogg"]),
        new(CueIds.AbilityRisingSlash, "SFX", true,
            ["res://assets/audio/kenney/rpg/drawKnife1.ogg"]),
        new(CueIds.AbilityQuickStepShot, "SFX", true,
            ["res://assets/audio/kenney/impact/impactMetal_light_000.ogg"]),
        new(CueIds.AbilityHeavyLunge, "SFX", true,
            ["res://assets/audio/kenney/impact/impactMetal_heavy_001.ogg"]),
        new(CueIds.AbilityFanTheHammer, "SFX", true,
            ["res://assets/audio/kenney/impact/impactMetal_light_001.ogg"]),
        new(CueIds.AbilityExplosiveShot, "SFX", true,
            ["res://assets/audio/kenney/impact/impactMining_000.ogg"]),
        new(CueIds.UiClick, "UI", false,
            ["res://assets/audio/kenney/interface/click_001.ogg"]),
        new(CueIds.UiBack, "UI", false,
            ["res://assets/audio/kenney/interface/back_001.ogg"]),
        new(CueIds.UiConfirm, "UI", false,
            ["res://assets/audio/kenney/interface/confirmation_001.ogg"]),
    ];

    private static readonly Dictionary<StringName, string> MusicTracks = new()
    {
        [CueIds.MusicMenu] = "res://assets/audio/opengameart/music/menu.ogg",
        [CueIds.MusicCombat] = "res://assets/audio/opengameart/music/combat.ogg",
        [CueIds.MusicBoss] = "res://assets/audio/opengameart/music/boss.ogg",
    };

    private readonly RandomNumberGenerator _random = new();
    private readonly Dictionary<StringName, VoicePool3D> _spatialPools = new();
    private readonly Dictionary<StringName, VoicePool2D> _uiPools = new();
    private readonly Dictionary<AudioStream, VoicePool3D> _streamPools = new();
    private readonly Dictionary<AudioStream, string> _resourcePaths = new();
    private readonly AudioStreamPlayer[] _musicPlayers = new AudioStreamPlayer[2];
    private AudioStreamPlayer? _ambiencePlayer;
    private int _activeMusic = -1;
    private int _incomingMusic = -1;
    private int _outgoingMusic = -1;
    private float _fadeElapsed;
    private float _outgoingStartLinear;

    /// <summary>Quantas vozes 3D deste cue estão tocando; para probes.</summary>
    public int PlayingVoices(StringName cue) =>
        _spatialPools.TryGetValue(cue, out var pool) ? pool.PlayingCount : 0;

    public override void _Ready()
    {
        ServiceLocator.Register(this);
        ProcessMode = ProcessModeEnum.Always;
        _random.Randomize();

        foreach (var spec in CueSpecs)
            ConstruirPool(spec);

        for (var i = 0; i < _musicPlayers.Length; i++)
        {
            _musicPlayers[i] = new AudioStreamPlayer { Bus = "Music", VolumeDb = -80f };
            AddChild(_musicPlayers[i]);
        }

        _ambiencePlayer = new AudioStreamPlayer { Bus = "Ambience", VolumeDb = 0f };
        AddChild(_ambiencePlayer);

        ServiceLocator.Events.MeleeSwingRequested += AoIniciarGolpe;
        ServiceLocator.Events.ShotFired += AoDisparar;
        ServiceLocator.Events.WeaponReloadRequested += AoRecarregar;
        ServiceLocator.Events.DamageImpactRequested += AoAcertar;
        ServiceLocator.Events.TransformationChanged += AoTransformar;
        ServiceLocator.Events.EnemySpawned += AoNascerInimigo;
        ServiceLocator.Events.EnemyDied += AoMorrerInimigo;
        ServiceLocator.Events.EnemyAttackWarning += AoAvisoDeAtaque;
        ServiceLocator.Events.PlayerDamaged += AoJogadorReceberDano;
        ServiceLocator.Events.EnemyKilled += AoAbaterInimigo;
        ServiceLocator.Events.MatchEnded += AoTerminarPartida;
        ServiceLocator.Events.WaveAnnounced += AoAnunciarOnda;
        GD.Print("[boot] AudioDirector pronto (pools de três vozes por som)");
    }

    public override void _ExitTree()
    {
        ServiceLocator.Events.MeleeSwingRequested -= AoIniciarGolpe;
        ServiceLocator.Events.ShotFired -= AoDisparar;
        ServiceLocator.Events.WeaponReloadRequested -= AoRecarregar;
        ServiceLocator.Events.DamageImpactRequested -= AoAcertar;
        ServiceLocator.Events.TransformationChanged -= AoTransformar;
        ServiceLocator.Events.EnemySpawned -= AoNascerInimigo;
        ServiceLocator.Events.EnemyDied -= AoMorrerInimigo;
        ServiceLocator.Events.EnemyAttackWarning -= AoAvisoDeAtaque;
        ServiceLocator.Events.PlayerDamaged -= AoJogadorReceberDano;
        ServiceLocator.Events.EnemyKilled -= AoAbaterInimigo;
        ServiceLocator.Events.MatchEnded -= AoTerminarPartida;
        ServiceLocator.Events.WaveAnnounced -= AoAnunciarOnda;

        foreach (var pool in _spatialPools.Values)
            pool.StopAll();
        foreach (var pool in _uiPools.Values)
            pool.StopAll();
        foreach (var pool in _streamPools.Values)
            pool.StopAll();
        foreach (var player in _musicPlayers)
        {
            player.Stop();
            player.Stream = null;
        }

        if (_ambiencePlayer is not null)
        {
            _ambiencePlayer.Stop();
            _ambiencePlayer.Stream = null;
        }

        _spatialPools.Clear();
        _uiPools.Clear();
        _streamPools.Clear();
        _resourcePaths.Clear();
    }

    /// <summary>Toca um cue posicional. Se as três vozes ocuparem, descarta a nova reprodução.</summary>
    public bool Play3D(StringName cue, Vector3 position)
    {
        if (!_spatialPools.TryGetValue(cue, out var pool))
            return false;

        return pool.Play(this, position, _random);
    }

    /// <summary>Toca um cue de interface pelo bus UI.</summary>
    public bool PlayUi(StringName cue)
    {
        if (!_uiPools.TryGetValue(cue, out var pool))
            return false;

        return pool.Play(_random);
    }

    /// <summary>Cria o pool de um stream antes de ele ser usado no caminho de física.</summary>
    public void PrepareStream3D(AudioStream stream, string bus = "SFX")
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (_streamPools.ContainsKey(stream))
            return;

        _streamPools.Add(stream, CriarPool3D([stream], bus));
    }

    /// <summary>Toca um AudioStream 3D preparado, limitado a três vozes por recurso.</summary>
    public bool PlayStream3D(AudioStream stream, Vector3 position)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!_streamPools.TryGetValue(stream, out var pool))
            return false;

        return pool.Play(this, position, _random);
    }

    /// <summary>Inicia uma faixa em loop e cruza para ela em 0,8 segundo.</summary>
    public void PlayMusic(StringName track)
    {
        if (!MusicTracks.TryGetValue(track, out var path))
        {
            GD.PushWarning($"AudioDirector: trilha desconhecida '{track}'.");
            return;
        }

        var stream = GD.Load<AudioStream>(path);
        if (stream is null)
        {
            GD.PushError($"AudioDirector: não consegui carregar '{path}'.");
            return;
        }

        ConfigurarLoop(stream);
        var outgoing = _incomingMusic >= 0 ? _incomingMusic : _activeMusic;
        if (outgoing >= 0 && _musicPlayers[outgoing].Stream?.ResourcePath == path)
            return;

        var incoming = outgoing == 0 ? 1 : 0;
        for (var i = 0; i < _musicPlayers.Length; i++)
        {
            if (i != outgoing && i != incoming)
                _musicPlayers[i].Stop();
        }

        _musicPlayers[incoming].Stop();
        _musicPlayers[incoming].Stream = stream;
        _musicPlayers[incoming].VolumeDb = -80f;
        _musicPlayers[incoming].Play();

        _outgoingMusic = outgoing;
        _incomingMusic = incoming;
        _outgoingStartLinear = outgoing >= 0 ? Mathf.DbToLinear(_musicPlayers[outgoing].VolumeDb) : 0f;
        _fadeElapsed = 0f;
    }

    /// <summary>Começa ou encerra a ambiência da cidade no bus configurável Ambience.</summary>
    public void SetCityAmbience(bool enabled)
    {
        if (_ambiencePlayer is null)
            return;

        if (!enabled)
        {
            _ambiencePlayer.Stop();
            _ambiencePlayer.Stream = null;
            return;
        }

        if (_ambiencePlayer.Playing)
            return;

        var stream = GD.Load<AudioStream>("res://assets/audio/opengameart/ambience/city.ogg");
        if (stream is null)
        {
            GD.PushError("AudioDirector: não consegui carregar a ambiência da cidade.");
            return;
        }

        ConfigurarLoop(stream);
        _ambiencePlayer.Stream = stream;
        _ambiencePlayer.Play();
    }

    public override void _Process(double delta)
    {
        if (_incomingMusic < 0)
            return;

        _fadeElapsed += (float)delta;
        var amount = Mathf.Clamp(_fadeElapsed / MusicFadeDuration, 0f, 1f);
        _musicPlayers[_incomingMusic].VolumeDb = Mathf.LinearToDb(Mathf.Max(0.001f, amount));

        if (_outgoingMusic >= 0)
        {
            var remaining = _outgoingStartLinear * (1f - amount);
            _musicPlayers[_outgoingMusic].VolumeDb = Mathf.LinearToDb(Mathf.Max(0.001f, remaining));
        }

        if (amount < 1f)
            return;

        if (_outgoingMusic >= 0)
            _musicPlayers[_outgoingMusic].Stop();

        _activeMusic = _incomingMusic;
        _musicPlayers[_activeMusic].VolumeDb = 0f;
        _incomingMusic = -1;
        _outgoingMusic = -1;
    }

    private void ConstruirPool(CueSpec spec)
    {
        var streams = new List<AudioStream>(spec.Paths.Length);
        foreach (var path in spec.Paths)
        {
            var stream = GD.Load<AudioStream>(path);
            if (stream is null)
            {
                GD.PushError($"AudioDirector: cue {spec.Id} não encontrou '{path}'.");
                continue;
            }

            streams.Add(stream);
        }

        if (streams.Count == 0)
            return;

        if (spec.Spatial)
            _spatialPools[spec.Id] = CriarPool3D(streams.ToArray(), spec.Bus);
        else
            _uiPools[spec.Id] = CriarPool2D(streams.ToArray(), spec.Bus);
    }

    private VoicePool3D CriarPool3D(AudioStream[] streams, string bus)
    {
        foreach (var stream in streams)
        {
            if (!_resourcePaths.ContainsKey(stream))
                _resourcePaths.Add(stream, stream.ResourcePath);
        }

        var players = new AudioStreamPlayer3D[VoicesPerCue];
        for (var i = 0; i < players.Length; i++)
        {
            players[i] = new AudioStreamPlayer3D { Bus = bus, MaxDistance = 36f, UnitSize = 8f };
            AddChild(players[i]);
        }

        return new VoicePool3D(streams, players);
    }

    private VoicePool2D CriarPool2D(AudioStream[] streams, string bus)
    {
        var players = new AudioStreamPlayer[VoicesPerCue];
        for (var i = 0; i < players.Length; i++)
        {
            players[i] = new AudioStreamPlayer { Bus = bus };
            AddChild(players[i]);
        }

        return new VoicePool2D(streams, players);
    }

    private void AoIniciarGolpe(MeleeSwingEvent evento)
        => Play3D(CueIds.CombatSwing, evento.Position);

    private void AoDisparar(ShotFiredEvent evento)
        => Play3D(evento.WeaponId == WeaponDefinition.ArmCannonId ? CueIds.CombatCannon : CueIds.CombatRevolver, evento.Origin);

    private void AoRecarregar(WeaponReloadEvent evento)
        => Play3D(CueIds.CombatReload, evento.Position);

    private void AoAcertar(DamageImpactEvent evento)
        => Play3D(evento.IsCritical ? CueIds.CombatCritical : evento.Type == DamageType.Explosive ? CueIds.CombatExplosion : CueIds.CombatImpact, evento.Position);

    private void AoTransformar(TransformationPresentationEvent evento)
    {
        if (evento.Activated)
            Play3D(CueIds.CombatTransform, evento.Position);
    }

    private void AoNascerInimigo(EnemySpawnedPresentationEvent evento)
    {
        Play3D(CueIds.CombatSpawn, evento.Position);
        if (evento.IsBoss)
            PlayMusic(CueIds.MusicBoss);
    }

    private void AoMorrerInimigo(EnemyDiedPresentationEvent evento)
        => Play3D(CueIds.CombatDeath, evento.Position);

    private void AoAvisoDeAtaque(EnemyAttackWarningEvent evento)
        => Play3D(CueIds.CombatWarning, evento.Position);

    private void AoJogadorReceberDano(PlayerDamagedEvent evento)
        => Play3D(CueIds.CombatPlayerDamage, evento.Position);

    private void AoAbaterInimigo(EnemyKilledEvent evento)
    {
        if (evento.IsBoss)
            PlayMusic(CueIds.MusicCombat);
    }

    private void AoTerminarPartida(MatchEndedEvent _)
    {
        SetCityAmbience(false);
        PlayMusic(CueIds.MusicMenu);
    }

    private void AoAnunciarOnda(WaveAnnouncedEvent _)
        => PlayUi(CueIds.UiConfirm);

    private int PlayingInstancesOf(AudioStream stream)
    {
        if (!_resourcePaths.TryGetValue(stream, out var resourcePath) || string.IsNullOrEmpty(resourcePath))
            return 0;

        var count = 0;
        foreach (var pool in _spatialPools.Values)
            count += pool.CountPlaying(resourcePath, _resourcePaths);
        foreach (var pool in _streamPools.Values)
            count += pool.CountPlaying(resourcePath, _resourcePaths);
        return count;
    }

    private static void ConfigurarLoop(AudioStream stream)
    {
        if (stream is AudioStreamOggVorbis vorbis)
            vorbis.Loop = true;
    }

    private sealed record CueSpec(StringName Id, string Bus, bool Spatial, string[] Paths);

    private sealed class VoicePool3D(AudioStream[] streams, AudioStreamPlayer3D[] players)
    {
        public void StopAll()
        {
            foreach (var player in players)
            {
                player.Stop();
                player.Stream = null;
            }
        }

        public int PlayingCount
        {
            get
            {
                var count = 0;
                foreach (var player in players)
                {
                    if (player.Playing)
                        count++;
                }

                return count;
            }
        }

        public int CountPlaying(string resourcePath, Dictionary<AudioStream, string> resourcePaths)
        {
            var count = 0;
            foreach (var player in players)
            {
                if (player.Playing
                    && player.Stream is { } stream
                    && resourcePaths.TryGetValue(stream, out var streamPath)
                    && string.Equals(streamPath, resourcePath, StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        public bool Play(AudioDirector owner, Vector3 position, RandomNumberGenerator random)
        {
            AudioStreamPlayer3D? selectedPlayer = null;
            foreach (var player in players)
            {
                if (player.Playing)
                    continue;

                selectedPlayer = player;
                break;
            }

            if (selectedPlayer is null)
                return false;

            var firstStream = random.RandiRange(0, streams.Length - 1);
            for (var offset = 0; offset < streams.Length; offset++)
            {
                var stream = streams[(firstStream + offset) % streams.Length];
                if (owner.PlayingInstancesOf(stream) >= VoicesPerCue)
                    continue;

                selectedPlayer.Stream = stream;
                selectedPlayer.GlobalPosition = position;
                selectedPlayer.PitchScale = random.RandfRange(1f - PitchVariation, 1f + PitchVariation);
                selectedPlayer.Play();
                return true;
            }

            return false;
        }
    }

    private sealed class VoicePool2D(AudioStream[] streams, AudioStreamPlayer[] players)
    {
        public void StopAll()
        {
            foreach (var player in players)
            {
                player.Stop();
                player.Stream = null;
            }
        }

        public bool Play(RandomNumberGenerator random)
        {
            foreach (var player in players)
            {
                if (player.Playing)
                    continue;

                player.Stream = streams[random.RandiRange(0, streams.Length - 1)];
                player.PitchScale = random.RandfRange(1f - PitchVariation, 1f + PitchVariation);
                player.Play();
                return true;
            }

            return false;
        }
    }
}
