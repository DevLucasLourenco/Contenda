using System;
using System.Collections.Generic;

namespace Contenda.Settings;

public enum WindowModeOption { Windowed, Fullscreen, BorderlessFullscreen }

public enum VSyncOption { Enabled, Disabled, Adaptive }

public enum ShadowQualityOption { Low, Medium, High }

public enum CommandWindowOption { Short, Normal, Long }

/// <summary>Opções de vídeo. Spec 14 §1.</summary>
public sealed class VideoSettings
{
    public WindowModeOption WindowMode { get; set; } = WindowModeOption.Windowed;

    /// <summary>Largura da janela em modo Janela. Zero mantém a atual.</summary>
    public int ResolutionWidth { get; set; }

    public int ResolutionHeight { get; set; }

    public VSyncOption VSync { get; set; } = VSyncOption.Enabled;

    /// <summary>Quadros por segundo no máximo. Zero é ilimitado.</summary>
    public int FpsLimit { get; set; }

    public ShadowQualityOption Shadows { get; set; } = ShadowQualityOption.High;

    /// <summary>Escala de renderização 3D, de 0,5 a 1,0.</summary>
    public float RenderScale { get; set; } = 1f;
}

/// <summary>Volumes de 0 a 100 por bus. Spec 14 §1.</summary>
public sealed class AudioSettings
{
    public int Master { get; set; } = 100;

    public int Music { get; set; } = 100;

    public int Sfx { get; set; } = 100;

    public int Ui { get; set; } = 100;

    public int Ambience { get; set; } = 100;
}

/// <summary>Opções de jogo. Spec 14 §1 ("Jogo").</summary>
public sealed class GameplaySettings
{
    /// <summary>Intensidade do tremor de tela, de 0 a 150 por cento.</summary>
    public int ShakeIntensityPercent { get; set; } = 100;

    public bool ShowDamageNumbers { get; set; } = true;

    public bool ShowComboGuide { get; set; } = true;

    /// <summary>Tolerância de tempo dos comandos -- acessibilidade deliberada (spec 14 §1).</summary>
    public CommandWindowOption CommandWindow { get; set; } = CommandWindowOption.Normal;

    public float ShakeMultiplier => ShakeIntensityPercent / 100f;

    /// <summary>Quanto tempo um símbolo de comando vale, em segundos: curta 0,5, normal 0,7, longa 0,9.</summary>
    public float CommandWindowSeconds => CommandWindow switch
    {
        CommandWindowOption.Short => 0.5f,
        CommandWindowOption.Long => 0.9f,
        _ => 0.7f,
    };
}

/// <summary>
/// Tudo que o jogador ajusta e que sobrevive a fechar o jogo. Spec 14 §2.
/// </summary>
/// <remarks>
/// Sem engine: o formato e as regras (limites, padrões, conflito de teclas) são
/// testados em xUnit. <see cref="Bindings"/> guarda só o que o jogador MUDOU
/// (ação -> lista de teclas), nunca o padrão inteiro: uma ação nova em uma
/// versão futura nasce com o padrão, sem migração.
/// </remarks>
public sealed class GameSettings
{
    public const int CurrentVersion = 1;

    public VideoSettings Video { get; set; } = new();

    public AudioSettings Audio { get; set; } = new();

    public GameplaySettings Gameplay { get; set; } = new();

    /// <summary>Ação -> teclas trocadas pelo jogador, como "key:87" / "mouse:1". Ver <see cref="BindingSpec"/>.</summary>
    public Dictionary<string, List<string>> Bindings { get; set; } = new();

    /// <summary>Uma cópia independente, para o menu editar um rascunho sem mexer no que está valendo.</summary>
    public GameSettings Clone()
    {
        var copia = new GameSettings
        {
            Video = new VideoSettings
            {
                WindowMode = Video.WindowMode,
                ResolutionWidth = Video.ResolutionWidth,
                ResolutionHeight = Video.ResolutionHeight,
                VSync = Video.VSync,
                FpsLimit = Video.FpsLimit,
                Shadows = Video.Shadows,
                RenderScale = Video.RenderScale,
            },
            Audio = new AudioSettings
            {
                Master = Audio.Master,
                Music = Audio.Music,
                Sfx = Audio.Sfx,
                Ui = Audio.Ui,
                Ambience = Audio.Ambience,
            },
            Gameplay = new GameplaySettings
            {
                ShakeIntensityPercent = Gameplay.ShakeIntensityPercent,
                ShowDamageNumbers = Gameplay.ShowDamageNumbers,
                ShowComboGuide = Gameplay.ShowComboGuide,
                CommandWindow = Gameplay.CommandWindow,
            },
        };

        foreach (var (acao, teclas) in Bindings)
            copia.Bindings[acao] = [.. teclas];

        return copia;
    }

    /// <summary>Puxa todo valor para dentro do que faz sentido -- um arquivo editado à mão não quebra o jogo.</summary>
    public void Normalize()
    {
        Video.ResolutionWidth = Math.Max(0, Video.ResolutionWidth);
        Video.ResolutionHeight = Math.Max(0, Video.ResolutionHeight);
        Video.FpsLimit = Math.Max(0, Video.FpsLimit);
        Video.RenderScale = Math.Clamp(float.IsNaN(Video.RenderScale) ? 1f : Video.RenderScale, 0.5f, 1f);

        Audio.Master = Math.Clamp(Audio.Master, 0, 100);
        Audio.Music = Math.Clamp(Audio.Music, 0, 100);
        Audio.Sfx = Math.Clamp(Audio.Sfx, 0, 100);
        Audio.Ui = Math.Clamp(Audio.Ui, 0, 100);
        Audio.Ambience = Math.Clamp(Audio.Ambience, 0, 100);

        Gameplay.ShakeIntensityPercent = Math.Clamp(Gameplay.ShakeIntensityPercent, 0, 150);
    }
}
