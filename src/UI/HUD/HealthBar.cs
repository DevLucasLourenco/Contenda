using Contenda.Components.Health;
using Godot;

namespace Contenda.UI.HUD;

/// <summary>
/// Barra de vida do jogador, com camada de dano atrasada.
/// </summary>
/// <remarks>
/// **A única parte do jogo onde 2D é permitido** — <c>Control</c>, regra 1 do
/// CLAUDE.md. Sem estilização de propósito: o HUD definitivo é o M7 (ticket
/// 12, comentários).
///
/// Não procura o jogador por conta própria: o <see cref="HudController"/>
/// chama <see cref="Bind"/> uma vez, com a referência já resolvida. Ver
/// spec 01 §4 — nenhum widget deveria saber COMO encontrar o personagem, só o
/// que fazer com ele depois de receber a referência.
///
/// Lê o estado por POLLING, não por evento: a camada de dano precisa de um
/// tique de qualquer forma para a animação de esvaziamento — não há ganho em
/// somar uma assinatura de evento por cima.
///
/// **Em <c>_PhysicsProcess</c>, não em <c>_Process</c>.** A spec 01 §6
/// desenha o HUD em <c>_Process</c>; na prática, `_Process` não roda de forma
/// confiável em modo headless (`--headless`) — as sondas deste projeto
/// dependem de rodar headless, e um HUD que só atualiza em `_Process` fica
/// impossível de verificar automaticamente. `_PhysicsProcess` é determinístico
/// e já é onde toda a simulação de vida/dano acontece; ler o estado no mesmo
/// passo evita ainda uma fonte a mais de descompasso de quadro.
/// </remarks>
public sealed partial class HealthBar : Control
{
    /// <summary>O preenchimento principal, que encolhe pela esquerda.</summary>
    [Export] public NodePath FillPath { get; set; } = new();

    /// <summary>A camada de dano, atrás do preenchimento.</summary>
    [Export] public NodePath DamageLayerPath { get; set; } = new();

    /// <summary>Rótulo numérico opcional — "atual/máximo".</summary>
    [Export] public NodePath ValueLabelPath { get; set; } = new();

    /// <summary>Largura total da barra, em pixels. Precisa bater com a cena.</summary>
    [Export(PropertyHint.Range, "40,600,1")] public float BarWidth { get; set; } = 220f;

    /// <summary>Altura total da barra, em pixels.</summary>
    [Export(PropertyHint.Range, "4,60,1")] public float BarHeight { get; set; } = 18f;

    /// <summary>Quanto tempo a fatia perdida leva para esvaziar, em segundos.</summary>
    [Export(PropertyHint.Range, "0.05,2,0.05")] public float DamageLayerCatchUp { get; set; } = 0.4f;

    private Control? _preenchimento;
    private Control? _camadaDeDano;
    private Label? _valor;
    private HealthComponent? _vida;
    private DamageLayerState? _estado;
    private int _vidaExibida = int.MinValue;
    private int _vidaMaximaExibida = int.MinValue;

    public override void _Ready()
    {
        _preenchimento = GetNodeOrNull<Control>(FillPath);
        _camadaDeDano = GetNodeOrNull<Control>(DamageLayerPath);
        _valor = GetNodeOrNull<Label>(ValueLabelPath);

        if (_preenchimento is null || _camadaDeDano is null)
        {
            // Falhar alto: sem as duas camadas a barra fica muda em silêncio.
            GD.PushError($"{Name}: FillPath ou DamageLayerPath não resolveram.");
            SetPhysicsProcess(false);
        }
    }

    /// <summary>Recebe a vida a exibir. Chamado uma vez pelo <see cref="HudController"/>.</summary>
    public void Bind(HealthComponent vida)
    {
        _vida = vida;
        _estado = new DamageLayerState(vida.Percent, DamageLayerCatchUp);

        // Desenha o estado inicial na hora, sem esperar o primeiro _Process:
        // delta zero é um Tick seguro (a classe trata <= 0 como no-op) e só
        // aplica o tamanho inicial das duas camadas.
        Atualizar(0f);
    }

    /// <summary>Libera a referência quando o personagem sai da árvore.</summary>
    public void Unbind()
    {
        _vida = null;
        _estado = null;
        _vidaExibida = int.MinValue;
        _vidaMaximaExibida = int.MinValue;
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

        // Encolhe pela ESQUERDA: o tamanho sozinho encolheria pelos dois
        // lados a partir do centro, e a barra pareceria sumir para o meio em
        // vez de esvaziar de um lado — mesma armadilha da WorldHealthBar.
        _preenchimento.Size = new Vector2(BarWidth * _estado.Current, BarHeight);
        _camadaDeDano.Size = new Vector2(BarWidth * _estado.DamageLayer, BarHeight);

        if (_valor is not null)
        {
            var vidaAtual = Mathf.RoundToInt(_vida.Current);
            var vidaMaxima = Mathf.RoundToInt(_vida.Max);
            if (vidaAtual != _vidaExibida || vidaMaxima != _vidaMaximaExibida)
            {
                _valor.Text = $"{vidaAtual}/{vidaMaxima}";
                _vidaExibida = vidaAtual;
                _vidaMaximaExibida = vidaMaxima;
            }
        }
    }

    /// <summary>Fração do preenchimento principal agora. Para o probe/depuração.</summary>
    public float CurrentFraction => _estado?.Current ?? 0f;

    /// <summary>Fração da camada de dano agora. Para o probe/depuração.</summary>
    public float DamageLayerFraction => _estado?.DamageLayer ?? 0f;
}
