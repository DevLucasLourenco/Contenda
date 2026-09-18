using Contenda.Core;
using Godot;

namespace Contenda.Camera;

/// <summary>
/// Esconde por transparência qualquer cenário entre a câmera e o alvo.
/// </summary>
/// <remarks>
/// Spec 02 §6 e spec 17 §4.3: "Fade dithered obrigatório em qualquer objeto
/// entre câmera e jogador... A câmera não se move para desviar; o objeto é
/// que desaparece." Sem colisão de câmera nenhuma -- é isto que preserva o
/// pilar de que o enquadramento nunca muda (spec 02 §4).
///
/// "Dithered" sai de graça do próprio motor: <see cref="BaseMaterial3D.TransparencyEnum.AlphaHash"/>
/// já é transparência por estipulagem (dithering), sem ordenação de
/// triângulos nem shader próprio -- exatamente o efeito pedido, sem escrever
/// um shader para isto.
///
/// Um raio só, não uma forma larga: mesmo idiomatismo de
/// <c>Perception.LinhaDeVisaoLivre</c> (raycast único contra
/// <see cref="PhysicsLayers.World"/>), aceitável aqui pelo mesmo motivo --
/// um raio por quadro, para UMA câmera, é uma fração do custo de todo
/// `EnemyBrain.Poll` já fazendo o mesmo contra cada inimigo.
///
/// Roda em <c>_PhysicsProcess</c>, não em <c>_Process</c>: mesma disciplina
/// de <c>HealthBar</c>/<c>HudController</c> -- <c>_Process</c> não roda de
/// forma confiável em modo headless, e as sondas deste projeto dependem de
/// rodar headless.
/// </remarks>
public sealed partial class CameraOcclusionFader : Node
{
    /// <summary>A câmera de onde o raio parte.</summary>
    [Export] public NodePath CameraPath { get; set; } = new();

    /// <summary>Quem a câmera está tentando enquadrar.</summary>
    [Export] public NodePath TargetPath { get; set; } = new();

    /// <summary>Deslocamento sobre o alvo -- mesmo motivo de <c>CameraSettings.TargetOffset</c>: mirar no torso, não nos pés.</summary>
    [Export] public Vector3 TargetOffset { get; set; } = new(0f, 1f, 0f);

    /// <summary>Camadas físicas que podem ocluir. Só cenário -- nunca personagem.</summary>
    [Export(PropertyHint.Layers3DPhysics)] public uint OcclusionMask { get; set; } = PhysicsLayers.World;

    /// <summary>Opacidade do objeto escondido. Zero seria invisível de vez; uma sombra fraca ainda lê como "tem algo ali".</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float FadedAlpha { get; set; } = 0.15f;

    private Camera3D? _camera;
    private Node3D? _alvo;
    private MeshInstance3D? _escondida;
    private StandardMaterial3D? _materialDeFade;

    public override void _Ready()
    {
        _camera = GetNodeOrNull<Camera3D>(CameraPath);
        _alvo = GetNodeOrNull<Node3D>(TargetPath);

        if (_camera is null)
        {
            GD.PushError($"{Name}: CameraPath não resolveu.");
            SetPhysicsProcess(false);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_camera is null || _alvo is null)
            return;

        var atual = MalhaOcultando(_camera.GlobalPosition, _alvo.GlobalPosition + TargetOffset);
        if (atual == _escondida)
            return;

        Restaurar();
        _escondida = atual;
        Esconder(_escondida);
    }

    /// <remarks>
    /// Fronteira com a engine: <c>Godot.Collections.Dictionary</c> só aqui,
    /// mesma disciplina de <c>Perception.LinhaDeVisaoLivre</c>.
    /// </remarks>
    private MeshInstance3D? MalhaOcultando(Vector3 origem, Vector3 alvo)
    {
        var espaco = _camera!.GetViewport().World3D.DirectSpaceState;
        var parametros = PhysicsRayQueryParameters3D.Create(origem, alvo, OcclusionMask);

        var resultado = espaco.IntersectRay(parametros);
        if (resultado.Count == 0)
            return null;

        // O raio acerta o `StaticBody3D` (a colisão), não a malha em si --
        // "Mesh" é o nome do filho visual em toda a geometria da arena
        // (Arena.tscn), o mesmo padrão que já vale para "Col". Não existe um
        // `CharacterContext` equivalente para cenário solto: a exceção de
        // caminho literal das convenções §2 vale aqui pela mesma razão que já
        // vale para `NavigationMotor.AgentPath` -- fronteira com um nó nativo,
        // sem outro jeito de alcançá-lo.
        var colisor = resultado["collider"].As<Node>();
        return colisor?.GetNodeOrNull<MeshInstance3D>("Mesh");
    }

    /// <remarks>
    /// Reaproveita a cor albedo do material ATUAL da malha, só reduzindo o
    /// alfa -- sem isto, todo objeto escondido ficaria com a MESMA cor
    /// genérica, e prédio/ônibus/andaime perderiam a própria identidade
    /// visual bem no instante em que mais precisam dela (parcialmente
    /// visíveis, não sumidos de vez).
    /// </remarks>
    private void Esconder(MeshInstance3D? malha)
    {
        if (malha is null)
            return;

        var corBase = (malha.GetActiveMaterial(0) as BaseMaterial3D)?.AlbedoColor ?? new Color(0.6f, 0.6f, 0.6f);

        _materialDeFade ??= new StandardMaterial3D { Transparency = BaseMaterial3D.TransparencyEnum.AlphaHash };
        _materialDeFade.AlbedoColor = new Color(corBase.R, corBase.G, corBase.B, FadedAlpha);

        malha.MaterialOverride = _materialDeFade;
    }

    private void Restaurar()
    {
        if (_escondida is not null)
            _escondida.MaterialOverride = null;
    }
}
