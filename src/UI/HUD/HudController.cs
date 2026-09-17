using Contenda.Characters.Base;
using Contenda.Core;
using Godot;

namespace Contenda.UI.HUD;

/// <summary>
/// Liga o HUD ao jogador — uma vez — e distribui as referências.
/// </summary>
/// <remarks>
/// Nenhum widget procura o personagem por conta própria: só este controlador
/// resolve o <see cref="CharacterController"/> do jogador, e repassa
/// <c>HealthComponent</c>/<c>ManaComponent</c> para quem precisa. Ver
/// ticket 12 e spec 01 §4 — é o mesmo motivo pelo qual componentes de
/// personagem não procuram uns aos outros por caminho, aplicado ao HUD.
///
/// Encontra o jogador pelo grupo <see cref="NodeGroups.Player"/>, não por
/// <c>NodePath</c>: este nó é adicionado à árvore pelo <c>GameBootstrap</c>
/// (um autoload), fora da cena do nível — instanciar o HUD dentro de
/// `Arena.tscn` reprovaria o verificador anti-2D (uma cena de `scenes/ui/`
/// dentro do mundo). Como autoload e nível vivem em partes desconectadas da
/// árvore, e o nível pode nem ter carregado ainda quando este `_Ready` roda,
/// a busca tenta de novo a cada quadro até encontrar — uma vez encontrado,
/// vincula e para de procurar.
///
/// Também guarda a tecla de debug que alterna o arquétipo do jogador em
/// runtime (ticket 12): existe para acelerar o teste dos dois personagens, e
/// só funciona em build de debug.
/// </remarks>
public sealed partial class HudController : CanvasLayer
{
    /// <summary>A barra de vida a alimentar.</summary>
    [Export] public NodePath HealthBarPath { get; set; } = new();

    /// <summary>A barra de mana a alimentar.</summary>
    [Export] public NodePath ManaBarPath { get; set; } = new();

    /// <summary>O guia de combos a alimentar.</summary>
    [Export] public NodePath AbilityGuidePath { get; set; } = new();

    /// <summary>A barra do chefe a alimentar.</summary>
    [Export] public NodePath BossBarPath { get; set; } = new();

    /// <summary>
    /// Arquétipos alternáveis pela tecla de debug, nesta ordem.
    /// </summary>
    /// <remarks>
    /// `.tres` no Export, não nome de personagem no código — trocar de
    /// arquétipo nunca é um `if` sobre quem é o personagem.
    /// </remarks>
    [Export] public CharacterDefinition[] DebugArchetypes { get; set; } = [];

    private HealthBar? _barraDeVida;
    private ManaBar? _barraDeMana;
    private AbilityGuide? _guiaDeHabilidades;
    private BossHealthBar? _barraDoChefe;
    private CharacterController? _jogador;
    private CharacterController? _chefeAtual;
    private int _indiceArquetipo;

    public override void _Ready()
    {
        _barraDeVida = GetNodeOrNull<HealthBar>(HealthBarPath);
        _barraDeMana = GetNodeOrNull<ManaBar>(ManaBarPath);
        _guiaDeHabilidades = GetNodeOrNull<AbilityGuide>(AbilityGuidePath);
        _barraDoChefe = GetNodeOrNull<BossHealthBar>(BossBarPath);

        if (_barraDeVida is null || _barraDeMana is null || _guiaDeHabilidades is null || _barraDoChefe is null)
        {
            // Falhar alto: um HUD "quase ligado" pareceria funcionar e nunca
            // atualizaria nada. Convenções §9.
            GD.PushError($"{Name}: HealthBarPath, ManaBarPath, AbilityGuidePath ou BossBarPath não resolveram.");
            SetPhysicsProcess(false);
        }
    }

