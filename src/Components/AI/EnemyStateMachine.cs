namespace Contenda.Components.AI;

/// <summary>
/// Decide as transições de <see cref="EnemyState"/>, sem nenhum nó envolvido.
/// </summary>
/// <remarks>
/// POCO de propósito, mesma disciplina do <c>MeleeCombo</c>: as janelas de
/// tempo (alerta, cooldown, atordoamento, perder de vista) são onde um erro
/// de comparação passa despercebido, e só dá para testar isso controlando o
/// relógio fora da engine. Ver docs/specs/09-inimigos-e-ia.md §2 e o
/// ticket 22.
///
/// Não sabe nada de percepção (raio, raycast), navegação ou combate de
/// verdade — só recebe sinais já resolvidos (<c>alvoVisivel</c>,
/// <c>dentroDoAlcanceDeAtaque</c>, <c>ataqueTerminou</c>) a cada quadro.
/// Quem produz esses sinais é o <c>EnemyBrain</c>, na fronteira com a engine.
/// </remarks>
public sealed class EnemyStateMachine
{
    private readonly float _alertDuration;
    private readonly float _attackCooldown;
    private readonly float _staggerDuration;
    private readonly float _loseTargetDelay;

    private float _tempoNoEstado;
    private float _tempoSemVisao;

    public EnemyStateMachine(float alertDuration, float attackCooldown, float staggerDuration, float loseTargetDelay)
    {
        _alertDuration = alertDuration;
        _attackCooldown = attackCooldown;
        _staggerDuration = staggerDuration;
        _loseTargetDelay = loseTargetDelay;
    }

    /// <summary>O estado atual.</summary>
    public EnemyState Estado { get; private set; } = EnemyState.Idle;

    /// <summary>Há quanto tempo está neste estado, em segundos.</summary>
    public float TempoNoEstado => _tempoNoEstado;

    /// <summary>
    /// Se já morreu. Centraliza a checagem usada em mais de um lugar como
    /// defesa contra golpe/morte repetidos -- ver <see cref="RegistrarMorte"/>
    /// e <see cref="RegistrarGolpeRecebido"/>.
    /// </summary>
    private bool EstaMorto => Estado == EnemyState.Death;

