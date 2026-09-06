using Contenda.Characters.Base;
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
/// Sem som: o projeto ainda não tem nenhum sistema de áudio (`AudioDirector`
/// é um autoload vazio) -- fica para o ticket 36 (M8), que já lista telegrafia
/// sonora na própria spec.
/// </remarks>
public sealed partial class AttackTelegraphComponent : Node, ICharacterComponent
{
    /// <summary>A malha a acender.</summary>
    [Export] public NodePath MeshPath { get; set; } = new();

    /// <summary>Cor emissiva da telegrafia.</summary>
    [Export] public Color TelegraphColor { get; set; } = new(1f, 0.35f, 0.1f);

    private MeshInstance3D? _malha;
    private StandardMaterial3D? _materialAviso;

    /// <summary>Se o aviso está aceso agora. Para o probe/depuração.</summary>
    public bool IsWarning { get; private set; }

    public override void _Ready()
    {
        _malha = GetNodeOrNull<MeshInstance3D>(MeshPath);
        if (_malha is null)
            GD.PushError($"{Name}: MeshPath não resolveu.");
    }

    /// <remarks>
    /// Vazio de propósito: ver o comentário equivalente em <c>NavigationMotor.Bind</c>
    /// -- quem me registra em <c>CharacterContext.AttackTelegraph</c> é o
    /// <c>Registrar</c> do <c>CharacterController</c>, antes de qualquer <c>Bind</c> rodar.
    /// </remarks>
    public void Bind(CharacterContext contexto)
    {
    }

    public void Configure(CharacterDefinition definicao)
    {
    }

    /// <summary>Acende o aviso. Chamado ao entrar no windup do golpe.</summary>
    public void LigarAviso()
    {
        IsWarning = true;

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
    public void DesligarAviso()
    {
        IsWarning = false;

        if (_malha is not null)
            _malha.MaterialOverride = null;
    }

    /// <summary>Devolve ao estado de recém-criado. Contrato do pool, no M5.</summary>
    public void ResetForSpawn() => DesligarAviso();
}
