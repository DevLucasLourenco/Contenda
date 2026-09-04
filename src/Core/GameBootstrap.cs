using Godot;

namespace Contenda.Core;

/// <summary>
/// Ponto de partida do jogo. Roda depois de todos os outros autoloads.
/// </summary>
/// <remarks>
/// A ordem de registro importa: autoloads recebem <c>_Ready</c> na ordem em que
/// aparecem em Project Settings, e este aqui é o único que **usa** os outros.
/// Por isso ele é o último, e não o primeiro — registrá-lo antes faria
/// <see cref="ServiceLocator"/> devolver nulo no exato momento do boot.
///
/// Aplicar configurações salvas e validar o conteúdo <c>.tres</c> entram aqui
/// no M7 e no M3 respectivamente.
/// </remarks>
public sealed partial class GameBootstrap : Node
{
    public override void _Ready()
    {
        GD.Print($"[boot] {ProjectInfo.Describe()}");
        GD.Print("[boot] GameBootstrap pronto — todos os serviços no ar");
    }
}
