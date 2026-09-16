using Contenda.Characters.Base;
using Contenda.Core;
using Godot;

namespace Contenda.Input;

/// <summary>
/// Traduz teclado e mouse em intenção. O único lugar do projeto que lê
/// <c>Input</c>.
/// </summary>
/// <remarks>
/// Roda em <c>_PhysicsProcess</c> de propósito: ler <c>IsActionJustPressed</c>
/// em <c>_Process</c> **engole entradas** quando o framerate é alto — um dos
/// bugs previsíveis da spec 15 §5.
///
/// Ele **não** atualiza a mira. Só reporta onde o cursor está; quem projeta o
/// cursor no mundo é o <c>TargetingComponent</c>, chamado pelo contêiner na
/// ordem documentada na spec 01 §6. Fazer isso aqui esconderia um componente
/// dentro de outro.
/// </remarks>
public sealed partial class PlayerInputController : Node, ICharacterComponent
{
    private Viewport? _viewport;

    /// <summary>A intenção deste tique.</summary>
    public IntentFrame Current { get; private set; } = IntentFrame.Idle;

    public void Bind(CharacterContext contexto)
    {
        // Cacheado no Bind: buscar o viewport a cada quadro é lookup de nó em
        // caminho crítico, proibido pelas convenções §5.
        _viewport = contexto.Body.GetViewport();
    }

    public void Configure(CharacterDefinition definicao)
    {
    }

    /// <summary>
    /// Vazio de propósito: o jogador nunca é reciclado pelo pool de inimigos
    /// (ticket 25) -- só existe para satisfazer o contrato de
    /// <see cref="ICharacterComponent"/>, que vale para todo mundo.
    /// </summary>
    public void ResetForSpawn()
    {
    }

    /// <summary>Lê os controles e monta a intenção bruta, sem mira resolvida.</summary>
    public IntentFrame Poll()
    {
        if (_viewport is null)
            return IntentFrame.Idle;

        var scroll = 0;
        if (Godot.Input.IsActionJustPressed(InputActions.FormPrev)) scroll -= 1;
        if (Godot.Input.IsActionJustPressed(InputActions.FormNext)) scroll += 1;

        Current = new IntentFrame(
            Move: Godot.Input.GetVector(
                InputActions.MoveLeft, InputActions.MoveRight,
                InputActions.MoveUp, InputActions.MoveDown),
            ScreenPointer: _viewport.GetMousePosition(),
            AimPoint: Vector3.Zero,
            AimDirection: Vector3.Zero,
            HasAim: false,
            AttackPressed: Godot.Input.IsActionJustPressed(InputActions.AttackBasic),
            AttackHeld: Godot.Input.IsActionPressed(InputActions.AttackBasic),
            ConfirmPressed: Godot.Input.IsActionJustPressed(InputActions.CommandConfirm),
            CommandUpPressed: Godot.Input.IsActionJustPressed(InputActions.MoveUp),
            CommandDownPressed: Godot.Input.IsActionJustPressed(InputActions.MoveDown),
            CommandLeftPressed: Godot.Input.IsActionJustPressed(InputActions.MoveLeft),
            CommandRightPressed: Godot.Input.IsActionJustPressed(InputActions.MoveRight),
            FormScrollDelta: scroll,
            FormActivatePressed: Godot.Input.IsActionJustPressed(InputActions.FormActivate),
            JumpPressed: Godot.Input.IsActionJustPressed(InputActions.Jump),
            DashPressed: Godot.Input.IsActionJustPressed(InputActions.Dash));

        return Current;
    }
}
