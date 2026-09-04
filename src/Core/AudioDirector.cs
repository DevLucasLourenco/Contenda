using Godot;

namespace Contenda.Core;

/// <summary>
/// Dono dos buses de áudio e, mais tarde, do pool de vozes 3D.
/// </summary>
/// <remarks>
/// O pool existe por um motivo concreto: com 40 inimigos em cena, sons soltos
/// estouram o limite de vozes e o mixer começa a cortar efeitos ao acaso. O
/// limite de 3 instâncias por som, com variação de pitch, entra no M8 junto com
/// os assets — ver docs/specs/13-assets-animacao-e-licencas.md §8.
/// </remarks>
public sealed partial class AudioDirector : Node
{
    public override void _Ready()
    {
        ServiceLocator.Register(this);
        GD.Print("[boot] AudioDirector pronto");
    }
}
