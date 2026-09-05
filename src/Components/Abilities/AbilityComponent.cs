using System;
using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.Combat;
using Contenda.Components.Health;
using Contenda.Input;
using Godot;

namespace Contenda.Components.Abilities;

/// <summary>
/// Resolve uma sequência de WASD confirmada numa habilidade, cobra o custo e
/// toca a execução do início ao fim.
/// </summary>
/// <remarks>
/// O momento central do jogo — spec 05 e o ticket 14. Guarda a trave de
/// entrada (<see cref="CommandBuffer"/> + <see cref="AbilityComboResolver{T}"/>),
/// a recarga por habilidade (<see cref="AbilityCooldownTracker"/>) e o estado
/// de UMA execução por vez — o <see cref="IAbilityBehavior"/> em si não sabe
/// de nenhuma dessas três coisas, só do próprio efeito.
///
/// Só um relógio interno (<c>_relogio</c>) alimenta tanto a expiração do
/// buffer quanto a recarga: os dois quereriam um "agora" monotônico de
/// qualquer forma, e usar dois relógios separados criaria dois lugares para
/// divergirem sem motivo.
/// </remarks>
public sealed partial class AbilityComponent : Node, ICharacterComponent
{
    /// <summary>Fonte usada para a trava que a própria execução aplica em si mesma.</summary>
    private const string SelfLockSource = "ability.cast";

    /// <summary>Grupo varrido em busca de alvos — mesmo padrão do <c>CombatComponent</c>.</summary>
    [Export] public StringName TargetGroup { get; set; } = new("damageable");

    [Export(PropertyHint.Range, "1,10,1")] public int BufferCapacity { get; set; } = 6;
    [Export(PropertyHint.Range, "0.1,2,0.05")] public float TokenLifetime { get; set; } = 0.70f;
    [Export(PropertyHint.Range, "0.2,3,0.05")] public float SequenceTimeout { get; set; } = 1.20f;

    private readonly AbilityCooldownTracker _recargas = new();

    private CommandBuffer _buffer = new();
    private AbilityComboResolver<AbilityDefinition>? _resolver;
    private CharacterContext? _contexto;
    private IReadOnlyList<AbilityDefinition> _habilidades = [];

    private readonly AbilityTimeline _relogioExecucao = new();

    private AbilityDefinition? _executando;
    private IAbilityBehavior? _comportamento;
    private AbilityContext? _ctxExecucao;
    private float _relogio;

    /// <summary>As habilidades deste personagem, na ordem do <see cref="CharacterDefinition"/>.</summary>
    public IReadOnlyList<AbilityDefinition> Abilities => _habilidades;

    /// <summary>A habilidade em execução agora. Nulo se ocioso.</summary>
    public AbilityDefinition? Executing => _executando;

    /// <summary>A sequência de WASD acumulada agora — alimenta o HUD ao vivo, no ticket 16.</summary>
    public ReadOnlySpan<CommandDirection> CurrentSequence => _buffer.Current;

    /// <summary>Uma execução começou com sucesso.</summary>
    public event Action<AbilityDefinition>? Executed;

    /// <summary>Uma tentativa contra uma habilidade específica falhou, e por quê.</summary>
    public event Action<AbilityDefinition, AbilityAttemptResult>? Rejected;

    /// <summary>Uma recarga começou — no INÍCIO da execução, não no fim.</summary>
    public event Action<StringName, float>? CooldownStarted;

    /// <summary>
    /// A sequência confirmada não casa com nenhuma habilidade. Evento à parte
    /// de <see cref="Rejected"/>: aqui não existe uma <see cref="AbilityDefinition"/>
    /// para apontar — ver <see cref="AbilityAttemptResult"/>.
    /// </summary>
    public event Action? SequenceRejected;

    /// <summary>
    /// A lista de <see cref="Abilities"/> mudou — de <see cref="Configure"/>,
    /// seja o primeiro `_Ready` ou uma troca de arquétipo em runtime (ticket
    /// 12). O guia de combos do ticket 16 reconstrói as próprias linhas a
    /// partir daqui, em vez de assumir que a lista nunca muda.
    /// </summary>
    public event Action? AbilitiesChanged;

