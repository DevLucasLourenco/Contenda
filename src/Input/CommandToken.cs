namespace Contenda.Input;

/// <summary>Um símbolo de comando: uma das quatro direções do WASD.</summary>
public enum CommandDirection : byte
{
    Up,
    Down,
    Left,
    Right,
}

/// <summary>
/// Um símbolo registrado no <see cref="CommandBuffer"/>, com quando entrou.
/// </summary>
/// <param name="Direction">Qual tecla.</param>
/// <param name="TimestampSeconds">
/// Quando entrou, em segundos de um relógio monotônico qualquer — o buffer não
/// lê relógio de parede sozinho, quem chama <see cref="CommandBuffer.Push"/>
/// decide a origem do tempo. Ver spec 03 §4.
/// </param>
public readonly record struct CommandToken(CommandDirection Direction, float TimestampSeconds);
