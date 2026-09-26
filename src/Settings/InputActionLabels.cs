using System.Collections.Generic;

namespace Contenda.Settings;

/// <summary>Os nomes das ações como aparecem na tela de controles. Sem engine.</summary>
/// <remarks>
/// A lista de ações vem do `InputMap` (a tela não conhece nenhuma delas); isto
/// só dá um nome legível às que já existem, e uma ação NOVA aparece sozinha,
/// com o próprio id embelezado, até alguém dar um rótulo a ela.
/// </remarks>
public static class InputActionLabels
{
    private static readonly Dictionary<string, string> Rotulos = new()
    {
        ["move_up"] = "Mover para cima",
        ["move_down"] = "Mover para baixo",
        ["move_left"] = "Mover para a esquerda",
        ["move_right"] = "Mover para a direita",
        ["attack_basic"] = "Ataque básico",
        ["command_confirm"] = "Confirmar habilidade",
        ["form_prev"] = "Forma anterior",
        ["form_next"] = "Próxima forma",
        ["form_activate"] = "Ativar forma",
        ["jump"] = "Pular",
        ["dash"] = "Esquiva",
    };

    public static string For(string acao)
    {
        System.ArgumentNullException.ThrowIfNull(acao);

        if (Rotulos.TryGetValue(acao, out var rotulo))
            return rotulo;

        var texto = acao.Replace('_', ' ');
        return texto.Length == 0 ? acao : char.ToUpperInvariant(texto[0]) + texto[1..];
    }
}
