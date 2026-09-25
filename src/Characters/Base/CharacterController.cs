using System;
using System.Collections.Generic;
using Contenda.Components.Abilities;
using Contenda.Components.AI;
using Contenda.Components.Combat;
using Contenda.Components.Health;
using Contenda.Components.Mana;
using Contenda.Components.Movement;
using Contenda.Components.Stats;
using Contenda.Components.Targeting;
using Contenda.Core;
using Contenda.Input;
using Godot;

namespace Contenda.Characters.Base;

/// <summary>
/// O personagem: um contêiner de componentes, e nada mais.
/// </summary>
/// <remarks>
/// **Não há lógica de gameplay aqui**, e é intencional. Este arquivo não deve
/// crescer com o projeto: vida, mana, combate, habilidades e transformações
/// entram como componentes irmãos, não como métodos desta classe. É o que evita
/// o `Player.cs` de 4.000 linhas que afundou as versões anteriores.
///
/// Também não existe ramificação por personagem. A diferença entre Swordsman e
/// Gunslinger vive no <see cref="CharacterDefinition"/>.
/// Ver docs/specs/01-arquitetura-tecnica.md §4.
/// </remarks>
public sealed partial class CharacterController : CharacterBody3D
{
    /// <summary>Quem este personagem é.</summary>
    [Export] public CharacterDefinition? Definition { get; set; }

    /// <summary>De que lado ele está.</summary>
    [Export] public Team Team { get; set; } = Team.Player;

    private readonly List<ICharacterComponent> _componentes = [];
    private PlayerInputController? _entrada;
    private EnemyBrain? _cerebro;
    private TargetingComponent? _mira;
    private MovementComponent? _movimento;
    private HealthComponent? _vida;
    private ManaComponent? _mana;
    private CombatComponent? _combate;
    private AbilityComponent? _habilidades;
    private DamageFlashComponent? _flash;

    /// <summary>O que os componentes enxergam uns dos outros.</summary>
    public CharacterContext? Context { get; private set; }

    public override void _Ready()
    {
        if (Definition is null)
        {
            // Falhar alto: sem definição o personagem roda com valores padrão e
            // parece "quase certo", que é o pior modo de errar. Convenções §9.
            GD.PushError($"{Name}: sem CharacterDefinition. Atribua um `.tres` na cena.");
            SetPhysicsProcess(false);
            return;
        }

        Context = new CharacterContext(this, this, Team);
        ColetarComponentes(this);

        // Primeira passada: todo mundo já existe na árvore, então a ordem aqui
        // não importa.
        foreach (var componente in _componentes)
            componente.Bind(Context);

        // Segunda passada: agora sim na ordem de dependência de dados.
        foreach (var componente in _componentes)
            componente.Configure(Definition);

        // Movimento é OPCIONAL: um manequim de treino tem vida e atributos, mas
        // não anda. Exigir locomoção obrigaria a inventar um componente inútil só
        // para satisfazer o contêiner.

        // O jogador se anuncia para quem precisar encontrá-lo sem escanear a
        // árvore por quadro (ticket 22): um EnemyBrain que chamasse
        // GetNodesInGroup a cada Tick de física alocaria por quadro, proibido
        // pelas convenções §5.
        if (Team == Team.Player)
            ServiceLocator.Session.PlayerBody = this;
    }

    /// <remarks>
    /// Quem se anunciou em <c>_Ready</c> se retira ao sair: sem isto, trocar de
    /// cena (retry, menu) deixaria <c>GameSession.PlayerBody</c> apontando para
    /// um nó já liberado, e o HUD continuaria "ligado" a um jogador morto.
    /// </remarks>
    public override void _ExitTree()
    {
        if (Team == Team.Player && ReferenceEquals(ServiceLocator.Session.PlayerBody, this))
            ServiceLocator.Session.PlayerBody = null;
    }

