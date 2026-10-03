using Contenda.Characters.Base;
using Godot;

namespace Contenda.Components.AI;

/// <summary>
/// Marca elites com três espigões geométricos sobre a cabeça e um realce
/// material. A silhueta continua identificável sem depender da cor.
/// </summary>
/// <remarks>
/// O acabamento emissivo permanente complementa a forma da coroa, sem ser o
/// único indicador. O material base do corpo é duplicado para preservar a
/// textura do modelo. <see cref="CharacterContext.BodyBaseMaterial"/> permite
/// que os efeitos temporários de dano e ataque restaurem o acabamento da
/// elite ao terminar.
///
/// Lê <see cref="EnemyDefinition"/> via <c>CharacterContext.EnemyBrain</c>,
/// não por <c>[Export]</c> próprio: a definição de comportamento é do
/// inimigo, não deste componente, e <c>CharacterContext</c> é o único jeito
/// sancionado de alcançar outro componente. Só faz sentido em inimigos
/// (nulo para o jogador) -- vazio, não erro, quando não há nenhum.
/// </remarks>
public sealed partial class EliteMarkerComponent : Node, ICharacterComponent
{
    /// <summary>A malha a tingir.</summary>
    [Export] public NodePath MeshPath { get; set; } = new();

    private CharacterContext? _contexto;
    private MeshInstance3D? _malha;
    private MeshInstance3D? _placeholder;
    private StandardMaterial3D? _materialElite;
    private Node3D? _silhuetaElite;

    /// <summary>Se este inimigo é uma elite. Para o probe/depuração.</summary>
    public bool IsElite { get; private set; }

    /// <summary>A silhueta geométrica opcional de elite ou chefe.</summary>
    public Node3D? Silhouette => _silhuetaElite;

    public override void _Ready()
    {
        _placeholder = GetNodeOrNull<MeshInstance3D>(MeshPath);
        _malha = _placeholder;
        if (_placeholder is null)
            GD.PushError($"{Name}: MeshPath não resolveu.");
    }

    public void Bind(CharacterContext contexto) => _contexto = contexto;

    /// <remarks>
    /// <c>EnemyBrain.Definition</c> só é lido AQUI, na segunda passada do
    /// contêiner, nunca em <c>Bind</c> -- mesmo motivo documentado em
    /// <c>EnemyBrain.Configure</c>: a primeira passada não garante ordem
    /// entre nós irmãos.
    /// </remarks>
    public void Configure(CharacterDefinition definicao)
    {
        SincronizarMalhaEInsignia();
        AplicarOuLimpar();
    }

    /// <summary>
    /// Devolve ao estado de recém-criado. Contrato do pool, no M5 (ticket 25).
    /// </summary>
    /// <remarks>
    /// Reaplica em vez de só limpar: um inimigo pooled sempre reciclado como
    /// a MESMA espécie (mesma <see cref="EnemyDefinition"/>), então
    /// <see cref="IsElite"/> não muda entre reciclagens -- mas o
    /// tingimento em si (a MALHA) precisa ser reafirmado porque
    /// <c>Desativar</c>/<c>Reativar</c> do pool não tocam
    /// <c>MaterialOverride</c> nenhum, então ele já estaria certo, mas é mais
    /// barato e mais claro reafirmar do que confiar nisso.
    /// </remarks>
    public void ResetForSpawn()
    {
        SincronizarMalhaEInsignia();
        AplicarOuLimpar();
    }

    private void SincronizarMalhaEInsignia()
    {
        var modelo = _contexto?.Owner.CurrentModel;
        _malha = _contexto?.Owner.CurrentBodyMesh ?? _placeholder;

        var def = _contexto?.EnemyBrain?.Definition;
        if (modelo is null || !(def?.IsElite ?? false) && !(def?.IsBoss ?? false))
            return;

        if (_silhuetaElite is not null && GodotObject.IsInstanceValid(_silhuetaElite))
            return;

        var material = new StandardMaterial3D { AlbedoColor = new Color(0.88f, 0.72f, 0.32f), Metallic = 0.55f, Roughness = 0.35f };
        var coroa = new Node3D { Name = "EliteSilhouette" };
        var mesh = new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.13f, Height = 0.42f, RadialSegments = 5 };
        for (var index = 0; index < 3; index++)
        {
            var chifre = new MeshInstance3D { Mesh = mesh, MaterialOverride = material };
            chifre.Position = new Vector3((index - 1) * 0.28f, 1.82f + (index == 1 ? 0.14f : 0f), 0f);
            chifre.RotationDegrees = new Vector3(0f, 0f, (index - 1) * -16f);
            coroa.AddChild(chifre);
        }

        modelo.AddChild(coroa);
        _silhuetaElite = coroa;
    }

    private void AplicarOuLimpar()
    {
        var def = _contexto?.EnemyBrain?.Definition;
        IsElite = def?.IsElite ?? false;

        if (_silhuetaElite is not null && GodotObject.IsInstanceValid(_silhuetaElite))
            _silhuetaElite.Visible = IsElite || (def?.IsBoss ?? false);

        if (_malha is null || _contexto is null)
            return;

        if (!IsElite)
        {
            _contexto.BodyBaseMaterial = null;
            _malha.MaterialOverride = null;
            return;
        }

        // `!`: `IsElite` só fica `true` quando `def?.IsElite` já é `true`
        // (linha acima), e isso exige `def` não nulo -- o ramo `!IsElite`
        // logo acima já capturou o caso `def is null`.
        if (_materialElite is null)
        {
            var materialElite = _malha.GetActiveMaterial(0) is StandardMaterial3D materialBase
                ? materialBase.Duplicate() as StandardMaterial3D ?? new StandardMaterial3D()
                : new StandardMaterial3D();
            materialElite.AlbedoColor = def!.EliteTint;
            materialElite.EmissionEnabled = true;
            materialElite.Emission = def.EliteTint;
            materialElite.EmissionEnergyMultiplier = 0.8f;
            _materialElite = materialElite;
        }

        _contexto.BodyBaseMaterial = _materialElite;
        _malha.MaterialOverride = _materialElite;
    }
}
