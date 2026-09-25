using System;
using System.Collections.Generic;
using Godot;

namespace Contenda.UI.Menus;

/// <summary>A escolha do modo de jogo. Só o horda é jogável; os outros aparecem, desabilitados, como "EM BREVE". Spec 11 §3.2, ticket 30.</summary>
/// <remarks>
/// Mostrar o que ainda não existe custa nada e comunica a ambição do projeto:
/// o jogador entende que o horda é o começo, não o produto inteiro. Esta tela
/// não sabe carregar nada -- avisa qual modo foi escolhido e quem a abriu
/// decide o que fazer com isso.
/// </remarks>
public sealed partial class GameModeMenu : Control
{
    /// <summary>Id do modo jogável, o mesmo que <c>IGameMode.Id</c> devolve.</summary>
    public const string HordeModeId = "horde";

    [Export] public NodePath HordeButtonPath { get; set; } = new();

    [Export] public NodePath BackButtonPath { get; set; } = new();

    private Button? _horda;
    private Button? _voltar;

    public event Action<string>? ModeChosen;

    public event Action? BackRequested;

    public Button? HordeButton => _horda;

    public Button? BackButton => _voltar;

    /// <summary>Todos os cartões de modo (sem o "voltar"), jogáveis ou não, na ordem da tela. Para o probe/depuração.</summary>
    public List<Button> Cards
    {
        get
        {
            var cartoes = new List<Button>();
            foreach (var no in FindChildren("*", "Button", recursive: true, owned: false))
            {
                if (no is Button botao && botao != _voltar)
                    cartoes.Add(botao);
            }

            return cartoes;
        }
    }

    public override void _Ready()
    {
        _horda = GetNodeOrNull<Button>(HordeButtonPath);
        _voltar = GetNodeOrNull<Button>(BackButtonPath);

        if (_horda is null || _voltar is null)
        {
            GD.PushError($"{Name}: HordeButtonPath ou BackButtonPath não resolveram.");
            return;
        }

        // As setas vão de um modo jogável a outro, sem parar num cartão "EM BREVE".
        foreach (var cartao in Cards)
        {
            if (cartao.Disabled)
                ComingSoon.Apply(cartao);
        }

        _horda.Pressed += () => ModeChosen?.Invoke(HordeModeId);
        _voltar.Pressed += () => BackRequested?.Invoke();

        // "Foco inicial no Horde" (spec 11 §3.2) -- e é o que faz o teclado funcionar sem mouse.
        _horda.GrabFocus();
    }

    public override void _UnhandledInput(InputEvent evento)
    {
        if (!evento.IsActionPressed("ui_cancel"))
            return;

        GetViewport().SetInputAsHandled();
        BackRequested?.Invoke();
    }
}
