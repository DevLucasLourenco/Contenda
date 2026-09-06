using Godot;

namespace Contenda.Components.Stats;

/// <summary>
/// Decide e aplica o crítico. <see cref="Rolar"/>/<see cref="Aplicar"/> são
/// funções puras: quem chama fornece o sorteio, para poder testar sem
/// depender do RNG de verdade da engine.
/// </summary>
/// <remarks>
/// Ticket 18, spec 16 §5. O sorteio em si (<c>GD.Randf()</c>) acontece do lado
/// de fora, uma vez por golpe — nunca por alvo, para um ataque em área não
/// virar loteria (ver comentário do ticket).
///
/// <see cref="RolarNaStats"/>/<see cref="AplicarNaStats"/> existem só para não
/// repetir `Stats?.Get(...) ?? valor` e `GD.Randf()` nos ~8 pontos do projeto
/// que aplicam dano — cada um chamando a engine de verdade, por isso não
/// entram nos testes de xUnit (que cobrem <see cref="Rolar"/>/<see cref="Aplicar"/>
/// diretamente, com o sorteio controlado à mão).
/// </remarks>
public static class CritMath
{
    /// <summary>
    /// Se este sorteio conta como crítico.
    /// </summary>
    /// <param name="chance">Chance de crítico, de <see cref="StatId.CritChance"/>.</param>
    /// <param name="sorteio01">Um número em [0, 1), já sorteado por quem chama.</param>
    public static bool Rolar(float chance, float sorteio01)
    {
        if (chance <= 0f)
            return false;

        if (chance >= 1f)
            return true;

        return sorteio01 < chance;
    }

    /// <summary>Aplica o multiplicador de crítico ao dano, se o golpe saiu crítico.</summary>
    public static float Aplicar(float dano, bool critico, float multiplicador)
        => critico ? dano * multiplicador : dano;

    /// <summary>Sorteia um crítico lendo a chance direto do <see cref="StatsComponent"/> de quem golpeou.</summary>
    /// <remarks>Nulo (sem StatsComponent) vira 0% -- nunca crítico, nunca uma exceção.</remarks>
    public static bool RolarNaStats(StatsComponent? stats)
        => Rolar(stats?.Get(StatId.CritChance) ?? 0f, GD.Randf());

    /// <summary>Aplica o multiplicador de crítico lido do mesmo <see cref="StatsComponent"/>.</summary>
    public static float AplicarNaStats(StatsComponent? stats, float dano, bool critico)
        => Aplicar(dano, critico, stats?.Get(StatId.CritMultiplier) ?? 1f);
}
