using Godot;

namespace Contenda.Components.AI;

/// <summary>
/// Os parâmetros de percepção e comportamento de um inimigo.
/// </summary>
/// <remarks>
/// Deliberadamente pequeno: o roster completo (spec 09 §5 —
/// `ModelScene`/`AnimationSet`/`AttackKind`/`ScoreValue`/`IsElite`/
/// `EliteTint`/`StaggerResistance`) pertence a tickets futuros (25, 26, 29,
/// 34), que ainda não existem em código. Adicionar esses campos agora seria
/// balanceamento para um sistema que não roda ainda -- regra do CLAUDE.md
/// contra desenhar para necessidade hipotética. Dano e alcance de ataque não
/// entram aqui: já vêm do <see cref="Contenda.Weapons.WeaponDefinition"/> da
/// arma equipada, a MESMA que o jogador usa (ticket 22 exige isso).
/// </remarks>
[GlobalClass]
public sealed partial class EnemyDefinition : Resource
{
    /// <summary>Raio em que o inimigo passa a perceber o alvo, em metros.</summary>
    [Export(PropertyHint.Range, "1,40,0.5")] public float DetectionRadius { get; set; } = 22f;

    /// <summary>
    /// Raio além do qual, já em perseguição, o alvo conta como fora de vista.
    /// </summary>
    /// <remarks>Maior que <see cref="DetectionRadius"/> de propósito: evita ioiô de estado perto da borda.</remarks>
    [Export(PropertyHint.Range, "1,60,0.5")] public float LoseTargetRadius { get; set; } = 30f;

    /// <summary>Quanto tempo sem ver o alvo até desistir de vez, em segundos.</summary>
    [Export(PropertyHint.Range, "0.5,10,0.5")] public float LoseTargetDelay { get; set; } = 3f;

    /// <summary>Duração do estado de alerta antes de partir para cima, em segundos.</summary>
    [Export(PropertyHint.Range, "0.1,2,0.05")] public float AlertDuration { get; set; } = 0.4f;

    /// <summary>
    /// Distância para começar a preparar o golpe, em metros.
    /// </summary>
    /// <remarks>
    /// Independente do alcance real da arma: começar a preparação um pouco
    /// ANTES de estar dentro do alcance de dano dá tempo do avanço do golpe
    /// (spec 07 §4) fechar a distância, em vez de exigir estar imóvel e
    /// colado no alvo primeiro.
    /// </remarks>
    [Export(PropertyHint.Range, "0.5,20,0.1")] public float AttackRange { get; set; } = 2.2f;

    /// <summary>
    /// Preparação visível antes do golpe em si, em segundos.
    /// </summary>
    /// <remarks>
    /// Maior que a janela de acerto de qualquer passo do jogador de propósito
    /// -- a telegrafia é o que evita o dano parecer aleatório sob câmera fixa
    /// e vários inimigos, spec 09 §7.
    /// </remarks>
    [Export(PropertyHint.Range, "0.1,2,0.05")] public float AttackWindup { get; set; } = 0.45f;

    /// <summary>Tempo sem poder atacar de novo depois de golpear, em segundos.</summary>
    [Export(PropertyHint.Range, "0.1,5,0.05")] public float AttackCooldown { get; set; } = 1.4f;

    /// <summary>Duração do atordoamento ao apanhar, em segundos.</summary>
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float StaggerDuration { get; set; } = 0.5f;
}
