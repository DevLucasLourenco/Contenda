using Contenda.UI.HUD;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// A camada de dano atrasada de uma barra de vida.
/// </summary>
/// <remarks>
/// A fatia perdida esvazia 0,4 s depois do golpe, comunicando quanto se levou
/// — ticket 12. É uma janela de tempo, e é exatamente aí que erro de
/// comparação passa despercebido: por isso testado isolado, como o
/// <c>MeleeCombo</c>.
/// </remarks>
public sealed class DamageLayerStateTests
{
    private const float Tol = 0.001f;
    private const float CatchUp = 0.4f;

    [Fact]
    public void Nasce_com_as_duas_camadas_no_valor_inicial()
    {
        var barra = new DamageLayerState(1f, CatchUp);
        Assert.Equal(1f, barra.Current, Tol);
        Assert.Equal(1f, barra.DamageLayer, Tol);
    }

    [Fact]
    public void Perder_valor_move_o_preenchimento_na_hora()
    {
        var barra = new DamageLayerState(1f, CatchUp);
        barra.SetCurrent(0.6f);

        Assert.Equal(0.6f, barra.Current, Tol);
    }

    [Fact]
    public void Perder_valor_mantem_a_camada_de_dano_no_valor_antigo_por_um_instante()
    {
        var barra = new DamageLayerState(1f, CatchUp);
        barra.SetCurrent(0.6f);

        // Sem Tick nenhum ainda: a camada de dano continua mostrando o quanto
        // se tinha ANTES do golpe.
        Assert.Equal(1f, barra.DamageLayer, Tol);
    }

    [Fact]
    public void A_camada_de_dano_esvazia_ate_o_valor_atual_no_tempo_configurado()
    {
        var barra = new DamageLayerState(1f, CatchUp);
        barra.SetCurrent(0.6f);

        barra.Tick(CatchUp);

        Assert.Equal(0.6f, barra.DamageLayer, Tol);
    }

    [Fact]
    public void A_camada_de_dano_nao_chega_ao_valor_atual_antes_da_hora()
    {
        var barra = new DamageLayerState(1f, CatchUp);
        barra.SetCurrent(0.6f);

        barra.Tick(CatchUp * 0.5f);

        // Na metade do caminho, meio caminho percorrido: 1,0 -> 0,8.
        Assert.Equal(0.8f, barra.DamageLayer, Tol);
    }

    [Fact]
    public void A_camada_de_dano_nao_ultrapassa_o_valor_atual()
    {
        var barra = new DamageLayerState(1f, CatchUp);
        barra.SetCurrent(0.6f);

        barra.Tick(CatchUp * 10f); // bem além do necessário

        Assert.Equal(0.6f, barra.DamageLayer, Tol);
    }

    [Fact]
    public void Curar_alinha_a_camada_de_dano_na_hora_sem_atraso()
    {
        var barra = new DamageLayerState(1f, CatchUp);
        barra.SetCurrent(0.3f);
        barra.Tick(0.1f); // ainda no meio do esvaziamento

        barra.SetCurrent(0.8f); // cura

        Assert.Equal(0.8f, barra.Current, Tol);
        Assert.Equal(0.8f, barra.DamageLayer, Tol);
    }

    [Fact]
    public void Valor_inalterado_nao_reinicia_a_animacao_em_andamento()
    {
        // Um _Process que chama SetCurrent todo quadro, tenha a vida mudado ou
        // não, não pode fazer a camada de dano "grudar" no valor antigo para
        // sempre -- reafirmar o mesmo valor não é nem dano nem cura.
        var barra = new DamageLayerState(1f, CatchUp);
        barra.SetCurrent(0.6f);
        barra.Tick(CatchUp * 0.5f); // DamageLayer em 0,8

        barra.SetCurrent(0.6f); // mesmo valor de novo, sem novo golpe
        Assert.Equal(0.8f, barra.DamageLayer, Tol);

        barra.Tick(CatchUp * 0.5f);
        Assert.Equal(0.6f, barra.DamageLayer, Tol);
    }

    [Fact]
    public void Um_segundo_golpe_durante_o_esvaziamento_reinicia_a_janela_pelo_novo_total()
    {
        var barra = new DamageLayerState(1f, CatchUp);
        barra.SetCurrent(0.6f);
        barra.Tick(CatchUp * 0.5f); // DamageLayer em 0,8, Current em 0,6

        barra.SetCurrent(0.3f); // novo golpe, ainda no meio da animação anterior

        Assert.Equal(0.3f, barra.Current, Tol);
        Assert.Equal(0.8f, barra.DamageLayer, Tol); // continua do ponto onde estava

        barra.Tick(CatchUp);
        Assert.Equal(0.3f, barra.DamageLayer, Tol); // nova janela cheia até o novo valor
    }

    [Fact]
    public void Esvazia_linearmente_sob_muitos_quadros_pequenos_nao_exponencialmente()
    {
        // Recalcular a velocidade a cada Tick, a partir do que resta, produz
        // uma curva exponencial que se aproxima do alvo sem nunca alcançá-lo.
        // A velocidade tem que ser fixada UMA VEZ no golpe -- como o
        // LungeMotion já faz para o avanço -- para esvaziar de verdade em
        // exatamente 0,4 s, e não "a maior parte disso" depois de 0,4 s.
        var barra = new DamageLayerState(1f, CatchUp);
        barra.SetCurrent(0.6f);

        const float passo = 1f / 60f;
        for (var t = 0f; t < CatchUp; t += passo)
            barra.Tick(passo);

        Assert.Equal(0.6f, barra.DamageLayer, Tol);
    }

    [Fact]
    public void Tick_sem_diferenca_nenhuma_nao_da_erro()
    {
        var barra = new DamageLayerState(1f, CatchUp);
        barra.Tick(1f / 60f);

        Assert.Equal(1f, barra.DamageLayer, Tol);
    }

    [Fact]
    public void Duracao_de_catchup_zero_esvazia_no_proximo_Tick()
    {
        var barra = new DamageLayerState(1f, catchUpDuration: 0f);
        barra.SetCurrent(0.5f);

        barra.Tick(1f / 60f);

        Assert.Equal(0.5f, barra.DamageLayer, Tol);
    }
}
