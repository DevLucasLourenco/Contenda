using Contenda.Characters.Base;
using Godot;

namespace Contenda.Components.Targeting;

/// <summary>
/// Converte a posição do cursor num ponto do mundo.
/// </summary>
/// <remarks>
/// O plano de mira acompanha a **altura do torso do personagem**, e não Y=0.
/// Mirando no chão, o erro cresce com a distância: quanto mais longe o cursor,
/// mais o ponto projetado se afasta de onde o jogador acha que aponta — e com
/// câmera inclinada isso é bem perceptível.
///
/// Ver docs/specs/02-camera-e-mundo-25d.md §7.
/// </remarks>
public sealed partial class TargetingComponent : Node, ICharacterComponent
{
    /// <summary>Altura do plano de mira acima dos pés, em metros.</summary>
    [Export(PropertyHint.Range, "0,3,0.1")] public float TorsoHeight { get; set; } = 1.0f;

    private CharacterContext? _contexto;
    private Viewport? _viewport;

    /// <summary>Último ponto mirado válido.</summary>
    public Vector3 AimPoint { get; private set; }

    /// <summary>Direção horizontal até a mira. Zero enquanto não houver mira.</summary>
    public Vector3 AimDirection { get; private set; }

    /// <summary>Se já houve ao menos uma projeção válida.</summary>
    public bool HasAim { get; private set; }

    public void Bind(CharacterContext contexto)
    {
        _contexto = contexto;

        // Cacheado no Bind: buscar o viewport a cada quadro é lookup de nó em
        // caminho crítico, proibido pelas convenções §5.
        _viewport = contexto.Body.GetViewport();
    }

    public void Configure(CharacterDefinition definicao)
    {
    }

    /// <summary>
    /// Projeta o cursor no plano do torso usando a câmera ativa.
    /// </summary>
    /// <remarks>
    /// Quando o raio não cruza o plano — cursor além do horizonte — a mira
    /// anterior é mantida. Virar o personagem para um ponto inventado seria pior
    /// que não atualizar.
    ///
    /// A câmera é consultada a cada chamada, e não cacheada, porque ela troca de
    /// verdade: menu, arena e tela de resultado usam câmeras diferentes. É a
    /// única consulta por quadro aqui, e o `GetCamera3D` do viewport é um campo,
    /// não uma varredura de árvore.
    /// </remarks>
    public void UpdateFromScreen(Vector2 posicaoNaTela)
    {
        if (_contexto is null || _viewport is null)
            return;

        var camera = _viewport.GetCamera3D();
        if (camera is null)
            return;

        var origem = camera.ProjectRayOrigin(posicaoNaTela);
        var direcao = camera.ProjectRayNormal(posicaoNaTela);
        var alturaDoPlano = _contexto.Body.GlobalPosition.Y + TorsoHeight;

        if (!TargetingMath.TryProjectToPlane(origem, direcao, alturaDoPlano, out var ponto))
            return;

        AimPoint = ponto;

        var direcaoNoPlano = TargetingMath.AimDirection(_contexto.Body.GlobalPosition, ponto);
        if (direcaoNoPlano.LengthSquared() <= 0f)
            return;

        AimDirection = direcaoNoPlano;
        HasAim = true;
    }
}
