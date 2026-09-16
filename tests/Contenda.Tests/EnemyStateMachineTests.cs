using Contenda.Components.AI;
using Xunit;

namespace Contenda.Tests;

/// <summary>Transições de estado do inimigo — ticket 22, spec 09 §2.</summary>
public sealed class EnemyStateMachineTests
{
    private const float Alerta = 0.4f;
    private const float Cooldown = 1.4f;
    private const float Atordoamento = 0.5f;
    private const float PerdaDeAlvo = 3f;
    private const float Passo = 1f / 60f;

    private static EnemyStateMachine Nova() => new(Alerta, Cooldown, Atordoamento, PerdaDeAlvo);

    [Fact]
    public void Comeca_ocioso()
    {
        Assert.Equal(EnemyState.Idle, Nova().Estado);
    }

    [Fact]
    public void Ignora_o_alvo_fora_de_alcance()
    {
        var maquina = Nova();
        maquina.Advance(Passo, alvoVisivel: false, dentroDoAlcanceDeAtaque: false, ataqueTerminou: false);

        Assert.Equal(EnemyState.Idle, maquina.Estado);
    }

    [Fact]
    public void Percebe_o_alvo_e_fica_em_alerta()
    {
        var maquina = Nova();
        maquina.Advance(Passo, alvoVisivel: true, dentroDoAlcanceDeAtaque: false, ataqueTerminou: false);

        Assert.Equal(EnemyState.Alert, maquina.Estado);
    }

    [Fact]
    public void Alerta_nao_vira_perseguicao_antes_da_duracao()
    {
        var maquina = Nova();
        maquina.Advance(Passo, true, false, false);

        for (var i = 0; i < 10; i++) // 10 quadros = 0,167 s, bem menos que 0,4 s
            maquina.Advance(Passo, true, false, false);

        Assert.Equal(EnemyState.Alert, maquina.Estado);
    }

    [Fact]
    public void Alerta_vira_perseguicao_depois_da_duracao()
    {
        var maquina = Nova();
        maquina.Advance(Passo, true, false, false);

        var quadros = (int)System.Math.Ceiling(Alerta / Passo) + 1;
        for (var i = 0; i < quadros; i++)
            maquina.Advance(Passo, true, false, false);

        Assert.Equal(EnemyState.Chase, maquina.Estado);
    }

    [Fact]
    public void Perseguicao_vira_ataque_ao_entrar_no_alcance()
    {
        var maquina = Nova();
        ChegarEmChase(maquina);

        maquina.Advance(Passo, alvoVisivel: true, dentroDoAlcanceDeAtaque: true, ataqueTerminou: false);

        Assert.Equal(EnemyState.Attack, maquina.Estado);
    }

    [Fact]
    public void Perseguicao_nao_ataca_fora_de_alcance()
    {
        var maquina = Nova();
        ChegarEmChase(maquina);

        maquina.Advance(Passo, alvoVisivel: true, dentroDoAlcanceDeAtaque: false, ataqueTerminou: false);

        Assert.Equal(EnemyState.Chase, maquina.Estado);
    }

    [Fact]
    public void Perder_o_alvo_um_instante_nao_desiste_na_hora()
    {
        var maquina = Nova();
        ChegarEmChase(maquina);

        // Bem menos que os 3 s de tolerância.
        for (var i = 0; i < 30; i++)
            maquina.Advance(Passo, alvoVisivel: false, dentroDoAlcanceDeAtaque: false, ataqueTerminou: false);

        Assert.Equal(EnemyState.Chase, maquina.Estado);
    }

    [Fact]
    public void Perder_o_alvo_por_tempo_demais_desiste()
    {
        var maquina = Nova();
        ChegarEmChase(maquina);

        var quadros = (int)System.Math.Ceiling(PerdaDeAlvo / Passo) + 1;
        for (var i = 0; i < quadros; i++)
            maquina.Advance(Passo, alvoVisivel: false, dentroDoAlcanceDeAtaque: false, ataqueTerminou: false);

        Assert.Equal(EnemyState.Idle, maquina.Estado);
    }

    [Fact]
    public void Reencontrar_o_alvo_antes_do_prazo_reseta_o_relogio_de_perda()
    {
        var maquina = Nova();
        ChegarEmChase(maquina);

        for (var i = 0; i < 100; i++) // quase os 3 s
            maquina.Advance(Passo, alvoVisivel: false, dentroDoAlcanceDeAtaque: false, ataqueTerminou: false);

        // Reencontra por um quadro -- o relógio de "sem ver" zera.
        maquina.Advance(Passo, alvoVisivel: true, dentroDoAlcanceDeAtaque: false, ataqueTerminou: false);

        var quadros = (int)System.Math.Ceiling(PerdaDeAlvo / Passo);
        for (var i = 0; i < quadros; i++)
            maquina.Advance(Passo, alvoVisivel: false, dentroDoAlcanceDeAtaque: false, ataqueTerminou: false);

        // Se o relógio não tivesse resetado, já teria estourado os 3 s totais.
        Assert.Equal(EnemyState.Chase, maquina.Estado);
    }

