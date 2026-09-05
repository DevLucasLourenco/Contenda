using Godot;

namespace Contenda.Components.Mana;

/// <summary>
/// Parâmetros de mana de uma entidade.
/// </summary>
/// <remarks>
/// Mana máxima e regeneração são balanceamento, e balanceamento mora em
/// `.tres`, nunca em `.cs` — regra 4 do CLAUDE.md. Ver spec 04 §4.
/// </remarks>
[GlobalClass]
public sealed partial class ManaDefinition : Resource
{
    /// <summary>Mana máxima inicial.</summary>
    [Export(PropertyHint.Range, "0,500,1")] public float MaxMana { get; set; } = 100f;

    /// <summary>Regeneração, em mana por segundo.</summary>
    [Export(PropertyHint.Range, "0,50,0.5")] public float RegenPerSecond { get; set; } = 5f;

    /// <summary>
    /// Por quanto tempo a regeneração pausa depois de qualquer gasto, em segundos.
    /// </summary>
    /// <remarks>
    /// Sem a pausa, gastar mana não teria custo real — a barra voltaria a subir
    /// no mesmo quadro. Ver ticket 10.
    /// </remarks>
    [Export(PropertyHint.Range, "0,5,0.05")] public float RegenDelayAfterSpend { get; set; } = 1.0f;

    /// <summary>Fração de mana ao nascer, de 0 a 1.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float StartingManaPercent { get; set; } = 1f;
}
