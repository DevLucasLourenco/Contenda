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

    /// <summary>Meia-abertura do cone de acerto, em graus. 180 significa em volta todo.</summary>
    [Export(PropertyHint.Range, "10,180,5")] public float HalfAngle { get; set; } = 60f;

    /// <summary>Repulsão base aplicada ao alvo.</summary>
    [Export(PropertyHint.Range, "0,20,0.5")] public float Knockback { get; set; } = 2f;

    /// <summary>A cadeia de golpes. Vazia vira um golpe único com os padrões.</summary>
    [Export] public MeleeComboStep[] ComboSteps { get; set; } = [];
}