    [Fact]
    public void Ataque_vira_recuperacao_quando_termina()
    {
        var maquina = Nova();
        ChegarEmAttack(maquina);

        maquina.Advance(Passo, true, true, ataqueTerminou: true);

        Assert.Equal(EnemyState.Recover, maquina.Estado);
    }

    [Fact]
    public void Ataque_nao_recupera_antes_de_terminar()
    {
        var maquina = Nova();
        ChegarEmAttack(maquina);

        maquina.Advance(Passo, true, true, ataqueTerminou: false);

        Assert.Equal(EnemyState.Attack, maquina.Estado);
    }

    [Fact]
    public void Recuperacao_volta_a_perseguir_apos_o_cooldown()
    {
        var maquina = Nova();
        ChegarEmAttack(maquina);
        maquina.Advance(Passo, true, true, ataqueTerminou: true);

        var quadros = (int)System.Math.Ceiling(Cooldown / Passo) + 1;
        for (var i = 0; i < quadros; i++)
            maquina.Advance(Passo, true, false, false);

        Assert.Equal(EnemyState.Chase, maquina.Estado);
    }

    [Theory]
    [InlineData(EnemyState.Idle)]
    [InlineData(EnemyState.Alert)]
    [InlineData(EnemyState.Chase)]
    [InlineData(EnemyState.Attack)]
    [InlineData(EnemyState.Recover)]
    public void Apanhar_atordoa_a_partir_de_qualquer_estado(EnemyState partida)
    {
        var maquina = Nova();
        IrPara(maquina, partida);

        maquina.RegistrarGolpeRecebido(lancamentoVertical: false);

        Assert.Equal(EnemyState.Staggered, maquina.Estado);
    }

    [Fact]
    public void Atordoamento_volta_a_perseguir_sozinho()
    {
        var maquina = Nova();
        maquina.RegistrarGolpeRecebido(lancamentoVertical: false);

        var quadros = (int)System.Math.Ceiling(Atordoamento / Passo) + 1;
        for (var i = 0; i < quadros; i++)
            maquina.Advance(Passo, false, false, false);

        Assert.Equal(EnemyState.Chase, maquina.Estado);
    }

    [Fact]
    public void Golpes_seguidos_refrescam_o_atordoamento_em_vez_de_somar()
    {
        var maquina = Nova();
        maquina.RegistrarGolpeRecebido(lancamentoVertical: false);

        var metade = (int)(Atordoamento / Passo / 2);
        for (var i = 0; i < metade; i++)
            maquina.Advance(Passo, false, false, false);

        maquina.RegistrarGolpeRecebido(lancamentoVertical: false); // refresca no meio do caminho

        for (var i = 0; i < metade; i++)
            maquina.Advance(Passo, false, false, false);

        // Sem o refresco, a soma dos dois trechos já teria estourado o
        // atordoamento original.
        Assert.Equal(EnemyState.Staggered, maquina.Estado);
    }

    [Theory]
    [InlineData(EnemyState.Idle)]
    [InlineData(EnemyState.Alert)]
    [InlineData(EnemyState.Chase)]
    [InlineData(EnemyState.Attack)]
    [InlineData(EnemyState.Recover)]
    [InlineData(EnemyState.Staggered)]
    public void Lancamento_vertical_manda_para_o_ar_a_partir_de_qualquer_estado(EnemyState partida)
    {
        var maquina = Nova();
        IrPara(maquina, partida);

        maquina.RegistrarGolpeRecebido(lancamentoVertical: true);

        Assert.Equal(EnemyState.Airborne, maquina.Estado);
    }

    [Fact]
    public void No_ar_nao_sai_sozinho_por_tempo_nenhum()
    {
        var maquina = Nova();
        maquina.RegistrarGolpeRecebido(lancamentoVertical: true);

        // Bem mais tempo que qualquer outro estado temporizado deste
        // arquivo -- "no ar" só sai ao tocar o chão, nunca por relógio.
        for (var i = 0; i < 600; i++)
            maquina.Advance(Passo, alvoVisivel: true, dentroDoAlcanceDeAtaque: true, ataqueTerminou: false, estaNoChao: false);

        Assert.Equal(EnemyState.Airborne, maquina.Estado);
    }

    [Fact]
    public void Tocar_o_chao_tira_do_ar_e_atordoa()
    {
        // Spec 16 §6: "voltando a Staggered ao tocar o chão" -- não direto
        // para Chase. Staggered já se recompõe sozinho (ver
        // Atordoamento_volta_a_perseguir_sozinho), então "volta a perseguir
        // normalmente" do ticket 24 continua valendo, só que com a mesma
        // janela de vulnerabilidade de qualquer outro golpe recebido.
        var maquina = Nova();
        maquina.RegistrarGolpeRecebido(lancamentoVertical: true);

        maquina.Advance(Passo, alvoVisivel: true, dentroDoAlcanceDeAtaque: true, ataqueTerminou: false, estaNoChao: true);

        Assert.Equal(EnemyState.Staggered, maquina.Estado);
    }

