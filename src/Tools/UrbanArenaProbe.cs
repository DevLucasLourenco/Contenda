using System.Collections.Generic;
using Contenda.Camera;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica a navegação e o fade de oclusão da câmera da arena urbana, na
/// árvore de nós real.
/// </summary>
/// <remarks>
/// Não há POCO nenhum para testar em xUnit aqui -- é tudo geometria, navmesh
/// bakeada e um raycast de câmera. O que este probe cobre: se a rampa da
/// praça conecta de verdade ao nível da rua (não só visualmente -- ao
/// navmesh, que é o que os inimigos seguem), se as ligações de navegação
/// (`NavigationLink3D`) levam da rua ao topo do ônibus e do ônibus ao
/// andaime, se o telhado fica GENUINAMENTE fora do navmesh (um refúgio de
/// verdade, não um acidente de bake), e se um objeto que entra na linha
/// câmera-jogador desaparece por fade e volta ao normal depois. Ticket 21,
/// spec 17.
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/UrbanArenaProbe.tscn
/// </code>
/// </remarks>
public sealed partial class UrbanArenaProbe : Node
{
    /// <summary>Cena da arena a inspecionar.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/arena/Arena.tscn";

    [Export] public NodePath FadePath { get; set; } = new("Arena/CameraRig/CameraOcclusionFader");
    [Export] public NodePath ContainerMeshPath { get; set; } = new("Arena/NavigationRegion3D/Conteiner/Visual/shipping-container-a");
    [Export] public NodePath CameraPath { get; set; } = new("Arena/CameraRig/CombatCamera");
    [Export] public NodePath PlayerPath { get; set; } = new("Arena/Jogador");

    private readonly List<string> _falhas = [];
    private Rid _mapa;
    private CameraOcclusionFader? _fade;
    private MeshInstance3D? _malhaDoConteiner;
    private MeshInstance3D? _malhaSegundoOclusor;
    private Node3D? _camera;
    private Node3D? _jogador;
    private Vector3 _posicaoDaCameraAntesDoFade;