    /// <summary>
    /// Devolve todo componente ao estado de recém-criado. Chamado pelo
    /// <c>EnemyPool</c> ao reaproveitar este inimigo (ticket 25).
    /// </summary>
    /// <remarks>
    /// Uma única passada: ao contrário de <c>Bind</c>/<c>Configure</c>,
    /// <see cref="ICharacterComponent.ResetForSpawn"/> não depende de nenhum
    /// componente irmão já reiniciado -- cada um zera só o próprio estado.
    /// </remarks>
    public void ResetForSpawn()
    {
        foreach (var componente in _componentes)
            componente.ResetForSpawn();
    }

    /// <summary>
    /// Troca a definição em runtime, reconfigurando todos os componentes.
    /// </summary>
    /// <remarks>
    /// Reaplica só a segunda passada (<c>Configure</c>) — os componentes já
    /// estão vinculados e não mudam de identidade, só os dados que carregam.
    /// É a mesma operação que uma transformação fará no M4; hoje serve à
    /// tecla de debug do ticket 12 para alternar de arquétipo sem reiniciar a
    /// cena.
    /// </remarks>
    public void SwitchDefinition(CharacterDefinition novaDefinicao)
    {
        ArgumentNullException.ThrowIfNull(novaDefinicao);

        Definition = novaDefinicao;
        foreach (var componente in _componentes)
            componente.Configure(novaDefinicao);
    }

    /// <summary>
    /// A ordem dos componentes dentro do quadro é decidida aqui.
    /// </summary>
    /// <remarks>
    /// Cada componente é passivo e só age quando o contêiner manda — nenhum deles
    /// tem <c>_PhysicsProcess</c> próprio. É o que torna a ordem da spec 01 §6
    /// verificável num lugar só, em vez de emergir da ordem dos nós na cena.
    /// </remarks>
    public override void _PhysicsProcess(double delta)
    {
        if (Context is null)
            return;

        // 1. entrada bruta -- teclado para o jogador, percepção/estado para
        //    um inimigo. Nunca os dois num mesmo personagem.
        var intencao = _entrada?.Poll() ?? _cerebro?.Poll((float)delta) ?? IntentFrame.Idle;

        // 2. mira: projeta o cursor e devolve a direção para a intenção
        if (_mira is not null && _entrada is not null)
        {
            _mira.UpdateFromScreen(intencao.ScreenPointer);
            intencao = intencao with
            {
                AimPoint = _mira.AimPoint,
                AimDirection = _mira.AimDirection,
                HasAim = _mira.HasAim,
            };
        }

        // Hitstop (ticket 11): escala SÓ a locomoção. Lido do estado que
        // RESOLVEQUEUE (item 4, abaixo) atualizou no quadro ANTERIOR — um
        // congelamento pedido agora só trava movimento a partir do próximo
        // quadro. Curto o bastante (0,04-0,09 s) para essa defasagem não
        // incomodar.
        //
        // NÃO escala combate: o combo/tambor de QUEM ACABOU de acertar
        // rodaria em delta zero pelo mesmo congelamento que o próprio acerto
        // pediu, atrasando o disparo/golpe seguinte a cada vez que um
        // conectasse -- um efeito cascata que se acumula tiro a tiro. O
        // congelamento visual vem do corpo parar de deslizar, que já é o
        // sinal dominante sem animações (M8).
        var escalaDeHitstop = _vida?.TimeScale ?? 1f;

        // 2b. mira de quem não tem cursor (inimigo à distância, ticket 28):
        //     o cérebro já resolveu a mira -- só a repassa à arma.
        if (_mira is not null && _entrada is null)
            _mira.SetAim(intencao.AimPoint, intencao.AimDirection, intencao.HasAim);

        // 3. locomoção
        _movimento?.Tick(intencao, (float)delta * escalaDeHitstop);

        // 3b. combate: o pedido primeiro, a resolução da janela depois — assim
        //     um golpe pedido neste quadro já pode abrir a janela no próximo.
        if (intencao.AttackPressed)
            _combate?.RequestBasicAttack();

        _combate?.Tick((float)delta, intencao.AttackHeld);

        // 3c. habilidades: grava o símbolo de comando na borda de subida de
        //     cada tecla (nunca o eixo composto de Move -- ver IntentFrame),
        //     confirma com M2, e avança a execução em curso. Depois do
        //     combate: o gate de ActionLock.Abilities lido por TryExecute
        //     precisa das travas JÁ atualizadas por este quadro.
        if (intencao.CommandUpPressed) _habilidades?.PushToken(CommandDirection.Up);
        if (intencao.CommandDownPressed) _habilidades?.PushToken(CommandDirection.Down);
        if (intencao.CommandLeftPressed) _habilidades?.PushToken(CommandDirection.Left);
        if (intencao.CommandRightPressed) _habilidades?.PushToken(CommandDirection.Right);
        if (intencao.ConfirmPressed) _habilidades?.RequestConfirm();

        _habilidades?.Tick((float)delta);

        // 3d. mana: regenera, com atraso após qualquer gasto. Precisa vir
        //     DEPOIS das habilidades: um TryExecute bem-sucedido já gastou
        //     mana neste mesmo quadro, e regenerar antes disso devolveria uma
        //     fração de sobra que a habilidade nem tinha visto ainda.
        _mana?.Tick((float)delta);

        // 3e. flash de dano: puramente visual, sem afetar simulação nenhuma.
        _flash?.Tick((float)delta);

        // 4. dano: PONTO ÚNICO do quadro. Golpes chegam de áreas de colisão em
        //    momentos arbitrários; resolvê-los só aqui é o que impede dois
        //    golpes simultâneos de disparar morte duas vezes. Delta CRU:
        //    é aqui que o relógio do próprio hitstop anda (ver HealthComponent).
        _vida?.ResolveQueue((float)delta);
    }

