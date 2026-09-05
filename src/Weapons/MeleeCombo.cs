using Godot;

namespace Contenda.Weapons;

/// <summary>
/// A máquina de estados de um combo corpo a corpo, sem nenhum nó envolvido.
/// </summary>
/// <remarks>
/// POCO de propósito: janelas de tempo são onde erro de comparação passa
/// despercebido, e testá-las exige controlar o relógio — o que só dá para fazer
/// fora da engine. Ver spec 15 §1.
///
/// As janelas vêm de DADOS, e não de faixas de chamada dentro da animação: no M8
/// os modelos são substituídos, e faixas embutidas se perderiam junto com eles.
/// Ver spec 13 §6.
/// </remarks>
public sealed class MeleeCombo
{
    private readonly int _passos;
    private readonly float _hitStartPadrao;
    private readonly float _hitEndPadrao;
    private readonly float _comboWindowEndPadrao;

    private float _hitStart;
    private float _hitEnd;
    private float _comboWindowEnd;
    private float _tempo;

    /// <param name="passos">Quantos golpes a cadeia tem.</param>
    /// <param name="hitStart">Quando a área de dano abre, em segundos desde o início do golpe.</param>
    /// <param name="hitEnd">Quando a área de dano fecha.</param>
    /// <param name="comboWindowEnd">Até quando um novo pedido encadeia.</param>
    public MeleeCombo(int passos, float hitStart, float hitEnd, float comboWindowEnd)
    {
        _passos = Mathf.Max(1, passos);
        _hitStartPadrao = _hitStart = hitStart;
        _hitEndPadrao = _hitEnd = hitEnd;
        _comboWindowEndPadrao = _comboWindowEnd = comboWindowEnd;
    }

    /// <summary>Passo atual da cadeia, de 1 a N. Zero quando ocioso.</summary>
    public int Step { get; private set; }

    /// <summary>Se há um golpe em andamento.</summary>
    public bool IsAttacking => Step > 0;

    /// <summary>Se a área de dano deve estar ativa agora.</summary>
    public bool IsHitWindowOpen => IsAttacking && _tempo >= _hitStart && _tempo < _hitEnd;

    /// <summary>
    /// Pede um golpe.
    /// </summary>
    /// <remarks>
    /// Recusa entre o início do golpe e a abertura da janela de acerto: sem isso,
    /// martelar o botão dispara a cadeia inteira num quadro só, pulando as
    /// animações.
    /// </remarks>
    /// <returns>Se o golpe começou.</returns>
    public bool TryStart() => TryStart(_hitStartPadrao, _hitEndPadrao, _comboWindowEndPadrao);

    /// <summary>
    /// Pede um golpe com as janelas do passo que vai comecar.
    /// </summary>
    /// <remarks>
    /// As janelas mudam por passo: o finalizador e mais lento e a janela de
    /// encadeamento mais curta. Fixa-las na construcao faria todos os passos
    /// herdarem o tempo do primeiro, e o acerto sairia fora da animacao.
    /// </remarks>
    public bool TryStart(float hitStart, float hitEnd, float comboWindowEnd)
    {
        if (IsAttacking && _tempo < _hitStart)
            return false;

        Step = IsAttacking && Step < _passos ? Step + 1 : 1;
        _tempo = 0f;
        _hitStart = hitStart;
        _hitEnd = hitEnd;
        _comboWindowEnd = comboWindowEnd;
        return true;
    }

    /// <summary>
    /// Avança o relógio do golpe.
    /// </summary>
    /// <remarks>
    /// Passada a janela de encadeamento, a cadeia se encerra sozinha — e sem
    /// penalidade: o próximo pedido começa no passo 1 imediatamente. Punir quem
    /// erra o ritmo tornaria o combate frustrante sem tornar-se mais profundo.
    /// </remarks>
    public void Advance(float delta)
    {
        if (!IsAttacking)
            return;

        _tempo += delta;

        if (_tempo >= _comboWindowEnd)
            Reset();
    }

    /// <summary>
    /// Interrompe o golpe em andamento.
    /// </summary>
    /// <remarks>
    /// Chamado ao morrer ou tomar atordoamento. Fecha a janela junto — deixar
    /// área de dano ativa órfã é um dos bugs previsíveis da spec 15 §5.
    /// </remarks>
    public void Cancel() => Reset();

    /// <summary>Devolve ao estado de recém-criado. Contrato do pool, no M5.</summary>
    public void Reset()
    {
        Step = 0;
        _tempo = 0f;
    }
}