    /// <remarks>
    /// Em <c>_PhysicsProcess</c>, não em <c>_Process</c>: convenções §4 —
    /// ler <c>IsActionJustPressed</c> em <c>_Process</c> engole entradas em
    /// framerate alto, o mesmo motivo pelo qual <c>PlayerInputController</c>
    /// lê em <c>_PhysicsProcess</c>. A troca de personagem só roda em build
    /// de debug: a spec 07 §1 e o ticket 12 tratam isto como ferramenta de
    /// teste, não ação de gameplay — ver <c>CombatCamera</c> para o mesmo
    /// padrão de checagem.
    /// </remarks>
    public override void _PhysicsProcess(double delta)
    {
        if (_jogador is null)
            ProcurarJogador();

        // Ao contrário do jogador (procurado uma vez e nunca mais), o chefe
        // pode nascer e morrer no meio da partida -- precisa checar todo
        // quadro, não só até achar a primeira vez.
        AtualizarChefe();

        if (_jogador is null)
            return;

        // Godot.Input, plenamente qualificado: Contenda.Input (o namespace de
        // IntentFrame) sombreia o nome, mesma armadilha do PlayerInputController.
        if (OS.IsDebugBuild() && Godot.Input.IsActionJustPressed(InputActions.DebugSwitchCharacter))
            AlternarArquetipo();
    }

    /// <remarks>
    /// Uma varredura por grupo por quadro de física, só enquanto o jogador
    /// ainda não apareceu — geralmente 1-2 quadros no boot. Depois de
    /// encontrado, este método nunca mais roda.
    /// </remarks>
    private void ProcurarJogador()
    {
        var encontrado = GetTree().GetFirstNodeInGroup(NodeGroups.Player);
        if (encontrado is not CharacterController jogador
            || jogador.Context is not { Health: { } vida, Mana: { } mana, Abilities: { } habilidades })
            return;

        _jogador = jogador;
        _barraDeVida?.Bind(vida);
        _barraDeMana?.Bind(mana);
        _guiaDeHabilidades?.Bind(habilidades, mana);
    }

    /// <remarks>
    /// Lê <c>GameSession.BossBody</c> (o inimigo se anuncia, ticket 26) --
    /// nunca <c>GetNodesInGroup</c> por quadro, proibido pelas convenções §5.
    /// Ao contrário de <see cref="ProcurarJogador"/>, roda TODO quadro: o
    /// chefe pode morrer (o campo some) ou nascer bem depois do jogador, e a
    /// barra precisa reagir aos dois sentidos, não só aparecer uma vez.
    /// </remarks>
    private void AtualizarChefe()
    {
        var chefe = ServiceLocator.Session.BossBody;
        var vivo = chefe is not null && GodotObject.IsInstanceValid(chefe) && chefe.Context?.Health is { IsAlive: true };

        if (!vivo)
        {
            if (_chefeAtual is not null)
            {
                _barraDoChefe?.Unbind();
                _chefeAtual = null;
            }

            return;
        }

        if (ReferenceEquals(chefe, _chefeAtual))
            return;

        // `!` de `chefe`/`.Context`/`.Health`: garantidos pela variável `vivo`
        // acima, que só chega `true` com os três não nulos -- o compilador não
        // enxerga essa relação através da variável local, mas o guard já
        // aconteceu.
        _chefeAtual = chefe;
        _barraDoChefe?.Bind(chefe!.Context!.Health!, chefe.Definition?.DisplayName ?? "Chefe");
    }

    /// <remarks>
    /// Reconfigura o MESMO <c>CharacterController</c> em vez de trocar de
    /// cena: os componentes (e as referências que as barras já seguram) não
    /// mudam de identidade, só os dados que carregam. Ver
    /// <c>CharacterController.SwitchDefinition</c>.
    /// </remarks>
    private void AlternarArquetipo()
    {
        if (DebugArchetypes.Length == 0)
            return;

        _indiceArquetipo = (_indiceArquetipo + 1) % DebugArchetypes.Length;

        // `!`: só chamado a partir do _PhysicsProcess, no ramo onde _jogador já não é nulo.
        _jogador!.SwitchDefinition(DebugArchetypes[_indiceArquetipo]);
    }
}
