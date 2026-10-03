using System;
using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.Stats;
using Contenda.Core;
using Godot;

namespace Contenda.Components.Health;

/// <summary>
/// Vida de uma entidade, com fila de dano.
/// </summary>
/// <remarks>
/// **O dano é enfileirado, não aplicado na hora.** Golpes chegam de áreas de
/// colisão, que disparam em momentos arbitrários do quadro; resolvê-los num
/// ponto único é o que garante que dois golpes simultâneos não disparem morte
/// duas vezes. Ver spec 01 §6 e spec 15 §5.
/// </remarks>
public sealed partial class HealthComponent : Node, ICharacterComponent, IDamageable
{
    /// <summary>Valores de vida, quando a definição do personagem não traz os seus.</summary>
    [Export] public HealthDefinition? Fallback { get; set; }

    private readonly Queue<DamageInfo> _fila = new();
    private readonly HitstopState _hitstop = new();
    private readonly AerialJuggleState _juggleAereo = new();
    private HealthState _estado = new(100f);
    private CharacterContext? _contexto;
    private StatsComponent? _stats;
    private float _invulnerabilidade = 0.25f;
    private float _restanteDeInvulnerabilidade;
    private float _restanteDeInvulnerabilidadeConcedida;

    /// <summary>Vida atual.</summary>
    public float Current => _estado.Current;

    /// <summary>Vida máxima efetiva.</summary>
    public float Max => _estado.Max;

    /// <summary>Fração de vida, de 0 a 1.</summary>
    public float Percent => _estado.Percent;

    /// <summary>Se está temporariamente imune.</summary>
    public bool IsInvulnerable => _estado.IsInvulnerable;

    /// <inheritdoc/>
    public bool IsAlive => _estado.IsAlive;

    /// <summary>
    /// Multiplicador de delta enquanto o congelamento de impacto durar — 0
    /// congelado, 1 normal.
    /// </summary>
    /// <remarks>
    /// Lido pelo <c>CharacterController</c> para escalar o delta de movimento
    /// e combate deste personagem. Nunca <c>Engine.TimeScale</c> — ver
    /// <c>HitstopState</c> e o ticket 11.
    /// </remarks>
    public float TimeScale => _hitstop.TimeScale;

    /// <summary>Avisa que apanhou.</summary>
    public event Action<DamageInfo>? Damaged;

    /// <summary>Avisa que caiu. Uma vez por vida.</summary>
    public event Action<DamageInfo>? Died;

    /// <summary>Aviso anterior a <see cref="Died"/>; formas revertem antes da morte.</summary>
    public event Action<DamageInfo>? BeforeDied;

    /// <summary>Avisa que foi curado.</summary>
    public event Action<float>? Healed;

    // --- ciclo de vida -------------------------------------------------------

    public void Bind(CharacterContext contexto)
    {
        _contexto = contexto;

        // Desassinar antes de assinar: Bind pode rodar de novo num nó reciclado
        // pelo pool, e assinar duas vezes dobraria todo aviso.
        if (_stats is not null)
            _stats.StatChanged -= AoMudarAtributo;

        _stats = contexto.Stats;

        if (_stats is not null)
            _stats.StatChanged += AoMudarAtributo;
    }

    public void Configure(CharacterDefinition definicao)
    {
        var valores = definicao.Health ?? Fallback ?? new HealthDefinition();
        _invulnerabilidade = valores.InvulnerabilityAfterHit;

        TrocarEstado(new HealthState(valores.MaxHealth));

        // A vida máxima passa pelos ATRIBUTOS, e não direto para o estado: é
        // assim que uma transformação que aumenta a vida máxima, no M4, chega
        // até aqui sem ninguém ligar os dois à mão.
        _stats?.SetBase(StatId.MaxHealth, valores.MaxHealth);
    }

    public override void _ExitTree()
    {
        if (_stats is not null)
            _stats.StatChanged -= AoMudarAtributo;

        DesassinarEstado();
    }

