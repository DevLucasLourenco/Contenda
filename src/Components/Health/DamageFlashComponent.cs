using Contenda.Characters.Base;
using Godot;

namespace Contenda.Components.Health;

/// <summary>
/// Pisca a malha do personagem em branco ao levar dano.
/// </summary>
/// <remarks>
/// Só o visual — nenhuma lógica de jogo depende disto. Usa
/// <c>MeshInstance3D.MaterialOverride</c>, não o material do <c>Mesh</c>
/// direto: o material de um <c>MeshInstance3D</c> costuma ser o MESMO recurso
/// compartilhado entre instâncias da cena (os três manequins da arena usam a
/// mesma malha) — escrever nele piscaria todo mundo de uma vez.
/// <c>MaterialOverride</c> é por instância, sem precisar duplicar recurso
/// nenhum.
///
/// Sem <c>_PhysicsProcess</c> próprio, como todo componente — quem chama
/// <see cref="Tick"/> é o <c>CharacterController</c>. Ver ticket 11 e spec 01 §6.
/// </remarks>
public sealed partial class DamageFlashComponent : Node, ICharacterComponent
{
    /// <summary>A malha a piscar.</summary>
    [Export] public NodePath MeshPath { get; set; } = new();

    /// <summary>Duração do flash, em segundos.</summary>
    [Export(PropertyHint.Range, "0.01,0.5,0.01")] public float FlashDuration { get; set; } = 0.08f;

    /// <summary>Cor emissiva do flash.</summary>
    [Export] public Color FlashColor { get; set; } = Colors.White;

    private MeshInstance3D? _malha;
    private StandardMaterial3D? _materialFlash;
    private CharacterContext? _contexto;
    private HealthComponent? _vida;
    private float _restante;

    /// <summary>Se o flash está aceso agora. Para o probe/depuração.</summary>
    public bool IsFlashing => _restante > 0f;

    public override void _Ready()
    {
        _malha = GetNodeOrNull<MeshInstance3D>(MeshPath);
        if (_malha is null)
        {
            GD.PushError($"{Name}: MeshPath não resolveu.");
        }
    }

    public void Bind(CharacterContext contexto)
    {
        _contexto = contexto;

        // Desassinar antes de assinar: Bind pode rodar de novo num nó
        // reciclado pelo pool.
        if (_vida is not null)
            _vida.Damaged -= AoApanhar;

        _vida = contexto.Health;

        if (_vida is not null)
            _vida.Damaged += AoApanhar;
    }

    public void Configure(CharacterDefinition definicao)
    {
    }

    public override void _ExitTree()
    {
        if (_vida is not null)
            _vida.Damaged -= AoApanhar;
    }

    /// <summary>Envelhece o flash. Chamado pelo contêiner.</summary>
    public void Tick(float delta)
    {
        if (_restante <= 0f)
            return;

        _restante -= delta;
        if (_restante <= 0f)
        {
            _restante = 0f;
            Desligar();
        }
    }

    /// <summary>Devolve ao estado de recém-criado. Contrato do pool, no M5.</summary>
    public void ResetForSpawn()
    {
        _restante = 0f;
        Desligar();
    }

    private void AoApanhar(DamageInfo golpe)
    {
        _restante = FlashDuration;

        if (_malha is null)
            return;

        _materialFlash ??= new StandardMaterial3D
        {
            EmissionEnabled = true,
            Emission = FlashColor,
            EmissionEnergyMultiplier = 2f,
        };

        _malha.MaterialOverride = _materialFlash;
    }

    /// <remarks>
    /// <see cref="CharacterContext.RestaurarMaterialDaMalha"/>, não
    /// <c>null</c> direto: para a maioria (sem material de base) os dois são
    /// o mesmo, mas uma elite (ticket 26) tem um tingimento permanente por
    /// baixo do flash -- apagar para <c>null</c> apagaria o tingimento junto.
    /// </remarks>
    private void Desligar() => _contexto?.RestaurarMaterialDaMalha(_malha);
}
