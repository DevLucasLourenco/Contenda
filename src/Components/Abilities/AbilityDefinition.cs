using Contenda.Components.Combat;
using Contenda.Input;
using Godot;

namespace Contenda.Components.Abilities;

/// <summary>Assinatura visual própria de cada habilidade de catálogo.</summary>
public enum AbilityVfxStyle : byte
{
    DashSlash,
    Deadeye,
    SpinSlash,
    RisingSlash,
    QuickStepShot,
    HeavyLunge,
    FanTheHammer,
    ExplosiveShot,
}

/// <summary>
/// Uma habilidade: sequência, custo, recarga e efeito — tudo dado, nada em código.
/// </summary>
/// <remarks>
/// Meta arquitetural da spec 05: adicionar uma habilidade nova é criar um
/// `.tres`, nunca mexer em `AbilityComponent`. Ver ticket 14/15.
///
/// O cast usa o estilo, a cor e o som configurados no recurso. `CastVfx` e
/// `HitVfx` continuam como pontos de extensão para cenas customizadas; a
/// apresentação procedural deste ticket não depende deles.
/// </remarks>
[GlobalClass]
public sealed partial class AbilityDefinition : Resource
{
    // --- identidade -----------------------------------------------------

    /// <summary>Identificador estável — "swordsman.dash_slash".</summary>
    [Export] public StringName Id { get; set; } = new("sem_habilidade");

    /// <summary>Nome exibido.</summary>
    [Export] public string DisplayName { get; set; } = "Sem nome";

    /// <summary>Ícone para o HUD. Sem consumidor até o ticket 16.</summary>
    [Export] public Texture2D? Icon { get; set; }

    /// <summary>Descrição para o HUD. Sem consumidor até o ticket 16.</summary>
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";

    // --- entrada ----------------------------------------------------------

    /// <summary>
    /// A sequência de WASD que executa esta habilidade.
    /// </summary>
    /// <remarks>
    /// <c>Godot.Collections.Array&lt;T&gt;</c>, não <c>CommandDirection[]</c>: um
    /// array de enum simples reprova a exportação com GD0102 — o marshaling do
    /// Godot para tipos exportados só cobre os `Godot.Collections.Array`.
    /// </remarks>
    [Export] public Godot.Collections.Array<CommandDirection> Sequence { get; set; } = [];

    // --- custos e gates -----------------------------------------------------

    /// <summary>Mana consumida, só depois de todos os outros gates passarem.</summary>
    [Export(PropertyHint.Range, "0,200,1")] public float ManaCost { get; set; } = 15f;

    /// <summary>Recarga, em segundos. Começa no INÍCIO da execução.</summary>
    [Export(PropertyHint.Range, "0,30,0.1")] public float Cooldown { get; set; } = 3f;

    /// <summary>Preparação antes do efeito acontecer, em segundos.</summary>
    [Export(PropertyHint.Range, "0,2,0.01")] public float CastTime { get; set; } = 0.10f;

    /// <summary>Recuperação depois do efeito, em segundos. Locks continuam ativos.</summary>
    [Export(PropertyHint.Range, "0,2,0.01")] public float RecoveryTime { get; set; } = 0.25f;

    /// <summary>
    /// Tags exigidas para executar. Sem consumidor até o M4 ter uma fonte de
    /// tags de verdade (transformações) — o gate está desligado até lá.
    /// </summary>
    [Export] public StringName[] RequiredTags { get; set; } = [];

    /// <summary>Tags que impedem a execução. Mesma ressalva de <see cref="RequiredTags"/>.</summary>
    [Export] public StringName[] BlockedByTags { get; set; } = [];

    // --- efeito -------------------------------------------------------------

    /// <summary>Qual comportamento executa o efeito — ver <see cref="AbilityBehaviorRegistry"/>.</summary>
    [Export] public AbilityEffectKind Kind { get; set; } = AbilityEffectKind.DashAttack;

    /// <summary>Dano antes dos multiplicadores.</summary>
    [Export(PropertyHint.Range, "0,300,1")] public float Damage { get; set; } = 35f;

    /// <summary>Alcance do efeito, em metros. Uso varia por <see cref="Kind"/>.</summary>
    [Export(PropertyHint.Range, "0,40,0.1")] public float Range { get; set; } = 6f;

    /// <summary>Raio/tolerância de acerto, em metros.</summary>
    [Export(PropertyHint.Range, "0.2,10,0.1")] public float Radius { get; set; } = 2f;

    /// <summary>Meia-abertura do cone, em graus. Uso varia por <see cref="Kind"/>.</summary>
    [Export(PropertyHint.Range, "5,180,1")] public float Angle { get; set; } = 90f;

    /// <summary>Distância do deslocamento, em metros. Só <see cref="AbilityEffectKind.DashAttack"/>.</summary>
    [Export(PropertyHint.Range, "0,15,0.1")] public float DashDistance { get; set; } = 6f;

    /// <summary>Repulsão base aplicada ao alvo.</summary>
    [Export(PropertyHint.Range, "0,20,0.5")] public float Knockback { get; set; } = 4f;

    /// <summary>Quantos alvos o efeito atinge. Zero é ilimitado.</summary>
    [Export(PropertyHint.Range, "0,20,1")] public int MaxTargets { get; set; }

    /// <summary>Cena do projétil. Só <see cref="AbilityEffectKind.Projectile"/>, sem consumidor ainda.</summary>
    [Export] public PackedScene? ProjectileScene { get; set; }

    // --- locks durante a execução ---------------------------------------------

    /// <summary>O que fica travado do início do cast até o fim da recuperação.</summary>
    [Export] public ActionLock LocksDuringCast { get; set; } = ActionLock.Abilities;

    /// <summary>Se a execução concede invulnerabilidade. Sem consumidor ainda.</summary>
    [Export] public bool GrantsInvulnerability { get; set; }

    // --- apresentação -----------------------------------------------------

    /// <summary>Nome da animação. Sem consumidor até o M8 ter animações.</summary>
    [Export] public StringName AnimationName { get; set; } = new("");

    /// <summary>Cena opcional para uma futura composição de VFX criada pelo artista.</summary>
    [Export] public PackedScene? CastVfx { get; set; }

    /// <summary>Variação mesh procedural do cast.</summary>
    [Export] public AbilityVfxStyle CastVfxStyle { get; set; }

    /// <summary>Cor de assinatura deste VFX.</summary>
    [Export] public Color CastVfxColor { get; set; } = Colors.White;

    /// <summary>VFX do acerto. Sem consumidor ainda.</summary>
    [Export] public PackedScene? HitVfx { get; set; }

    /// <summary>Som posicional do cast, tocado pelo pool do AudioDirector.</summary>
    [Export] public AudioStream? CastSfx { get; set; }

    /// <summary>Intensidade do screen shake ao executar. Sem consumidor ainda.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float CameraShake { get; set; } = 0.2f;
}