    // --- API -----------------------------------------------------------------

    /// <inheritdoc/>
    public void ApplyDamage(in DamageInfo golpe) => _fila.Enqueue(golpe);

    /// <summary>Cura, sem passar do máximo e sem ressuscitar.</summary>
    public void Heal(float quanto) => _estado.Heal(quanto);

    /// <summary>
    /// Congela este personagem por um instante. Chamado pela arma, dos dois
    /// lados do golpe — quem bateu e quem apanhou.
    /// </summary>
    public void ApplyHitstop(float duration) => _hitstop.Apply(duration);

    /// <summary>
    /// Concede invulnerabilidade externa por uma duração, além da que já
    /// existe (se houver).
    /// </summary>
    /// <remarks>
    /// Fonte independente da invulnerabilidade pós-golpe — as duas somam por
    /// OR (uma reforça a outra, nunca corta a que já estava rodando), mesma
    /// disciplina do <c>ActionLockSet</c> para não deixar uma fonte encerrar
    /// cedo demais o que a outra ainda estava pedindo. O dash (ticket 17,
    /// spec 16 §4) é o primeiro consumidor: os i-frames do dash não deveriam
    /// nem estender nem encurtar os i-frames de ter acabado de apanhar.
    /// </remarks>
    public void GrantInvulnerability(float duration)
        => _restanteDeInvulnerabilidadeConcedida = Mathf.Max(_restanteDeInvulnerabilidadeConcedida, duration);

    /// <summary>
    /// Registra mais um acerto aéreo consecutivo sofrido.
    /// </summary>
    /// <remarks>
    /// Ticket 19, spec 16 §6. Chamado pela arma de quem golpeou, uma vez por
    /// alvo atingido por um golpe aéreo -- nunca pelo golpe de solo.
    /// </remarks>
    /// <returns>
    /// Se ainda está sob o teto de <see cref="AerialJuggleState.LimiteDeAcertos"/>
    /// -- quem golpeou só deveria empurrar para cima (o alvo e a si mesmo)
    /// enquanto isto for true. Sem o teto, um combo aéreo prenderia o alvo no
    /// ar para sempre.
    /// </returns>
    public bool RegistrarAcertoAereo() => _juggleAereo.RegistrarAcerto();

    /// <summary>
    /// Zera o contador de juggle aéreo. Chamado por <c>MovementComponent</c>
    /// ao tocar o chão -- um combo aéreo de verdade sempre termina em
    /// aterrissagem, e o próximo lançamento deveria começar do zero.
    /// </summary>
    public void ResetAerialJuggle() => _juggleAereo.Reset();

    /// <summary>Enfileira um golpe fatal, resolvido no ponto único do quadro.</summary>
    public void Kill(string origem)
        => ApplyDamage(new DamageInfo(
            Amount: _estado.Max,
            Type: DamageType.True,
            HitPoint: _contexto?.Body.GlobalPosition ?? Vector3.Zero,
            Direction: Vector3.Zero,
            Knockback: 0f,
            SourceId: 0UL,
            SourceTag: origem,
            IsCritical: false));

