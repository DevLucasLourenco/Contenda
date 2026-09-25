using System.IO;
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
    /// <summary>Caminho do HUD, carregado uma vez no boot.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string HudScenePath { get; set; } = "res://scenes/ui/hud/Hud.tscn";

    /// <summary>Caminho da tela de carregamento (ticket 30), adicionada à raiz como o HUD.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string LoadingScenePath { get; set; } = "res://scenes/ui/menus/LoadingScreen.tscn";

    /// <summary>Caminho da tela de resultado (ticket 29), adicionada à raiz como o HUD.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string ResultsScenePath { get; set; } = "res://scenes/ui/menus/ResultsScreen.tscn";

    public override void _Ready()
    {
        GD.Print($"[boot] {ProjectInfo.Describe()}");

        // O HUD vive fora da cena do nível de propósito: `Arena.tscn` não pode
        // instanciar uma cena de `scenes/ui/` sem reprovar o verificador
        // anti-2D (regra 1 do CLAUDE.md — nenhuma cena de interface dentro do
        // mundo). Um autoload adicionando o HUD à raiz da árvore, e o
        // HudController achando o jogador por grupo em vez de NodePath, é o
        // que mantém o HUD independente de qual nível está carregado.
        var hud = GD.Load<PackedScene>(HudScenePath);
        if (hud is null)
        {
            // Erro de CONTEÚDO: falha alto no boot em debug, só loga em
            // release -- convenções §9. Um jogador não deveria ver a build
            // inteira cair por um caminho de cena errado, mas quem está
            // editando o `.tres`/`.tscn` deveria descobrir na hora.
            var mensagem = $"{Name}: não consegui carregar o HUD em '{HudScenePath}'.";
            if (OS.IsDebugBuild())
                throw new FileNotFoundException(mensagem, HudScenePath);

            GD.PushError(mensagem);
        }
        else
        {
            // Adiado: a raiz da árvore ainda está ocupada registrando os
            // outros autoloads durante ESTE _Ready — um add_child direto aqui
            // falha com "Parent node is busy setting up children".
            GetTree().Root.CallDeferred(Node.MethodName.AddChild, hud.Instantiate());
        }

        // A tela de resultado e a de carregamento moram ao lado do HUD, pelo
        // mesmo motivo (regra 1 do CLAUDE.md) -- escondidas até serem chamadas.
        AdicionarNaRaiz(ResultsScenePath, "a tela de resultado");
        AdicionarNaRaiz(LoadingScenePath, "a tela de carregamento");

        GD.Print("[boot] GameBootstrap pronto — todos os serviços no ar");
    }

    private void AdicionarNaRaiz(string caminho, string descricao)
    {
        var cena = GD.Load<PackedScene>(caminho);
        if (cena is null)
        {
            GD.PushError($"{Name}: não consegui carregar {descricao} em '{caminho}'.");
            return;
        }

        GetTree().Root.CallDeferred(Node.MethodName.AddChild, cena.Instantiate());
    }
}
