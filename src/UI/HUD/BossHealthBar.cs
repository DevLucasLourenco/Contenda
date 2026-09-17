using Contenda.Components.Health;
using Godot;

namespace Contenda.UI.HUD;

/// <summary>
/// Barra de vida própria do chefe, no alto da tela, com nome.
/// </summary>
/// <remarks>
/// Mesmo desenho de <see cref="HealthBar"/> (camada de dano, `_PhysicsProcess`
/// por causa dos probes headless -- ver o remark daquela classe), com duas
/// diferenças: tem <see cref="Bind"/> E <see cref="Unbind"/>, porque ao
/// contrário do jogador um chefe pode morrer no meio da partida; e nasce
/// escondida, porque a maior parte da partida não tem chefe nenhum. Ver
/// ticket 26.
///
/// Quem decide QUANDO vincular/desvincular é o <see cref="HudController"/>,
/// lendo <c>GameSession.BossBody</c> -- esta barra não procura o chefe por
/// conta própria, mesmo espírito de <see cref="HealthBar"/> não procurar o
/// jogador.
/// </remarks>
public sealed partial class BossHealthBar : Control
{
    /// <summary>O preenchimento principal, que encolhe pela esquerda.</summary>
    [Export] public NodePath FillPath { get; set; } = new();

    /// <summary>A camada de dano, atrás do preenchimento.</summary>
    [Export] public NodePath DamageLayerPath { get; set; } = new();

    /// <summary>O nome do chefe.</summary>
    [Export] public NodePath NameLabelPath { get; set; } = new();

    /// <summary>Largura total da barra, em pixels. Precisa bater com a cena.</summary>
    [Export(PropertyHint.Range, "40,1200,1")] public float BarWidth { get; set; } = 600f;

    /// <summary>Altura total da barra, em pixels.</summary>
    [Export(PropertyHint.Range, "4,80,1")] public float BarHeight { get; set; } = 24f;

    /// <summary>Quanto tempo a fatia perdida leva para esvaziar, em segundos.</summary>
    [Export(PropertyHint.Range, "0.05,2,0.05")] public float DamageLayerCatchUp { get; set; } = 0.4f;

    private Control? _preenchimento;
    private Control? _camadaDeDano;
    private Label? _nome;
    private HealthComponent? _vida;
    private DamageLayerState? _estado;

    public override void _Ready()
    {
        _preenchimento = GetNodeOrNull<Control>(FillPath);
        _camadaDeDano = GetNodeOrNull<Control>(DamageLayerPath);
        _nome = GetNodeOrNull<Label>(NameLabelPath);

        if (_preenchimento is null || _camadaDeDano is null || _nome is null)
        {
            // Falhar alto: sem as três partes a barra fica muda em silêncio.
            GD.PushError($"{Name}: FillPath, DamageLayerPath ou NameLabelPath não resolveram.");
            SetPhysicsProcess(false);
            return;
        }

        // Nasce escondida: sem chefe nenhum na maior parte da partida.
        Visible = false;
    }

    /// <summary>Passa a mostrar a vida de um chefe. Chamado pelo <see cref="HudController"/> quando um aparece.</summary>
    public void Bind(HealthComponent vida, string nome)
    {
        _vida = vida;
        _estado = new DamageLayerState(vida.Percent, DamageLayerCatchUp);

        if (_nome is not null)
            _nome.Text = nome;

        Visible = true;

        // Desenha o estado inicial na hora, sem esperar o primeiro Tick --
        // mesmo motivo de HealthBar.Bind.
        Atualizar(0f);
    }

    /// <summary>Some a barra. Chamado pelo <see cref="HudController"/> quando o chefe morre ou some.</summary>
    public void Unbind()
    {
        _vida = null;
        _estado = null;
        Visible = false;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_vida is null || _estado is null)
            return;

        Atualizar((float)delta);
    }

    private void Atualizar(float delta)
    {
        if (_vida is null || _estado is null || _preenchimento is null || _camadaDeDano is null)
            return;

        _estado.SetCurrent(_vida.Percent);
        _estado.Tick(delta);

        // Encolhe pela ESQUERDA: mesma armadilha documentada em HealthBar/WorldHealthBar.
        _preenchimento.Size = new Vector2(BarWidth * _estado.Current, BarHeight);
        _camadaDeDano.Size = new Vector2(BarWidth * _estado.DamageLayer, BarHeight);
    }

    /// <summary>Se há um chefe vinculado agora. Para o probe/depuração.</summary>
    public bool IsBound => _vida is not null;

    /// <summary>Fração do preenchimento principal agora. Para o probe/depuração.</summary>
    public float CurrentFraction => _estado?.Current ?? 0f;
}
