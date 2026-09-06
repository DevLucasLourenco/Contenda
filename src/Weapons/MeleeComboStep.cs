using Godot;

namespace Contenda.Weapons;

/// <summary>
/// Um passo da cadeia de golpes corpo a corpo.
/// </summary>
/// <remarks>
/// Os tempos são **dados**, não faixas de chamada dentro da animação: no M8 os
/// modelos são substituídos e faixas embutidas se perderiam junto. Quando a
/// animação real chegar, ajusta-se ESTE arquivo, não a animação — ver spec 13 §6.
/// </remarks>
[GlobalClass]
public sealed partial class MeleeComboStep : Resource
{
    /// <summary>Multiplicador de dano deste passo.</summary>
    [Export(PropertyHint.Range, "0.1,5,0.05")] public float DamageMultiplier { get; set; } = 1f;

    /// <summary>Quando a área de dano abre, em segundos desde o início do golpe.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float HitWindowStart { get; set; } = 0.18f;

    /// <summary>Quando a área de dano fecha.</summary>
    [Export(PropertyHint.Range, "0,2,0.01")] public float HitWindowEnd { get; set; } = 0.32f;

    /// <summary>Até quando um novo clique encadeia o próximo golpe.</summary>
    [Export(PropertyHint.Range, "0.1,3,0.01")] public float ComboWindowEnd { get; set; } = 0.75f;

    /// <summary>
    /// Quanto o personagem avança ao golpear, em metros.
    /// </summary>
    /// <remarks>
    /// Golpe corpo a corpo que não desloca é muito difícil de acertar sob câmera
    /// fixa: o jogador julga distância mal no ângulo inclinado, e o avanço
    /// perdoa o erro.
    /// </remarks>
    [Export(PropertyHint.Range, "0,3,0.05")] public float ForwardStep { get; set; } = 0.6f;

    /// <summary>Multiplicador da repulsão aplicada ao alvo.</summary>
    [Export(PropertyHint.Range, "0,5,0.1")] public float KnockbackMultiplier { get; set; } = 1f;

    /// <summary>
    /// Duração do congelamento local ao conectar, em segundos. Ver ticket 11.
    /// </summary>
    /// <remarks>
    /// É por isto que o finalizador (0,09 s) não precisa de nenhum código
    /// especial: é só um passo com um número maior aqui que os outros
    /// (0,04 s). Aplicado aos dois envolvidos — quem bateu e quem apanhou.
    /// </remarks>
    [Export(PropertyHint.Range, "0,0.3,0.01")] public float HitstopSeconds { get; set; } = 0.04f;

    /// <summary>
    /// Somado a <see cref="HitstopSeconds"/> quando o golpe sai crítico.
    /// </summary>
    /// <remarks>
    /// Somado ao valor DESTE passo, não um número absoluto -- assim o
    /// finalizador crítico (0,09 s + 0,05 s) continua mais longo que um jab
    /// crítico (0,04 s + 0,05 s), e os dois continuam mais longos que a
    /// versão normal do mesmo passo. Ticket 18, spec 16 §5.
    /// </remarks>
    [Export(PropertyHint.Range, "0,0.3,0.01")] public float CriticalHitstopBonus { get; set; } = 0.05f;
}
