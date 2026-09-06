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
    public void Advance(float delta, bool alvoVisivel, bool dentroDoAlcanceDeAtaque, bool ataqueTerminou)
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

            default:
                break;
        }
    }

    /// <summary>
    /// Um golpe conectou -- interrompe qualquer estado e atordoa.
    /// </summary>
    /// <remarks>
    /// Golpes seguidos REFRESCAM o atordoamento em vez de somar duração: um
    /// combo de três do jogador não deveria travar o inimigo por três vezes
    /// o tempo, só mantê-lo atordoado enquanto os golpes continuarem
    /// conectando. Mesma disciplina do "nunca somar, sempre tomar o maior ou
    /// resetar" já usada em <c>ActionLockSet</c>.
    /// </remarks>
    public void RegistrarGolpeRecebido()
    {
        if (Estado == EnemyState.Staggered)
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
