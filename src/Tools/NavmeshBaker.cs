using System.Linq;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Bakeia a malha de navegação de uma cena e salva o resultado em disco.
/// </summary>
/// <remarks>
/// Existe porque a navmesh precisa ser **commitada** — sem ela versionada, cada
/// máquina bakeia a sua e os inimigos andam diferente em cada checkout.
///
/// Rode assim, a partir da raiz do projeto:
/// <code>
/// godot --headless --path . --scene res://scenes/debug/BakeNavmesh.tscn
/// </code>
///
/// É ferramenta, não gameplay: nunca entra no fluxo do jogo. Refazer o bake é
/// obrigatório sempre que a geometria da arena mudar — é o passo que todo mundo
/// esquece, e o sintoma é inimigo andando dentro de parede.
/// </remarks>
public sealed partial class NavmeshBaker : Node
{
    /// <summary>Cena cuja navegação será bakeada.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/arena/Arena.tscn";

    /// <summary>Onde salvar a malha resultante.</summary>
    [Export(PropertyHint.SaveFile, "*.tres")]
    public string OutputPath { get; set; } = "res://data/navmesh/arena_nav.tres";

    public override void _Ready()
    {
        var codigo = Bake() ? 0 : 1;
        GetTree().Quit(codigo);
    }

    private bool Bake()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            GD.PrintErr($"[navmesh] não consegui carregar {ScenePath}");
            return false;
        }

        var raiz = packed.Instantiate();
        AddChild(raiz);

        var regiao = Encontrar(raiz);
        if (regiao is null)
        {
            GD.PrintErr("[navmesh] a cena não tem NavigationRegion3D");
            raiz.QueueFree();
            return false;
        }

        var malha = regiao.NavigationMesh;
        if (malha is null)
        {
            GD.PrintErr("[navmesh] a NavigationRegion3D não tem NavigationMesh configurada");
            raiz.QueueFree();
            return false;
        }

        GD.Print($"[navmesh] bakeando {ScenePath}…");
        regiao.BakeNavigationMesh(false);

        var poligonos = malha.GetPolygonCount();
        var vertices = malha.GetVertices();
        GD.Print($"[navmesh] {poligonos} polígonos, {vertices.Length} vértices");

        // Imprime os parâmetros do agente porque eles NÃO aparecem no .tres
        // quando coincidem com o padrão da engine — e "ausente" é
        // indistinguível de "esquecido" ao ler o arquivo commitado.
        GD.Print($"[navmesh] agente: raio {malha.AgentRadius} m, altura " +
                 $"{malha.AgentHeight} m, rampa máx {malha.AgentMaxSlope}°");

        // Faixas de altura navegáveis. É o que prova que a rampa conectou a
        // plataforma: uma rampa que não encosta produz navegação só no nível do
        // chão, e o sintoma só aparece no M5, como inimigo que nunca sobe.
        //
        // Topos de obstáculo também viram faixa — são ilhas inalcançáveis,
        // inofensivas para o caminho mas boas de enxergar.
        var faixas = new System.Collections.Generic.SortedDictionary<int, int>();
        foreach (var v in vertices)
        {
            var chave = Mathf.RoundToInt(v.Y * 10f);
            faixas[chave] = faixas.TryGetValue(chave, out var n) ? n + 1 : 1;
        }

        foreach (var (chave, n) in faixas)
            GD.Print($"[navmesh] altura {chave / 10f:0.0} m — {n} vértices");

        if (poligonos == 0)
        {
            // Falhar aqui, e não gravar uma malha vazia: navmesh vazia faz todo
            // inimigo ficar parado, e o sintoma não aponta para o bake.
            GD.PrintErr("[navmesh] bake produziu ZERO polígonos — nada foi salvo");
            raiz.QueueFree();
            return false;
        }

        var erro = ResourceSaver.Save(malha, OutputPath);
        raiz.QueueFree();

        if (erro != Error.Ok)
        {
            GD.PrintErr($"[navmesh] falha ao salvar em {OutputPath}: {erro}");
            return false;
        }

        GD.Print($"[navmesh] salvo em {OutputPath}");
        return true;
    }

    /// <remarks>
    /// <c>GetChildren()</c> devolve <c>Godot.Collections.Array</c>. As convenções
    /// §2 permitem a exceção na fronteira com a API da engine; e LINQ aqui é
    /// aceitável porque isto é ferramenta de linha de comando, não caminho
    /// crítico de quadro.
    /// </remarks>
    private static NavigationRegion3D? Encontrar(Node no)
        => no as NavigationRegion3D
           ?? no.GetChildren().Select(Encontrar).FirstOrDefault(r => r is not null);
}
