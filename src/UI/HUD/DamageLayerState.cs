using Godot;

namespace Contenda.UI.HUD;

/// <summary>
/// A camada de dano atrasada de uma barra de vida — sem nenhum nó envolvido.
/// </summary>
/// <remarks>
/// POCO de propósito, como o <c>MeleeCombo</c>: é uma janela de tempo, e é
/// exatamente aí que erro de comparação passa despercebido. Ver ticket 12.
///
/// O preenchimento principal (<see cref="Current"/>) muda na hora. A camada de
/// dano (<see cref="DamageLayer"/>) fica para trás numa QUEDA e esvazia até
/// alcançar o valor atual no tempo configurado — comunicando quanto se levou,
/// não só quanto sobrou. Numa SUBIDA (cura), as duas se alinham na hora: não
/// há "cura atrasada" para comunicar.
///
/// A velocidade de esvaziamento é calculada UMA VEZ, no instante do golpe —
/// nunca recalculada a cada <see cref="Tick"/> a partir do que resta. Recalcular
/// a cada quadro (gap restante / duração total) produz uma curva exponencial
/// que se aproxima do alvo sem nunca chegar exatamente nele; fixar a
/// velocidade, como o <c>LungeMotion</c> já faz para o avanço do golpe, é o
/// que garante o esvaziamento LINEAR que a spec pede — de verdade em 0,4 s, e
/// não "a maior parte disso".
/// </remarks>
public sealed class DamageLayerState
{
    private readonly float _catchUpDuration;
    private float _porSegundo;

    public DamageLayerState(float valorInicial, float catchUpDuration)
    {
        Current = valorInicial;
        DamageLayer = valorInicial;
        _catchUpDuration = Mathf.Max(0f, catchUpDuration);
    }

    /// <summary>O preenchimento principal — muda instantaneamente.</summary>
    public float Current { get; private set; }

    /// <summary>A fatia que ainda não caiu, mostrando quanto se tinha antes do golpe.</summary>
    public float DamageLayer { get; private set; }

    /// <summary>
    /// Informa o novo valor atual.
    /// </summary>
    /// <remarks>
    /// Chamado todo quadro, tenha o valor mudado ou não — quem chama (a barra)
    /// não sabe se houve golpe, só lê o estado atual. Por isso a comparação é
    /// estrita: reafirmar o mesmo valor não pode reiniciar nem travar a
    /// animação em andamento.
    /// </remarks>
    public void SetCurrent(float novoValor)
    {
        if (novoValor < Current)
        {
            // A camada de dano assume o pico mais alto entre onde já estava e
            // de onde a vida caiu agora -- um segundo golpe durante o
            // esvaziamento não deveria fazer a fatia visível encolher de volta.
            DamageLayer = Mathf.Max(DamageLayer, Current);

            var gap = DamageLayer - novoValor;
            _porSegundo = _catchUpDuration > 0f ? gap / _catchUpDuration : float.PositiveInfinity;
        }
        else if (novoValor > Current)
        {
            // Cura: sem atraso, as duas camadas sobem juntas.
            DamageLayer = novoValor;
        }

        Current = novoValor;
    }

    /// <summary>Esvazia a camada de dano em direção ao valor atual.</summary>
    public void Tick(float delta)
    {
        if (DamageLayer <= Current || delta <= 0f)
            return;

        DamageLayer = Mathf.Max(Current, DamageLayer - (_porSegundo * delta));
    }
}
