using Contenda.Characters.Base;
using Contenda.Core;
using Godot;

namespace Contenda.Components.AI;

/// <summary>
/// Acende um brilho emissivo na malha do personagem enquanto um golpe está
/// em preparação.
/// </summary>
/// <remarks>
/// Mesma técnica de <c>DamageFlashComponent</c> (ticket 07):
/// <c>MaterialOverride</c>, não o material do <c>Mesh</c> direto, porque o
/// material costuma ser um recurso COMPARTILHADO entre instâncias da cena —
/// escrever nele acenderia todo mundo de uma vez.
///
/// Ligado/desligado por chamada explícita do <c>EnemyBrain</c> (início e fim
/// do windup), não por evento: ao contrário do flash de dano, que reage a
/// algo que já aconteceu, a telegrafia é uma decisão do estado de combate,
/// que só o cérebro do inimigo tem. Ver spec 09 §7 — "todo ataque precisa
/// ser legível antes de acertar".
///
/// A apresentação sonora fica no <c>AudioDirector</c>: o <c>EnemyBrain</c>
/// publica <c>EnemyAttackWarningEvent</c> quando inicia o windup, e o áudio
/// responde a esse evento sem acoplar este componente visual ao serviço.
/// </remarks>
public sealed partial class AttackTelegraphComponent : Node, ICharacterComponent
{
    /// <summary>A malha a acender.</summary>
    [Export] public NodePath MeshPath { get; set; } = new();

    /// <summary>Cor emissiva da telegrafia.</summary>
    [Export] public Color TelegraphColor { get; set; } = new(1f, 0.35f, 0.1f);

    [Export(PropertyHint.Range, "0.5,6,0.1")] public float TelegraphRadius { get; set; } = 1.45f;

    private CharacterContext? _contexto;
    private MeshInstance3D? _malha;
    private MeshInstance3D? _placeholder;
    private StandardMaterial3D? _materialAviso;
    private MeshInstance3D? _decalAviso;

    /// <summary>Se o aviso está aceso agora. Para o probe/depuração.</summary>
    public bool IsWarning { get; private set; }

    public override void _Ready()
    {
        _placeholder = GetNodeOrNull<MeshInstance3D>(MeshPath);
        _malha = _placeholder;
        if (_placeholder is null)
            GD.PushError($"{Name}: MeshPath não resolveu.");
    }

    /// <remarks>
    /// Só guarda o contexto (para <see cref="DesligarAviso"/> restaurar o
    /// material de base de uma elite depois, ticket 26) -- quem me registra
    /// em <c>CharacterContext.AttackTelegraph</c> é o <c>Registrar</c> do
    /// <c>CharacterController</c>, antes de qualquer <c>Bind</c> rodar, o
    /// mesmo motivo do comentário equivalente em <c>NavigationMotor.Bind</c>.
    /// </remarks>
    public void Bind(CharacterContext contexto)
    {
        _contexto = contexto;

        if (_decalAviso is null)
        {
            var material = new StandardMaterial3D
            {
                AlbedoColor = new Color(TelegraphColor.R, TelegraphColor.G, TelegraphColor.B, 0.62f),
                EmissionEnabled = true,
                Emission = TelegraphColor,
                EmissionEnergyMultiplier = 1.8f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            };
            _decalAviso = new MeshInstance3D
            {
                Name = "AvisoNoChao",
                Mesh = new TorusMesh
                {
                    InnerRadius = Mathf.Max(0.1f, TelegraphRadius - 0.12f),
                    OuterRadius = TelegraphRadius,
                    RingSegments = 28,
                    Rings = 8,
                },
                MaterialOverride = material,
                Position = new Vector3(0f, 0.035f, 0f),
                Visible = false,
            };
            contexto.Body.AddChild(_decalAviso);
        }
    }

    public void Configure(CharacterDefinition definicao)
    {
        _malha = _contexto?.Owner.CurrentBodyMesh ?? _placeholder;
    }

    /// <summary>Acende o aviso. Chamado ao entrar no windup do golpe.</summary>
    public void LigarAviso()
    {
        if (IsWarning)
            return;

        IsWarning = true;
        if (_decalAviso is not null)
            _decalAviso.Visible = true;
        if (_contexto is not null)
            ServiceLocator.Events.RaiseEnemyAttackWarning(new EnemyAttackWarningEvent(_contexto.Body.GlobalPosition));

        if (_malha is null)
            return;

        _materialAviso ??= new StandardMaterial3D
        {
            EmissionEnabled = true,
            Emission = TelegraphColor,
            EmissionEnergyMultiplier = 2.5f,
        };

        _malha.MaterialOverride = _materialAviso;
    }

    /// <summary>Apaga o aviso. Chamado ao golpear de verdade ou ao cancelar.</summary>
    /// <remarks>
    /// De propósito um NO-OP se já estava apagado: <c>EnemyBrain.AoApanhar</c>
    /// chama isto INCONDICIONALMENTE em todo golpe recebido, só por garantia
    /// de cancelar um windup em andamento -- na maioria das vezes não há
    /// nenhum. Sem este guard, restaurar o material toda vez apagaria o
    /// flash de dano que <c>DamageFlashComponent</c> (assinante do MESMO
    /// evento <c>Health.Damaged</c>) acabou de acender, no mesmo despacho
    /// síncrono -- o flash nunca chegaria a aparecer.
    /// <see cref="CharacterContext.RestaurarMaterialDaMalha"/>, não
    /// <c>null</c> direto, quando HÁ de verdade o que apagar: uma elite
    /// (ticket 26) tem um tingimento permanente por baixo do aviso.
    /// </remarks>
    public void DesligarAviso()
    {
        if (!IsWarning)
            return;

        IsWarning = false;
        if (_decalAviso is not null)
            _decalAviso.Visible = false;
        _contexto?.RestaurarMaterialDaMalha(_malha);
    }

    /// <summary>Devolve ao estado de recém-criado. Contrato do pool, no M5.</summary>
    public void ResetForSpawn() => DesligarAviso();
}
