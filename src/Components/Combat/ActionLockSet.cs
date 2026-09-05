using System.Collections.Generic;

namespace Contenda.Components.Combat;

/// <summary>
/// Trava ações do personagem, por fonte e com duração, somadas por OR.
/// </summary>
/// <remarks>
/// POCO de propósito, como o <c>MeleeCombo</c>: janelas de tempo são onde erro
/// de comparação passa despercebido, e testá-las exige controlar o relógio —
/// só dá para fazer fora da engine. É aqui que mora o bug clássico da spec 07
/// §8 — duas fontes travam o mesmo movimento, uma expira, e um bool único
/// destravaria cedo demais. Cada fonte tem a própria contagem regressiva; o
/// agregado só libera uma flag quando a ÚLTIMA fonte que a pedia expira ou é
/// liberada.
///
/// A fonte é <c>string</c>, não <c>StringName</c>: construir um
/// <c>StringName</c> fora do processo do Godot derruba o processo (não lança
/// exceção — é <c>AccessViolationException</c> nativa), e esta classe existe
/// justamente para ser testada em xUnit, fora da engine. Mesma escolha do
/// <c>SourceTag</c> em <c>DamageInfo</c>.
/// </remarks>
public sealed class ActionLockSet
{
    private readonly Dictionary<string, (ActionLock Flags, float Remaining)> _travas = new();
    private readonly List<string> _expiradas = [];

    /// <summary>A união de todas as travas ativas agora.</summary>
    public ActionLock Current { get; private set; }

    /// <summary>
    /// Aplica ou renova a trava desta fonte, pela duração dada.
    /// </summary>
    /// <remarks>
    /// Renovar SUBSTITUI a entrada da fonte, não soma a duração: quem chama é
    /// quem sabe por quanto tempo ainda precisa da trava, e uma fonte
    /// refrescada a cada quadro não pode acumular duração sem limite.
    /// Duração zero ou negativa, ou nenhuma flag, equivale a <see cref="Clear"/>.
    /// </remarks>
    public void Apply(string source, ActionLock flags, float duration)
    {
        if (duration <= 0f || flags == ActionLock.None)
        {
            Clear(source);
            return;
        }

        _travas[source] = (flags, duration);
        Recompute();
    }

    /// <summary>Libera a trava desta fonte agora, mesmo com duração restante.</summary>
    public void Clear(string source)
    {
        if (_travas.Remove(source))
            Recompute();
    }

    /// <summary>Envelhece as travas ativas pelo quadro; remove as que expiraram.</summary>
    public void Tick(float delta)
    {
        if (_travas.Count == 0)
            return;

        _expiradas.Clear();

        foreach (var trava in _travas)
        {
            var restante = trava.Value.Remaining - delta;
            if (restante <= 0f)
                _expiradas.Add(trava.Key);
            else
                _travas[trava.Key] = (trava.Value.Flags, restante);
        }

        if (_expiradas.Count == 0)
            return;

        for (var i = 0; i < _expiradas.Count; i++)
            _travas.Remove(_expiradas[i]);

        Recompute();
    }

    /// <summary>Devolve ao estado de recém-criado. Contrato do pool, no M5.</summary>
    public void Reset()
    {
        _travas.Clear();
        Current = ActionLock.None;
    }

    private void Recompute()
    {
        var uniao = ActionLock.None;
        foreach (var trava in _travas.Values)
            uniao |= trava.Flags;

        Current = uniao;
    }
}
