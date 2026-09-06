using Godot;

namespace Contenda.Input;

/// <summary>
/// O que uma entidade quer fazer neste tique de física.
/// </summary>
/// <remarks>
/// **Separa "ler o teclado" de "decidir a ação"** — e é o que permite ao inimigo
/// reusar o mesmo <c>MovementComponent</c> do jogador no M5, sem uma linha de
/// locomoção duplicada: a IA preenche este mesmo tipo, sem teclado nenhum.
///
/// <c>ScreenPointer</c> só faz sentido para o jogador; a IA o deixa em zero e
/// preenche <c>AimPoint</c> diretamente.
///
/// Ver docs/specs/01-arquitetura-tecnica.md §3 e
/// docs/specs/03-input-comandos-e-combos.md §2.
/// </remarks>
/// <param name="Move">Eixo bruto do WASD. Y negativo é frente.</param>
/// <param name="ScreenPointer">Posição do cursor na tela, em pixels.</param>
/// <param name="AimPoint">Ponto do mundo para onde se olha.</param>
/// <param name="AimDirection">Direção horizontal até a mira, já normalizada.</param>
/// <param name="HasAim">Se há mira válida; falso quando o cursor passa do horizonte.</param>
/// <param name="AttackPressed">Ataque básico neste tique — a borda de subida do clique.</param>
/// <param name="AttackHeld">
/// Se o botão de ataque básico segue pressionado agora. Hitscan atira em
/// cadência enquanto for true (spec 07 §5, ticket 09); corpo a corpo ignora
/// para o combo em si, mas usa para decidir a estocada de queda — segurado
/// depois do pico do pulo, sem golpe em andamento, vira mergulho (ticket 19,
/// spec 16 §6).
/// </param>
/// <param name="ConfirmPressed">Confirmação de sequência neste tique.</param>
/// <param name="CommandUpPressed">
/// Borda de subida de W neste tique — símbolo de comando, não movimento.
/// Distinto de <see cref="Move"/>: o buffer de comandos grava tecla por
/// tecla, nunca o eixo composto, senão W+D gravaria só uma diagonal em vez de
/// dois símbolos. Ver <c>CommandBuffer</c> e o ticket 14.
/// </param>
/// <param name="CommandDownPressed">Borda de subida de S neste tique. Ver <see cref="CommandUpPressed"/>.</param>
/// <param name="CommandLeftPressed">Borda de subida de A neste tique. Ver <see cref="CommandUpPressed"/>.</param>
/// <param name="CommandRightPressed">Borda de subida de D neste tique. Ver <see cref="CommandUpPressed"/>.</param>
/// <param name="FormScrollDelta">Troca de forma selecionada: −1, 0 ou +1.</param>
/// <param name="FormActivatePressed">Ativação de forma neste tique.</param>
/// <param name="JumpPressed">Pulo neste tique. Consumido a partir do ticket 17.</param>
/// <param name="DashPressed">Avanço rápido neste tique. Consumido a partir do ticket 17.</param>
public readonly record struct IntentFrame(
    Vector2 Move,
    Vector2 ScreenPointer,
    Vector3 AimPoint,
    Vector3 AimDirection,
    bool HasAim,
    bool AttackPressed,
    bool AttackHeld,
    bool ConfirmPressed,
    bool CommandUpPressed,
    bool CommandDownPressed,
    bool CommandLeftPressed,
    bool CommandRightPressed,
    int FormScrollDelta,
    bool FormActivatePressed,
    bool JumpPressed,
    bool DashPressed)
{
    /// <summary>Intenção vazia: parado, sem mira, sem ação.</summary>
    public static IntentFrame Idle => default;
}
