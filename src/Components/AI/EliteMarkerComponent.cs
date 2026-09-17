using Contenda.Characters.Base;
using Godot;

namespace Contenda.Components.AI;

/// <summary>
/// Tinge a malha do corpo se o inimigo é uma elite, para se distinguir de
/// longe além do tamanho da própria barra de vida.
/// </summary>
/// <remarks>
/// Sem modelo/textura de verdade ainda (ADR-010): o placeholder de hoje é um
/// tingimento emissivo permanente na cápsula, via
/// <c>MeshInstance3D.MaterialOverride</c> -- mesma técnica de
/// <c>DamageFlashComponent</c>/<c>AttackTelegraphComponent</c>. A diferença é
/// que este é PERMANENTE, não um pulso: por isso publica o próprio material
/// em <see cref="CharacterContext.BodyBaseMaterial"/>, para que os outros
/// dois "desliguem" restaurando a ele em vez de a <c>null</c> -- sem isto, o
/// primeiro flash de dano ou windup de ataque apagaria o tingimento de uma
/// elite para sempre. Ver ticket 26, spec 09 §5 e §7 (elite dourado, spec
/// 11 §4).
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
    private StandardMaterial3D? _materialElite;

    /// <summary>Se este inimigo é uma elite. Para o probe/depuração.</summary>
    public bool IsElite { get; private set; }

    public override void _Ready()
    {
        _malha = GetNodeOrNull<MeshInstance3D>(MeshPath);
        if (_malha is null)
            GD.PushError($"{Name}: MeshPath não resolveu.");
    }

    public void Bind(CharacterContext contexto) => _contexto = contexto;

    /// <remarks>
    /// <c>EnemyBrain.Definition</c> só é lido AQUI, na segunda passada do
    /// contêiner, nunca em <c>Bind</c> -- mesmo motivo documentado em
    /// <c>EnemyBrain.Configure</c>: a primeira passada não garante ordem
    /// entre nós irmãos.
    /// </remarks>
    public void Configure(CharacterDefinition definicao) => AplicarOuLimpar();

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
    public void ResetForSpawn() => AplicarOuLimpar();

    private void AplicarOuLimpar()
    {
        var def = _contexto?.EnemyBrain?.Definition;
        IsElite = def?.IsElite ?? false;

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
        _materialElite ??= new StandardMaterial3D
        {
            AlbedoColor = def!.EliteTint,
            EmissionEnabled = true,
            Emission = def.EliteTint,
            EmissionEnergyMultiplier = 0.8f,
        };

        _contexto.BodyBaseMaterial = _materialElite;
        _malha.MaterialOverride = _materialElite;
    }
}