    [Fact]
    public void Aterrissar_atordoado_ainda_se_recompoe_sozinho()
    {
        var maquina = Nova();
        maquina.RegistrarGolpeRecebido(lancamentoVertical: true);
        maquina.Advance(Passo, alvoVisivel: true, dentroDoAlcanceDeAtaque: true, ataqueTerminou: false, estaNoChao: true);

        var quadros = (int)System.Math.Ceiling(Atordoamento / Passo) + 1;
        for (var i = 0; i < quadros; i++)
            maquina.Advance(Passo, true, false, false, estaNoChao: true);

        Assert.Equal(EnemyState.Chase, maquina.Estado);
    }

    [Fact]
    public void Golpe_comum_no_ar_nao_interrompe_para_atordoado()
    {
        // O combo aéreo do ticket 19 sustenta o inimigo no alto com vários
        // acertos comuns (horizontais) -- só o impulso vertical, aplicado à
        // parte, é o que conta como "sustentar"; a MÁQUINA de estado só
        // precisa não sair do ar por causa desses acertos comuns.
        var maquina = Nova();
        maquina.RegistrarGolpeRecebido(lancamentoVertical: true);

        maquina.RegistrarGolpeRecebido(lancamentoVertical: false);

        Assert.Equal(EnemyState.Airborne, maquina.Estado);
    }

    [Fact]
    public void Novo_lancamento_vertical_no_ar_refresca_sem_erro()
    {
        var maquina = Nova();
        maquina.RegistrarGolpeRecebido(lancamentoVertical: true);

        maquina.RegistrarGolpeRecebido(lancamentoVertical: true);

        Assert.Equal(EnemyState.Airborne, maquina.Estado);
    }

    [Theory]
    [InlineData(EnemyState.Idle)]
    [InlineData(EnemyState.Alert)]
    [InlineData(EnemyState.Chase)]
    [InlineData(EnemyState.Attack)]
    [InlineData(EnemyState.Recover)]
    [InlineData(EnemyState.Staggered)]
    public void Morte_interrompe_qualquer_estado(EnemyState partida)
    {
        var maquina = Nova();
        IrPara(maquina, partida);

        maquina.RegistrarMorte();

        Assert.Equal(EnemyState.Death, maquina.Estado);
    }

    [Fact]
    public void Morte_no_ar_tambem_funciona()
    {
        // O finalizador de um combo aéreo pode matar um inimigo ainda
        // lançado -- a morte precisa valer a partir de QUALQUER estado,
        // Airborne incluso, e não só dos seis do teste acima.
        var maquina = Nova();
        maquina.RegistrarGolpeRecebido(lancamentoVertical: true);

        maquina.RegistrarMorte();

        Assert.Equal(EnemyState.Death, maquina.Estado);
    }

    [Fact]
    public void Morto_nao_sai_sozinho_por_tempo_nenhum()
    {
        var maquina = Nova();
        maquina.RegistrarMorte();

        // Bem mais tempo que qualquer estado temporizado deste arquivo --
        // "morto" só sai por reciclagem (ResetForSpawn), nunca por relógio
        // nem por qualquer sinal que Advance receba.
        for (var i = 0; i < 600; i++)
            maquina.Advance(Passo, alvoVisivel: true, dentroDoAlcanceDeAtaque: true, ataqueTerminou: true, estaNoChao: true);

        Assert.Equal(EnemyState.Death, maquina.Estado);
    }

    [Fact]
    public void Golpe_depois_de_morto_nao_faz_nada()
    {
        // Defesa de sobra: `HealthState.Apply` já descarta todo golpe contra
        // quem não está vivo, então isto não deveria acontecer de verdade --
        // mas a própria máquina não deveria confiar só nisso.
        var maquina = Nova();
        maquina.RegistrarMorte();

        maquina.RegistrarGolpeRecebido(lancamentoVertical: false);
        Assert.Equal(EnemyState.Death, maquina.Estado);

        maquina.RegistrarGolpeRecebido(lancamentoVertical: true);
        Assert.Equal(EnemyState.Death, maquina.Estado);
    }

    private static void ChegarEmChase(EnemyStateMachine maquina)
    {
        maquina.Advance(Passo, true, false, false);
        var quadros = (int)System.Math.Ceiling(Alerta / Passo) + 1;
        for (var i = 0; i < quadros; i++)
            maquina.Advance(Passo, true, false, false);
    }

    private static void ChegarEmAttack(EnemyStateMachine maquina)
    {
        ChegarEmChase(maquina);
        maquina.Advance(Passo, true, true, false);
    }

    private static void IrPara(EnemyStateMachine maquina, EnemyState alvo)
    {
        switch (alvo)
        {
            case EnemyState.Idle:
                break;
            case EnemyState.Alert:
                maquina.Advance(Passo, true, false, false);
                break;
            case EnemyState.Chase:
                ChegarEmChase(maquina);
                break;
            case EnemyState.Attack:
                ChegarEmAttack(maquina);
                break;
            case EnemyState.Recover:
                ChegarEmAttack(maquina);
                maquina.Advance(Passo, true, true, true);
                break;
            default:
                break;
        }
    }
}