    public void Bind(CharacterContext contexto)
    {
        // Desassinar antes de assinar: Bind pode rodar de novo num nó
        // reciclado pelo pool, e assinar duas vezes dobraria todo Died.
        if (_contexto?.Health is not null)
            _contexto.Health.Died -= AoMorrer;

        _contexto = contexto;

        // Context.Health já existe aqui: o CharacterController registra os
        // componentes (e popula o CharacterContext) ANTES da passada de Bind.
        // Ver ICharacterComponent e CharacterController.ColetarComponentes.
        if (_contexto.Health is not null)
            _contexto.Health.Died += AoMorrer;
    }

    public void Configure(CharacterDefinition definicao)
    {
        _habilidades = definicao.Abilities ?? [];
        _resolver = _habilidades.Count > 0
            ? new AbilityComboResolver<AbilityDefinition>(_habilidades, a => a.Sequence, a => a.DisplayName)
            : null;

        _buffer = new CommandBuffer(BufferCapacity, TokenLifetime, SequenceTimeout);
        CancelCurrent();
        _recargas.Reset();

        AbilitiesChanged?.Invoke();
    }

    public override void _ExitTree()
    {
        if (_contexto?.Health is not null)
            _contexto.Health.Died -= AoMorrer;
    }

    // --- entrada ---------------------------------------------------------

    /// <summary>Grava um símbolo de comando. Chamado pelo contêiner na borda de subida do WASD.</summary>
    public void PushToken(CommandDirection direcao) => _buffer.Push(direcao, _relogio);

    /// <summary>
    /// Confirma a sequência acumulada agora. Chamado pelo contêiner ao apertar M2.
    /// </summary>
    /// <remarks>
    /// Sem match, a fila é limpa e <see cref="SequenceRejected"/> dispara —
    /// **nunca** cai no ataque básico como consolo, spec 03 §5 e ticket 14.
    /// Com match mas gate reprovado (recarga, mana), a fila permanece: o
    /// jogador já digitou a sequência certa, só falta a habilidade ficar
    /// disponível, e reconfirmar não deveria custar reescrever o combo.
    /// </remarks>
    public void RequestConfirm()
    {
        var resolvida = _resolver?.Resolve(_buffer.Current);
        if (resolvida is null)
        {
            _buffer.Clear();
            SequenceRejected?.Invoke();
            return;
        }

        TryExecute(resolvida);
    }

    // --- execução ----------------------------------------------------------

    /// <summary>Quanto falta para esta habilidade recarregar, em segundos.</summary>
    public float CooldownRemaining(StringName abilityId) => _recargas.RemainingAt(abilityId.ToString(), _relogio);

    /// <summary>Se esta habilidade já pode ser executada de novo.</summary>
    public bool IsReady(StringName abilityId) => _recargas.IsReady(abilityId.ToString(), _relogio);

    /// <summary>
    /// Tenta executar uma habilidade específica, validando os gates na ordem da spec 05 §2.
    /// </summary>
    /// <remarks>
    /// A ordem em si — <see cref="AbilityGateEvaluator"/> — é uma função pura,
    /// testada em xUnit isoladamente. Mana fica de fora dela de propósito e é
    /// resolvida aqui, por último e com uma chamada só: <c>TryConsume</c> já é
    /// atômico (não consome nada se reprovar), então checar <c>CanConsume</c>
    /// antes não preveniria corrida nenhuma — está tudo síncrono, no mesmo
    /// quadro de física.
    ///
    /// Tags requeridas/bloqueadoras (spec 05 §2, passos 4-5) também ficam fora
    /// do avaliador: não existe nenhuma fonte real de tag (transformação, M4)
    /// para conceder ou negar uma ainda.
    /// </remarks>
    public AbilityAttemptResult TryExecute(AbilityDefinition ability)
    {
        ArgumentNullException.ThrowIfNull(ability);

        var travas = _contexto?.Combat?.ActiveLocks ?? ActionLock.None;
        var resultado = AbilityGateEvaluator.Evaluate(
            alreadyCasting: _executando is not null,
            blockedByLock: (travas & ActionLock.Abilities) != 0,
            onCooldown: !_recargas.IsReady(ability.Id.ToString(), _relogio));

        if (resultado != AbilityAttemptResult.Success)
            return Rejeitar(ability, resultado);

        var mana = _contexto?.Mana;
        if (!(mana?.TryConsume(ability.ManaCost) ?? true))
            return Rejeitar(ability, AbilityAttemptResult.NotEnoughMana);

        ComecarExecucao(ability);
        return AbilityAttemptResult.Success;
    }

