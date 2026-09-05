using Godot;

namespace Contenda.Weapons;

/// <summary>
/// O que uma arma é.
/// </summary>
/// <remarks>
/// **É aqui que M1 ganha significado.** O botão não quer dizer "soco": quer
/// dizer "ataque básico", e a arma equipada decide o resto. Trocar este recurso
/// troca o comportamento do ataque sem um único `if` sobre qual personagem está
/// em jogo — ver spec 07 §1.
/// </remarks>
[GlobalClass]
public sealed partial class WeaponDefinition : Resource
{
    /// <summary>Identificador estável.</summary>
    [Export] public StringName Id { get; set; } = new("sem_arma");

    /// <summary>Nome exibido.</summary>
    [Export] public string DisplayName { get; set; } = "Sem arma";

    /// <summary>Corpo a corpo ou tiro.</summary>
    [Export] public WeaponKind Kind { get; set; } = WeaponKind.Melee;

    /// <summary>Dano antes dos multiplicadores.</summary>
    [Export(PropertyHint.Range, "1,200,1")] public float BaseDamage { get; set; } = 20f;

    /// <summary>Alcance do golpe, em metros.</summary>
    [Export(PropertyHint.Range, "0.5,30,0.1")] public float Range { get; set; } = 2.2f;

    /// <summary>
    /// Meia-abertura do cone de acerto, em graus. 180 significa em volta todo.
    /// </summary>
    /// <remarks>Só corpo a corpo. Hitscan mira em linha reta; ver <see cref="SpreadDegrees"/>.</remarks>
    [Export(PropertyHint.Range, "10,180,5")] public float HalfAngle { get; set; } = 60f;

    /// <summary>Repulsão base aplicada ao alvo.</summary>
    [Export(PropertyHint.Range, "0,20,0.5")] public float Knockback { get; set; } = 2f;

    /// <summary>A cadeia de golpes. Vazia vira um golpe único com os padrões. Só corpo a corpo.</summary>
    [Export] public MeleeComboStep[] ComboSteps { get; set; } = [];

    /// <summary>
    /// Tempo mínimo entre tiros, em segundos. Só hitscan.
    /// </summary>
    /// <remarks>
    /// Dividido por <c>AttackSpeed</c> do <c>StatBlock</c> a cada tiro, nunca
    /// editado em código: é assim que Overdrive (M4) acelera a cadência da
    /// pistoleira sem tocar neste `.tres`. Ver spec 07 §5 e o ticket 09.
    /// </remarks>
    [Export(PropertyHint.Range, "0.05,3,0.01")] public float AttackInterval { get; set; } = 0.3f;

    /// <summary>Cartuchos no tambor antes de recarregar. Só hitscan.</summary>
    [Export(PropertyHint.Range, "1,20,1")] public int MagazineSize { get; set; } = 6;

    /// <summary>Tempo de recarga automática ao esvaziar o tambor, em segundos. Só hitscan.</summary>
    [Export(PropertyHint.Range, "0.1,5,0.05")] public float ReloadTime { get; set; } = 1.6f;

    /// <summary>Meia-abertura do cone de dispersão aleatória do tiro, em graus. Só hitscan.</summary>
    [Export(PropertyHint.Range, "0,15,0.1")] public float SpreadDegrees { get; set; } = 1.5f;

    /// <summary>
    /// Duração do congelamento local ao conectar, em segundos. Só hitscan —
    /// corpo a corpo usa o valor por passo em <see cref="MeleeComboStep"/>.
    /// </summary>
    /// <remarks>Ver ticket 11. Aplicado aos dois envolvidos — quem atirou e quem apanhou.</remarks>
    [Export(PropertyHint.Range, "0,0.3,0.01")] public float HitstopSeconds { get; set; } = 0.04f;
}
