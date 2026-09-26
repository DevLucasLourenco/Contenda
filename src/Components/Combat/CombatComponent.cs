using System;
using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Weapons;
using Godot;

namespace Contenda.Components.Combat;

/// <summary>
/// Executa o ataque básico com a arma equipada.
/// </summary>
/// <remarks>
/// **M1 não significa "soco", significa "ataque básico".** Este componente não
/// sabe se está segurando espada ou revólver: ele pede o golpe e a
/// <see cref="IWeapon"/> que a <see cref="WeaponFactory"/> monta a partir da
/// <see cref="WeaponDefinition"/> decide o que isso quer dizer. É a abstração
/// que permite o ticket 09 existir sem um `if` sobre qual personagem está em
/// jogo — e o terceiro, o décimo e o quinquagésimo personagem depois.
/// Ver spec 07 §1 e o ticket 08.
/// </remarks>
public sealed partial class CombatComponent : Node, ICharacterComponent
{
    /// <summary>A arma, quando a definição do personagem não traz a sua.</summary>
    [Export] public WeaponDefinition? Fallback { get; set; }

    /// <summary>Grupo varrido em busca de alvos.</summary>
    [Export] public StringName TargetGroup { get; set; } = new("damageable");

    /// <summary>
    /// Diferenca de altura tolerada entre quem golpeia e quem apanha, em metros.
    /// </summary>
    /// <remarks>
    /// Generosa de proposito: sob camera inclinada o jogador julga altura mal, e
    /// recusar um golpe por meio metro de desnivel parece bug. O combate aereo do
    /// ticket 19 vai querer isto configuravel por golpe.
    /// </remarks>
    [Export(PropertyHint.Range, "0.5,6,0.1")] public float VerticalReach { get; set; } = 2.5f;

    /// <summary>Fonte usada para a trava que o próprio golpe aplica em si mesmo.</summary>
    private const string SelfLockSource = "combat.attack";

    private readonly ActionLockSet _locks = new();
    private readonly Dictionary<WeaponDefinition, IWeapon> _instanciasDeArma = [];

    private CharacterContext? _contexto;
    private IWeapon _arma = NullWeapon.Instance;
    private WeaponDefinition _armaBase = new();

    /// <summary>Definição da arma atualmente equipada.</summary>
    public WeaponDefinition EquippedWeapon { get; private set; } = new();

    /// <summary>Se há um golpe em andamento.</summary>
    public bool IsAttacking => _arma.IsAttacking;

    /// <summary>Passo atual da cadeia, de 1 a N. Zero quando ocioso.</summary>
    public int ComboStep => _arma.ComboStep;

    /// <summary>
    /// A união de todas as travas de ação ativas agora, de qualquer fonte.
    /// </summary>
    /// <remarks>
    /// Consumido pelo <c>MovementComponent</c> para recusar WASD e giro
    /// enquanto o golpe estiver em andamento — sem isto, o jogador anda e vira
    /// livremente no meio da animação, que é exatamente o que o ticket 08
    /// apontava como faltando.
    /// </remarks>
    public ActionLock ActiveLocks => _locks.Current;

    /// <summary>Avisa que um golpe começou, com o passo da cadeia.</summary>
    public event Action<int>? AttackStarted;

    /// <summary>Avisa que um golpe conectou.</summary>
    public event Action<Node3D>? HitLanded;

    public void Bind(CharacterContext contexto) => _contexto = contexto;

    public void Configure(CharacterDefinition definicao)
    {
        // _contexto já existe: o CharacterController garante Bind em todos os
        // componentes antes de Configure em qualquer um. Ver spec 01 §4.1.
        _armaBase = definicao.Weapon ?? Fallback ?? new WeaponDefinition();
        _instanciasDeArma.Clear();
        _instanciasDeArma.Add(_armaBase, WeaponFactory.Criar(_armaBase, _contexto!, this, TargetGroup, VerticalReach));
        Equipar(_armaBase);
    }

    /// <summary>Troca temporariamente a arma básica, mantendo a original para reversão.</summary>
    public void SetWeaponOverride(WeaponDefinition? weapon) => Equipar(weapon ?? _armaBase);

    private void Equipar(WeaponDefinition weapon)
    {
        if (ReferenceEquals(weapon, EquippedWeapon))
            return;

        _arma.Cancel();
        DesligarEventos();
        EquippedWeapon = weapon;
        if (!_instanciasDeArma.TryGetValue(weapon, out var instancia))
        {
            instancia = WeaponFactory.Criar(weapon, _contexto!, this, TargetGroup, VerticalReach);
            _instanciasDeArma.Add(weapon, instancia);
        }

        _arma = instancia;
        LigarEventos();
    }

    /// <summary>Pede um ataque básico. Chamado pelo contêiner ao apertar M1.</summary>
    public void RequestBasicAttack() => _arma.RequestBasicAttack();

    /// <summary>Interrompe o golpe. Chamado ao morrer ou tomar atordoamento.</summary>
    public void Cancel()
    {
        _arma.Cancel();
        _locks.Clear(SelfLockSource);
    }

    /// <summary>Devolve ao estado de recém-criado. Contrato do pool, no M5.</summary>
    public void ResetForSpawn()
    {
        _arma.ResetForSpawn();
        _locks.Reset();
    }

    /// <summary>
    /// Avança a arma e a expiração das travas. Chamado pelo contêiner.
    /// </summary>
    /// <param name="delta">Tempo do tique, em segundos.</param>
    /// <param name="triggerHeld">Se o botão de M1 segue pressionado agora.</param>
    public void Tick(float delta, bool triggerHeld)
    {
        _arma.Tick(delta, triggerHeld);
        _locks.Tick(delta);

        // O golpe trava quem o desfere: refrescado a cada quadro enquanto durar,
        // em vez de aplicado uma vez com a duração do passo. A duração vem
        // DAQUI, não da arma -- é o que faz o revólver do ticket 09 herdar o
        // mesmo travamento sem saber que ActionLock existe. `delta * 2`
        // sobrevive a um quadro perdido e ainda assim libera dentro de ~1
        // quadro do fim do golpe, sem precisar de ClearLock explícito.
        if (_arma.IsAttacking)
            _locks.Apply(SelfLockSource, ActionLock.Movement | ActionLock.Rotation, delta * 2f);
    }

    /// <summary>Aplica uma trava de outra fonte (atordoamento, habilidade, etc.).</summary>
    public void ApplyLock(string source, ActionLock flags, float duration) => _locks.Apply(source, flags, duration);

    /// <summary>Libera a trava desta fonte, se houver.</summary>
    public void ClearLock(string source) => _locks.Clear(source);

    private void LigarEventos()
    {
        _arma.AttackStarted += RepassarInicio;
        _arma.HitLanded += RepassarAcerto;
    }

    private void DesligarEventos()
    {
        _arma.AttackStarted -= RepassarInicio;
        _arma.HitLanded -= RepassarAcerto;
    }

    private void RepassarInicio(int passo) => AttackStarted?.Invoke(passo);

    private void RepassarAcerto(Node3D alvo) => HitLanded?.Invoke(alvo);
}