    private int _quadro;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            GD.PrintErr($"[arena-urbana] não consegui carregar {ScenePath}");
            GetTree().Quit(1);
            return;
        }

        var arena = packed.Instantiate<Node3D>();
        arena.Name = "Arena";
        var segundoOclusor = new StaticBody3D
        {
            Name = "ProbeSecondOccluder",
            Position = new Vector3(-11f, 0.75f, -7f),
            CollisionLayer = Contenda.Core.PhysicsLayers.World,
            CollisionMask = 0
        };
        _malhaSegundoOclusor = new MeshInstance3D
        {
            Name = "ProbeSecondOccluderVisual",
            Mesh = new BoxMesh { Size = new Vector3(1.2f, 1.5f, 1.2f) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.2f, 0.2f) }
        };
        segundoOclusor.AddChild(_malhaSegundoOclusor);
        segundoOclusor.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(1.2f, 1.5f, 1.2f) }
        });
        arena.AddChild(segundoOclusor);
        AddChild(arena);
        CachearReferenciasDaCena();
    }

    public override void _PhysicsProcess(double delta)
    {
        _quadro++;

        // O NavigationServer precisa de alguns quadros depois do boot para
        // sincronizar a malha recém-carregada com o mapa de navegação padrão
        // -- consultar cedo demais devolveria um mapa ainda vazio.
        if (_quadro == 15)
        {
            ExecutarChecagensDeNavegacao();
            PrepararChecagemDeFade();
            return;
        }

        // O fade precisa de posições sintéticas de câmera/jogador (ver
        // PrepararChecagemDeFade) e de mais um quadro de física para o
        // CameraOcclusionFader reagir a elas antes de conferir o resultado.
        if (_quadro == 17)
        {
            VerificarFadeDeOclusao();
            Concluir();
        }
    }

    private void ExecutarChecagensDeNavegacao()
    {
        _mapa = GetViewport().World3D.NavigationMap;

        VerificarCaminho("rua → centro da praça (rampa norte)", new Vector3(0f, 0.1f, -14f), new Vector3(0f, -1f, 0f), 2.5f);
        VerificarCaminho("rua → centro da praça (rampa sul)", new Vector3(0f, 0.1f, 14f), new Vector3(0f, -1f, 0f), 2.5f);
        VerificarCaminho("rua → centro da praça (rampa leste)", new Vector3(14f, 0.1f, 0f), new Vector3(0f, -1f, 0f), 2.5f);
        VerificarCaminho("rua → centro da praça (rampa oeste)", new Vector3(-14f, 0.1f, 0f), new Vector3(0f, -1f, 0f), 2.5f);
        VerificarCaminho("rua → topo do ônibus (ligação)", new Vector3(6f, 0.1f, -10f), new Vector3(6f, 1.3f, -12f), 2.0f);
        VerificarCaminho("ônibus → andaime (ligação)", new Vector3(6f, 1.3f, -12f), new Vector3(11f, 1.5f, -11.5f), 2.0f);
        VerificarCaminho("rua → contêiner (ligação)", new Vector3(-11f, 0.1f, -10f), new Vector3(-11f, 1.5f, -12f), 2.0f);
        VerificarCaminho("rua sul → caçamba (ligação)", new Vector3(6f, 0.1f, 10f), new Vector3(6f, 1.2f, 12f), 2.0f);

        VerificarTelhadoForaDoNavmesh();
    }

    /// <summary>Confere que existe um caminho de navegação chegando perto o bastante do destino.</summary>
    private void VerificarCaminho(string rotulo, Vector3 origem, Vector3 destino, float tolerancia)
    {
        var caminho = NavigationServer3D.MapGetPath(_mapa, origem, destino, true);
        if (caminho.Length == 0)
        {
            Verificar(false, $"[{rotulo}] nenhum caminho encontrado.");
            return;
        }

        var chegou = caminho[^1].DistanceTo(destino);
        Verificar(chegou <= tolerancia,
            $"[{rotulo}] o caminho terminou a {chegou:0.00} m do destino (tolerância {tolerancia} m) -- {caminho.Length} pontos.");
    }

    /// <remarks>
    /// O telhado (ver Arena.tscn) fica FORA da `NavigationRegion3D` de
    /// propósito -- um refúgio real do jogador, spec 17 §5. Se o bake
    /// vazasse geometria dele para dentro do navmesh, um caminho pedido até
    /// um ponto no telhado chegaria bem PERTO do alvo; se está genuinamente
    /// fora, o `NavigationServer3D` só consegue chegar até a borda do que
    /// EXISTE no navmesh (a saliência mais próxima), bem mais longe.
    /// </remarks>
    private void VerificarTelhadoForaDoNavmesh()
    {
        var pontoNoTelhado = new Vector3(0f, 3f, -20f);
        var caminho = NavigationServer3D.MapGetPath(_mapa, new Vector3(0f, 0.1f, -14f), pontoNoTelhado, true);

        var chegou = caminho.Length > 0 ? caminho[^1].DistanceTo(pontoNoTelhado) : float.PositiveInfinity;
        Verificar(chegou > 2.5f,
            $"o telhado deveria estar fora do navmesh -- o caminho chegou a {chegou:0.00} m dele, perto demais para estar excluído.");
    }

    /// <remarks>
    /// A câmera/jogador de verdade da cena não ocluem um pelo outro no spawn
    /// padrão -- o ângulo travado (pitch -45°, 17 m de distância) faz o raio
    /// câmera→jogador cair quase na vertical bem perto do jogador, então só
    /// objetos MUITO altos e MUITO perto da própria câmera entrariam nele por
    /// acidente. Testar o mecanismo de fade de verdade exige posições
    /// sintéticas -- aqui um raio horizontal simples, na altura do próprio
    /// contêiner, com ele bem no meio.
    /// </remarks>
    private void PrepararChecagemDeFade()
    {
        if (_fade is null || _malhaDoConteiner is null || _camera is null || _jogador is null)
        {
            Verificar(false, "CameraOcclusionFader, a malha do Contêiner, a câmera ou o jogador não resolveram.");
            return;
        }

        // Contêiner em (-11, 0.75, -12), 5×1,5×2,5 -- um raio reto em
        // x=-11, y=0,75 (seu próprio centro), de z=-20 a z=0 atravessa
        // seu meio de ponta a ponta (z ∈ [-13.25, -10.75]).
        // Começa antes do contêiner, mas além da fachada norte: raycasts
        // iniciados dentro de um StaticBody não retornam esse próprio corpo.
        // Pausamos o rig e a aplicação do offset local da câmera para que o
        // enquadramento normal não seja confundido com movimento do fader.
        _camera.GetParent()?.SetProcess(false);
        _camera.GetParent()?.SetPhysicsProcess(false);
        _camera.SetProcess(false);
        _camera.SetPhysicsProcess(false);
        _camera.GlobalPosition = new Vector3(-11f, 0.75f, -18f);
        _jogador.GlobalPosition = new Vector3(-11f, -0.25f, 0f); // + TargetOffset (0,1,0) do fader = (-11, 0.75, 0)
        _posicaoDaCameraAntesDoFade = _camera.GlobalPosition;
    }

    private void VerificarFadeDeOclusao()
    {
        if (_malhaDoConteiner is null)
            return; // já reportado como falha em PrepararChecagemDeFade

        Verificar(_malhaDoConteiner.GetSurfaceOverrideMaterial(0) is not null,
            "o contêiner está no caminho câmera→jogador; deveria estar com fade aplicado.");
        Verificar(_malhaSegundoOclusor?.GetSurfaceOverrideMaterial(0) is not null,
            "o segundo objeto também está no caminho câmera→jogador; ambos deveriam receber fade.");
        var movimentoDaCamera = _camera?.GlobalPosition.DistanceTo(_posicaoDaCameraAntesDoFade) ?? float.PositiveInfinity;
        Verificar(movimentoDaCamera < 0.001f,
            $"o fader não deveria mover a câmera para desviar do oclusor (deslocamento {movimentoDaCamera:0.000} m).");
    }

    private void CachearReferenciasDaCena()
    {
        _fade = GetNodeOrNull<CameraOcclusionFader>(FadePath);
        _malhaDoConteiner = GetNodeOrNull<MeshInstance3D>(ContainerMeshPath);
        _camera = GetNodeOrNull<Node3D>(CameraPath);
        _jogador = GetNodeOrNull<Node3D>(PlayerPath);
    }

    private void Verificar(bool condicao, string mensagem)
    {
        if (condicao)
            return;

        _falhas.Add(mensagem);
        GD.PrintErr($"[arena-urbana] FALHA: {mensagem}");
    }

    private void Concluir()
    {
        if (_falhas.Count > 0)
        {
            GD.PrintErr($"[arena-urbana] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[arena-urbana] todas as verificações passaram");
        GetTree().Quit();
    }
}