    /// <summary>
    /// Encontra os componentes entre os descendentes.
    /// </summary>
    /// <remarks>
    /// Varre a subárvore em vez de exigir filhos diretos, para que agrupar
    /// componentes em um nó não quebre o contêiner. Não atravessa outro
    /// <see cref="CharacterController"/>: um personagem aninhado é dono dos
    /// próprios componentes.
    ///
    /// <c>GetChildren()</c> devolve <c>Godot.Collections.Array</c>; as convenções
    /// §2 permitem a exceção na fronteira com a engine, e isto roda uma vez, no
    /// <c>_Ready</c>.
    /// </remarks>
    private void ColetarComponentes(Node no)
    {
        foreach (var filho in no.GetChildren())
        {
            if (filho is CharacterController)
                continue;

            if (filho is ICharacterComponent componente)
            {
                _componentes.Add(componente);
                Registrar(componente);
            }

            ColetarComponentes(filho);
        }
    }

    /// <remarks>
    /// Este switch cresce com o número de componentes e, a partir de uns poucos,
    /// vira ponto de edição repetida junto com o <see cref="CharacterContext"/>.
    /// Quando passar de meia dúzia, trocar por auto-registro do componente no
    /// próprio <c>Bind</c>.
    /// </remarks>
    private void Registrar(ICharacterComponent componente)
    {
        switch (componente)
        {
            case MovementComponent m:
                _movimento = m;
                Context!.Movement = m;
                break;
            case TargetingComponent t:
                _mira = t;
                Context!.Targeting = t;
                break;
            case StatsComponent st:
                Context!.Stats = st;
                break;
            case HealthComponent h:
                _vida = h;
                Context!.Health = h;
                break;
            case ManaComponent mn:
                _mana = mn;
                Context!.Mana = mn;
                break;
            case CombatComponent c:
                _combate = c;
                Context!.Combat = c;
                break;
            case AbilityComponent a:
                _habilidades = a;
                Context!.Abilities = a;
                break;
            case DamageFlashComponent f:
                _flash = f;
                break;
            case NavigationMotor nav:
                Context!.NavigationMotor = nav;
                break;
            case AttackTelegraphComponent tel:
                Context!.AttackTelegraph = tel;
                break;
            case PlayerInputController e:
                _entrada = e;
                break;
            case EnemyBrain b:
                _cerebro = b;
                Context!.EnemyBrain = b;
                break;
        }
    }
}
