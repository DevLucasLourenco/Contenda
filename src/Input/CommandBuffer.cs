using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Contenda.Input;

/// <summary>
/// Grava a sequência de WASD recente do jogador, sem nenhum nó envolvido.
/// </summary>
/// <remarks>
/// POCO de propósito, como o <c>MeleeCombo</c>: expiração é janela de tempo, e
/// é onde erro de comparação passa despercebido.
///
/// **A disciplina de só chamar <see cref="Push"/> na borda de subida
/// (<c>IsActionJustPressed</c>), nunca em estado contínuo, é do CHAMADOR.**
/// O buffer não lê teclado nem sabe a diferença entre segurar e tocar — ele só
/// grava o que mandarem gravar. É assim que "segurar W por 3 s grava 1
/// símbolo" vira uma propriedade do <c>PlayerInputController</c>, não deste
/// tipo. Ver spec 03 §3 e o ticket 13.
///
/// Duas regras de expiração independentes, spec 03 §5: <see cref="TokenLifetime"/>
/// é uma janela deslizante por TOKEN (o mais velho sai sozinho, sem levar o
/// resto); <see cref="SequenceTimeout"/> é um teto sobre a sequência INTEIRA
/// desde o primeiro símbolo. Sob a sintonia padrão (0,7 s / 1,2 s) a primeira
/// quase sempre dispara antes da segunda ter chance — a segunda existe como
/// rede de segurança para quando alguém tunar <see cref="TokenLifetime"/> para
/// cima sem lembrar do teto. Por isso as duas são mantidas, e testadas, em
/// separado.
/// </remarks>
public sealed class CommandBuffer
{
    // Duas listas em lockstep, não uma: _tokens guarda o carimbo de tempo que
    // a expiração precisa, _direcoes existe só para Current devolver um
    // ReadOnlySpan sem alocar (CollectionsMarshal.AsSpan exige um List<T> do
    // tipo exato do Span). QUALQUER mutação (Push/Tick/Clear) tem que tocar
    // as duas juntas, na mesma ordem -- é o invariante que os três métodos
    // abaixo mantêm.
    private readonly List<CommandToken> _tokens = [];
    private readonly List<CommandDirection> _direcoes = [];

    public CommandBuffer(int capacity = 6, float tokenLifetime = 0.70f, float sequenceTimeout = 1.20f)
    {
        Capacity = Math.Max(1, capacity);
        TokenLifetime = Math.Max(0f, tokenLifetime);
        SequenceTimeout = Math.Max(0f, sequenceTimeout);
    }

    /// <summary>Quantos símbolos cabem antes de descartar o mais antigo.</summary>
    public int Capacity { get; }

    /// <summary>Quanto tempo um símbolo, sozinho, permanece válido, em segundos.</summary>
    public float TokenLifetime { get; }

    /// <summary>Teto de idade da sequência inteira, desde o primeiro símbolo, em segundos.</summary>
    public float SequenceTimeout { get; }

    /// <summary>A sequência atual, mais antigo primeiro.</summary>
    public ReadOnlySpan<CommandDirection> Current => CollectionsMarshal.AsSpan(_direcoes);

    /// <summary>Avisa que a sequência mudou — o HUD ao vivo assina isto.</summary>
    public event Action<ReadOnlyMemory<CommandDirection>>? Changed;

    /// <summary>
    /// Grava um símbolo. Chamado só na borda de subida da tecla — ver o
    /// comentário da classe.
    /// </summary>
    /// <param name="dir">Qual tecla.</param>
    /// <param name="now">Relógio de quem chama; o buffer não lê nenhum.</param>
    public void Push(CommandDirection dir, float now)
    {
        _tokens.Add(new CommandToken(dir, now));
        _direcoes.Add(dir);

        if (_tokens.Count > Capacity)
        {
            _tokens.RemoveAt(0);
            _direcoes.RemoveAt(0);
        }

        Notificar();
    }

    /// <summary>Envelhece a sequência, aplicando as duas regras de expiração.</summary>
    public void Tick(float now)
    {
        if (_tokens.Count == 0)
            return;

        // Timeout da sequência: o primeiro símbolo já passou do teto -- some
        // TUDO de uma vez, não só o mais velho. É uma regra diferente da
        // janela deslizante abaixo, não uma versão mais estrita dela.
        if (now - _tokens[0].TimestampSeconds > SequenceTimeout)
        {
            _tokens.Clear();
            _direcoes.Clear();
            Notificar();
            return;
        }

        // Janela deslizante: cada símbolo sai sozinho quando envelhece além de
        // TokenLifetime, sem levar o resto da sequência.
        var mudou = false;
        while (_tokens.Count > 0 && now - _tokens[0].TimestampSeconds > TokenLifetime)
        {
            _tokens.RemoveAt(0);
            _direcoes.RemoveAt(0);
            mudou = true;
        }

        if (mudou)
            Notificar();
    }

    /// <summary>
    /// Esvazia a sequência na hora. Chamado ao executar com sucesso, ao
    /// confirmar sem match, e ao morrer, atordoar ou pausar — spec 03 §5.
    /// </summary>
    public void Clear()
    {
        if (_tokens.Count == 0)
            return;

        _tokens.Clear();
        _direcoes.Clear();
        Notificar();
    }

    private void Notificar() => Changed?.Invoke(_direcoes.ToArray());
}
