using System;
using System.Collections.Generic;

namespace Contenda.Settings;

/// <summary>
/// Uma tecla ou botão como texto estável ("key:87", "mouse:1"), e as contas de
/// conflito entre ações. Sem engine. Spec 14 §1 ("Controles").
/// </summary>
/// <remarks>
/// O código de tecla é o <c>physical_keycode</c> do Godot (independe do layout
/// do teclado), o de mouse é o <c>button_index</c>. O arquivo de configurações
/// guarda só isto -- quem traduz de e para <c>InputEvent</c> é o
/// <c>SettingsStore</c>, o único que fala com o <c>InputMap</c>.
/// </remarks>
public static class BindingSpec
{
    public static string Key(int physicalKeycode) => "key:" + physicalKeycode;

    public static string Mouse(int buttonIndex) => "mouse:" + buttonIndex;

    /// <summary>Se o texto tem a forma "key:N" ou "mouse:N" com N inteiro positivo.</summary>
    public static bool IsValid(string spec)
    {
        var (tipo, valor) = Split(spec);
        return tipo is "key" or "mouse" && valor > 0;
    }

    public static (string Tipo, int Valor) Split(string spec)
    {
        var dois = spec.IndexOf(':');
        if (dois <= 0 || !int.TryParse(spec.AsSpan(dois + 1), out var valor))
            return ("", 0);

        return (spec[..dois], valor);
    }

    /// <summary>
    /// As teclas de cada ação valendo agora: o padrão, com o que o jogador
    /// trocou por cima. Uma ação trocada substitui o padrão inteiro (não soma).
    /// </summary>
    public static Dictionary<string, List<string>> Effective(
        IReadOnlyDictionary<string, List<string>> defaults, IReadOnlyDictionary<string, List<string>> overrides)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(overrides);

        var efetivo = new Dictionary<string, List<string>>();
        foreach (var (acao, teclas) in defaults)
            efetivo[acao] = [.. teclas];

        foreach (var (acao, teclas) in overrides)
            efetivo[acao] = [.. teclas];

        return efetivo;
    }

    /// <summary>A OUTRA ação que já usa <paramref name="spec"/>, ou nulo se ninguém usa. Ignora a própria <paramref name="acao"/>.</summary>
    public static string? FindConflict(IReadOnlyDictionary<string, List<string>> efetivo, string acao, string spec)
    {
        ArgumentNullException.ThrowIfNull(efetivo);

        foreach (var (outra, teclas) in efetivo)
        {
            if (outra != acao && teclas.Contains(spec))
                return outra;
        }

        return null;
    }

    /// <summary>
    /// Dá <paramref name="spec"/> a <paramref name="acao"/> e TIRA a tecla de quem
    /// a usava (que fica sem bind -- spec 14 §1, "a segunda ação fica sem bind").
    /// Devolve a ação que perdeu a tecla, se houve.
    /// </summary>
    public static string? Rebind(Dictionary<string, List<string>> efetivo, string acao, string spec)
    {
        ArgumentNullException.ThrowIfNull(efetivo);

        var perdeu = FindConflict(efetivo, acao, spec);
        if (perdeu is not null)
            efetivo[perdeu].Remove(spec);

        efetivo[acao] = [spec];
        return perdeu;
    }
}