    /// <summary>
    /// Avança um quadro.
    /// </summary>
    /// <param name="delta">Tempo do quadro, em segundos.</param>
    /// <param name="alvoVisivel">
    /// Se o alvo está detectável AGORA -- a peneira certa (raio de detecção
    /// vs. raio de perda) já foi escolhida por quem chama, de acordo com o
    /// estado atual.
    /// </param>
    /// <param name="dentroDoAlcanceDeAtaque">Se o alvo já está perto o bastante para golpear.</param>
    /// <param name="ataqueTerminou">Se o golpe em andamento (estado <see cref="EnemyState.Attack"/>) já acabou.</param>
    /// <param name="estaNoChao">
    /// Se o inimigo está apoiado no chão AGORA -- só importa para
    /// <see cref="EnemyState.Airborne"/> sair sozinho ao aterrissar.
    /// </param>
    public void Advance(
        float delta, bool alvoVisivel, bool dentroDoAlcanceDeAtaque, bool ataqueTerminou, bool estaNoChao = true)
    {
        _tempoNoEstado += delta;

        switch (Estado)
        {
            case EnemyState.Idle:
                if (alvoVisivel)
                    TransicionarPara(EnemyState.Alert);
                break;

            case EnemyState.Alert:
                AtualizarPerdaDeAlvo(delta, alvoVisivel);
                if (Estado == EnemyState.Alert && _tempoNoEstado >= _alertDuration)
                    TransicionarPara(EnemyState.Chase);
                break;

            case EnemyState.Chase:
                AtualizarPerdaDeAlvo(delta, alvoVisivel);
                if (Estado == EnemyState.Chase && alvoVisivel && dentroDoAlcanceDeAtaque)
                    TransicionarPara(EnemyState.Attack);
                break;

            case EnemyState.Attack:
                if (ataqueTerminou)
                    TransicionarPara(EnemyState.Recover);
                break;

            case EnemyState.Recover:
                if (_tempoNoEstado >= _attackCooldown)
                    TransicionarPara(EnemyState.Chase);
                break;

            case EnemyState.Staggered:
                if (_tempoNoEstado >= _staggerDuration)
                    TransicionarPara(EnemyState.Chase);
                break;

            case EnemyState.Airborne:
                if (estaNoChao)
                    TransicionarPara(EnemyState.Staggered);
                break;

            case EnemyState.Death:
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// A vida chegou a zero -- interrompe TUDO e entra em
    /// <see cref="EnemyState.Death"/>, de onde só a reciclagem do pool
    /// (<c>ResetForSpawn</c>, construindo uma máquina nova) sai. Ver ticket
    /// 25, spec 09 §2 e §8.
    /// </summary>
    public void RegistrarMorte()
    {
        if (EstaMorto)
            return;

        TransicionarPara(EnemyState.Death);
    }

    /// <summary>
    /// Um golpe conectou -- interrompe qualquer estado e atordoa ou lança no
    /// ar, dependendo do tipo de golpe.
    /// </summary>
    /// <param name="lancamentoVertical">
    /// Se este golpe é um lançamento vertical (anti-aéreo, ticket 24) em vez
    /// de um golpe comum.
    /// </param>
    /// <remarks>
    /// Golpes seguidos REFRESCAM o atordoamento em vez de somar duração: um
    /// combo de três do jogador não deveria travar o inimigo por três vezes
    /// o tempo, só mantê-lo atordoado enquanto os golpes continuarem
    /// conectando. Mesma disciplina do "nunca somar, sempre tomar o maior ou
    /// resetar" já usada em <c>ActionLockSet</c>.
    ///
    /// Um golpe comum enquanto já está <see cref="EnemyState.Airborne"/> NÃO
    /// interrompe para <see cref="EnemyState.Staggered"/>: é exatamente o
    /// combo aéreo do ticket 19 sustentando o inimigo no alto com uma
    /// sequência de golpes -- só tocar o chão (visto por <see cref="Advance"/>)
    /// tira alguém do ar. Um lançamento vertical, por outro lado, entra em
    /// <see cref="EnemyState.Airborne"/> a partir de QUALQUER estado, mesmo
    /// já atordoado ou já no ar (refresca, não empilha).
    /// </remarks>
    public void RegistrarGolpeRecebido(bool lancamentoVertical)
    {
        // Defesa extra, não o caminho normal: `HealthState.Apply` já descarta
        // todo golpe contra quem não `IsAlive`, então `Damaged`/`Died` nunca
        // deveriam refirar depois da morte. Mas esta máquina é pública e não
        // deveria confiar em quem chama para nunca golpear um cadáver -- a
        // mesma disciplina de "morte é idempotente" do HealthState, um nível
        // acima.
        if (EstaMorto)
            return;

        if (lancamentoVertical)
        {
            TransicionarPara(EnemyState.Airborne);
            return;
        }

        if (Estado is EnemyState.Airborne or EnemyState.Staggered)
        {
            _tempoNoEstado = 0f;
            return;
        }

        TransicionarPara(EnemyState.Staggered);
    }

    private void AtualizarPerdaDeAlvo(float delta, bool alvoVisivel)
    {
        if (alvoVisivel)
        {
            _tempoSemVisao = 0f;
            return;
        }

        _tempoSemVisao += delta;
        if (_tempoSemVisao >= _loseTargetDelay)
            TransicionarPara(EnemyState.Idle);
    }

    private void TransicionarPara(EnemyState novo)
    {
        Estado = novo;
        _tempoNoEstado = 0f;
        _tempoSemVisao = 0f;
    }
}