    /// <summary>Interrompe a execução em andamento. Chamado ao morrer ou tomar atordoamento forte.</summary>
    public void CancelCurrent()
    {
        if (_executando is null)
            return;

        FinalizarExecucao(cancelled: true);
    }

    /// <summary>
    /// Envelhece o buffer e avança a execução em andamento. Chamado pelo contêiner.
    /// </summary>
    public void Tick(float delta)
    {
        _relogio += delta;
        _buffer.Tick(_relogio);

        if (_executando is null || _ctxExecucao is null || _comportamento is null)
            return;

        var terminou = _relogioExecucao.Advance(delta);
        _ctxExecucao.ElapsedTime = _relogioExecucao.Elapsed;
        _comportamento.Tick(_ctxExecucao, delta);

        if (terminou)
            FinalizarExecucao(cancelled: false);
    }

    /// <summary>Devolve ao estado de recém-criado. Contrato do pool, no M5.</summary>
    public void ResetForSpawn()
    {
        CancelCurrent();
        _buffer.Clear();
        _recargas.Reset();
        _relogio = 0f;
    }

    private void ComecarExecucao(AbilityDefinition ability)
    {
        var corpo = _contexto!.Body;
        var frenteBruta = -corpo.GlobalTransform.Basis.Z;
        var frente = new Vector3(frenteBruta.X, 0f, frenteBruta.Z).Normalized();
        var mira = _contexto.Targeting is { HasAim: true } alvo ? alvo.AimDirection : frente;

        _executando = ability;
        _relogioExecucao.Begin(ability.CastTime, ability.RecoveryTime);
        _comportamento = AbilityBehaviorRegistry.Criar(ability.Kind);
        _ctxExecucao = new AbilityContext(_contexto, ability, corpo.GlobalPosition, mira, TargetGroup);

        // Recarga começa no INÍCIO da execução, não no fim -- spec 05 §4. Uma
        // execução cancelada a meio caminho não devolve a recarga: ver
        // FinalizarExecucao.
        _recargas.Start(ability.Id.ToString(), ability.Cooldown, _relogio);
        CooldownStarted?.Invoke(ability.Id, ability.Cooldown);

        _contexto.Combat?.ApplyLock(SelfLockSource, ability.LocksDuringCast, ability.CastTime + ability.RecoveryTime);

        // Só ao ter sucesso -- ver o comentário de RequestConfirm sobre por
        // que um gate reprovado NÃO limpa o buffer.
        _buffer.Clear();

        _comportamento.Begin(_ctxExecucao);
        Executed?.Invoke(ability);
    }

    /// <remarks>
    /// A trava só precisa ser liberada explicitamente no cancelamento: no
    /// término natural ela já expira sozinha, porque foi aplicada com a MESMA
    /// duração (CastTime + RecoveryTime) e o mesmo delta cru que este método
    /// observa -- os dois relógios andam em lockstep dentro do mesmo quadro.
    /// </remarks>
    private void FinalizarExecucao(bool cancelled)
    {
        _comportamento?.End(_ctxExecucao!, cancelled);

        if (cancelled)
        {
            _relogioExecucao.Cancel();
            _contexto?.Combat?.ClearLock(SelfLockSource);
        }

        _executando = null;
        _comportamento = null;
        _ctxExecucao = null;
    }

    private AbilityAttemptResult Rejeitar(AbilityDefinition ability, AbilityAttemptResult motivo)
    {
        Rejected?.Invoke(ability, motivo);
        return motivo;
    }

    /// <remarks>
    /// Também limpa o buffer de comandos: morrer no meio de uma sequência
    /// ainda não confirmada não deveria deixá-la sobreviver ao respawn --
    /// mesma lista de gatilhos que o próprio <c>CommandBuffer.Clear</c>
    /// documenta (spec 03 §5), e que <see cref="CancelCurrent"/> sozinho não
    /// cobre quando não há execução em andamento.
    /// </remarks>
    private void AoMorrer(DamageInfo golpe)
    {
        CancelCurrent();
        _buffer.Clear();
    }
}
