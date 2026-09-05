using System.Collections.Generic;
using Contenda.Components.Combat;
using Contenda.Components.Health;
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
    private TargetingComponent? _mira;
    private MovementComponent? _movimento;
    private HealthComponent? _vida;
    private DebugDamageDealer? _golpeDebug;

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

        // 1. entrada bruta
        var intencao = _entrada?.Poll() ?? IntentFrame.Idle;

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

        // 3. locomoção
        _movimento?.Tick(intencao, (float)delta);

        // 3b. andaime do ticket 07: sai quando o IWeapon do 08 entrar
        if (intencao.AttackPressed)
            _golpeDebug?.TryStrike();

        // 4. dano: PONTO ÚNICO do quadro. Golpes chegam de áreas de colisão em
        //    momentos arbitrários; resolvê-los só aqui é o que impede dois
        //    golpes simultâneos de disparar morte duas vezes.
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
            case DebugDamageDealer d:
                _golpeDebug = d;
                break;
            case PlayerInputController e:
                _entrada = e;
                break;
        }
    }
}