    /// <summary>
    /// Resolve todos os golpes do quadro. Chamado pelo contêiner.
    /// </summary>
    /// <param name="delta">Tempo do tique de física, em segundos.</param>
    /// <remarks>
    /// É o "ponto único do quadro" da spec 01 §6.
    ///
    /// A invulnerabilidade conta por <c>delta</c> acumulado, e não por relógio de
    /// parede: relógio de parede continua correndo com o jogo pausado e ignora
    /// câmera lenta, então os i-frames acabariam durante a pausa.
    /// </remarks>
    public void ResolveQueue(float delta)
    {
        // Sempre em delta CRU, nunca no delta já escalado pelo hitstop que o
        // CharacterController repassa a movimento/combate -- senão o próprio
        // congelamento nunca andaria o relógio que o encerra.
        _hitstop.Advance(delta);

        _restanteDeInvulnerabilidade = Mathf.Max(0f, _restanteDeInvulnerabilidade - delta);
        _restanteDeInvulnerabilidadeConcedida = Mathf.Max(0f, _restanteDeInvulnerabilidadeConcedida - delta);

        // OR das duas fontes, recalculado a cada quadro -- nunca um `= false`
        // direto de uma fonte só, que apagaria a invulnerabilidade que a OUTRA
        // fonte ainda estava pedindo.
        _estado.IsInvulnerable = _restanteDeInvulnerabilidade > 0f || _restanteDeInvulnerabilidadeConcedida > 0f;

        var algumAcertou = false;

        while (_fila.Count > 0)
        {
            var golpe = _fila.Dequeue();
            var defesa = _stats?.Get(StatId.DefenseMultiplier) ?? 1f;

            if (_estado.Apply(golpe, defesa))
                algumAcertou = true;
        }

        // A invulnerabilidade só liga DEPOIS de a fila inteira ser resolvida.
        // Ligá-la dentro do laço faria o primeiro golpe do quadro bloquear os
        // seguintes — e o objetivo da fila é justamente que dois golpes
        // simultâneos contem os dois, matando uma vez só.
        if (algumAcertou && _estado.IsAlive && _invulnerabilidade > 0f)
        {
            _estado.IsInvulnerable = true;
            _restanteDeInvulnerabilidade = _invulnerabilidade;
        }
    }

    /// <summary>Devolve ao estado de recém-criado. Contrato do pool, no M5.</summary>
    public void ResetForSpawn()
    {
        _fila.Clear();
        _restanteDeInvulnerabilidade = 0f;
        _restanteDeInvulnerabilidadeConcedida = 0f;
        _estado.Reset();
        _hitstop.Reset();
        _juggleAereo.Reset();
    }

    // --- privados ------------------------------------------------------------

    private void TrocarEstado(HealthState novo)
    {
        DesassinarEstado();
        _estado = novo;
        _estado.Damaged += RepassarDano;
        _estado.Died += RepassarMorte;
        _estado.Healed += RepassarCura;
        _estado.Dying += RepassarMorteAntecipada;
    }

    private void DesassinarEstado()
    {
        _estado.Damaged -= RepassarDano;
        _estado.Died -= RepassarMorte;
        _estado.Healed -= RepassarCura;
        _estado.Dying -= RepassarMorteAntecipada;
    }

    private void AoMudarAtributo(StatId stat, float valor)
    {
        if (stat == StatId.MaxHealth)
            _estado.SetMax(valor);
    }

    /// <remarks>
    /// Repassa também para <see cref="GameEvents"/>: o número de dano flutuante
    /// do ticket 11 é pooled e genérico, sem referência a nenhum personagem —
    /// exatamente o tipo de consumidor cross-cutting que o barramento existe
    /// para atender, spec 01 §5.
    /// </remarks>
    private void RepassarDano(DamageInfo golpe)
    {
        Damaged?.Invoke(golpe);
        ServiceLocator.Events.RaiseDamageImpact(new DamageImpactEvent(
            golpe.HitPoint,
            golpe.Type,
            golpe.IsCritical,
            golpe.SourceTag));

        if (_contexto?.Body == ServiceLocator.Session.PlayerBody)
            ServiceLocator.Events.RaisePlayerDamaged(new PlayerDamagedEvent(golpe.HitPoint));

        ServiceLocator.Events.RaiseDamageNumber(new DamageNumberEvent(golpe.HitPoint, golpe.Amount, golpe.IsCritical));
    }

    private void RepassarMorte(DamageInfo golpe) => Died?.Invoke(golpe);

    private void RepassarMorteAntecipada(DamageInfo golpe) => BeforeDied?.Invoke(golpe);

    private void RepassarCura(float quanto) => Healed?.Invoke(quanto);
}
