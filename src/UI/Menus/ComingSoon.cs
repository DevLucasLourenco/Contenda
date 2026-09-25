using Godot;

namespace Contenda.UI.Menus;

/// <summary>A regra de "ainda não existe": o botão fica visível, desabilitado e fora do foco do teclado.</summary>
/// <remarks>
/// Uma regra só, usada por todo botão que aponta para algo dos próximos
/// tickets (modos futuros, configurações, trocar personagem, menu principal
/// antes de existir) -- spec 11 §1, "Em breve". Sem o foco desligado, o Godot
/// deixa a seta do teclado parar num botão que não faz nada.
/// </remarks>
public static class ComingSoon
{
    public static void Apply(Button botao)
    {
        botao.Disabled = true;
        botao.TooltipText = "Em breve";
        botao.FocusMode = Control.FocusModeEnum.None;
    }
}
