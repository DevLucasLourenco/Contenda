using Contenda.Input;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// O buffer que grava sequências de WASD sem atrapalhar o movimento — ticket 13.
/// </summary>
/// <remarks>
/// A disciplina de só chamar <see cref="CommandBuffer.Push"/> na BORDA DE
/// SUBIDA (nunca em estado contínuo) é do CHAMADOR, não do buffer — é por isso
/// que "segurar W por 3 s grava só 1 símbolo" não precisa de um teste de
/// input de verdade: chamar <c>Push</c> uma vez é literalmente o que "tocar a
/// tecla uma vez" significa aqui. Ver spec 03 §3.
/// </remarks>
public sealed class CommandBufferTests
{
    [Fact]
    public void Comeca_vazio()
    {
        var buffer = new CommandBuffer();
        Assert.Equal(0, buffer.Current.Length);
    }

    [Fact]
    public void Push_acrescenta_ao_fim()
    {
        var buffer = new CommandBuffer();
        buffer.Push(CommandDirection.Up, now: 0f);
        buffer.Push(CommandDirection.Up, now: 0.1f);

        Assert.Equal(2, buffer.Current.Length);
        Assert.Equal(CommandDirection.Up, buffer.Current[0]);
        Assert.Equal(CommandDirection.Up, buffer.Current[1]);
    }

    [Fact]
    public void Tocar_duas_teclas_diferentes_rapido_grava_as_duas_na_ordem()
    {
        var buffer = new CommandBuffer();
        buffer.Push(CommandDirection.Up, now: 0f);
        buffer.Push(CommandDirection.Left, now: 0.05f);

        Assert.Equal([CommandDirection.Up, CommandDirection.Left], buffer.Current.ToArray());
    }

    [Fact]
    public void Capacidade_descarta_o_mais_antigo_primeiro()
    {
        var buffer = new CommandBuffer(capacity: 3, tokenLifetime: 100f, sequenceTimeout: 100f);
        buffer.Push(CommandDirection.Up, now: 0f);
        buffer.Push(CommandDirection.Down, now: 1f);
        buffer.Push(CommandDirection.Left, now: 2f);
        buffer.Push(CommandDirection.Right, now: 3f); // estoura a capacidade

        Assert.Equal(
            [CommandDirection.Down, CommandDirection.Left, CommandDirection.Right],
            buffer.Current.ToArray());
    }

    [Fact]
    public void Um_simbolo_isolado_some_sozinho_depois_do_TokenLifetime()
    {
        var buffer = new CommandBuffer(tokenLifetime: 0.7f, sequenceTimeout: 100f);
        buffer.Push(CommandDirection.Up, now: 0f);

        buffer.Tick(now: 0.69f);
        Assert.Equal(1, buffer.Current.Length);

        buffer.Tick(now: 0.71f);
        Assert.Equal(0, buffer.Current.Length);
    }

    [Fact]
    public void A_janela_deslizante_descarta_so_o_token_que_envelheceu_nao_a_sequencia_inteira()
    {
        // Um token entra a cada 0,3 s -- nenhum vive o suficiente para o
        // primeiro ultrapassar TokenLifetime (0,7 s) antes do segundo chegar,
        // mas em algum momento o MAIS VELHO ainda vai envelhecer e sair
        // sozinho, sem levar o resto da sequência junto.
        var buffer = new CommandBuffer(tokenLifetime: 0.7f, sequenceTimeout: 100f);
        buffer.Push(CommandDirection.Up, now: 0f);
        buffer.Push(CommandDirection.Down, now: 0.3f);
        buffer.Push(CommandDirection.Left, now: 0.6f);

        buffer.Tick(now: 0.75f); // Up (idade 0,75) expira; Down (0,45) e Left (0,15) não

        Assert.Equal([CommandDirection.Down, CommandDirection.Left], buffer.Current.ToArray());
    }

    [Fact]
    public void A_sequencia_inteira_e_descartada_se_o_primeiro_token_passar_do_SequenceTimeout()
    {
        // TokenLifetime bem maior que SequenceTimeout, para isolar esta regra
        // da janela deslizante -- as duas expiram por motivos diferentes
        // (spec 03 §5) e precisam de teste em separado.
        var buffer = new CommandBuffer(tokenLifetime: 100f, sequenceTimeout: 1.2f);
        buffer.Push(CommandDirection.Up, now: 0f);
        buffer.Push(CommandDirection.Down, now: 1.0f);

        buffer.Tick(now: 1.19f);
        Assert.Equal(2, buffer.Current.Length);

        buffer.Tick(now: 1.21f);
        Assert.Equal(0, buffer.Current.Length);
    }

    [Fact]
    public void Sequencia_de_tres_tokens_dentro_da_janela_fica_intacta()
    {
        var buffer = new CommandBuffer(tokenLifetime: 0.7f, sequenceTimeout: 1.2f);
        buffer.Push(CommandDirection.Up, now: 0f);
        buffer.Tick(now: 0.2f);
        buffer.Push(CommandDirection.Up, now: 0.2f);
        buffer.Tick(now: 0.4f);
        buffer.Push(CommandDirection.Left, now: 0.4f);
        buffer.Tick(now: 0.4f);

        Assert.Equal(
            [CommandDirection.Up, CommandDirection.Up, CommandDirection.Left],
            buffer.Current.ToArray());
    }

    [Fact]
    public void Parar_no_meio_deixa_a_sequencia_expirar_por_completo()
    {
        var buffer = new CommandBuffer(tokenLifetime: 0.7f, sequenceTimeout: 1.2f);
        buffer.Push(CommandDirection.Up, now: 0f);
        buffer.Push(CommandDirection.Up, now: 0.2f);
        // Jogador para de digitar aqui -- nenhum novo Push.

        buffer.Tick(now: 2.0f); // bem além dos dois limites

        Assert.Equal(0, buffer.Current.Length);
    }

    [Fact]
    public void Clear_esvazia_na_hora()
    {
        var buffer = new CommandBuffer();
        buffer.Push(CommandDirection.Up, now: 0f);
        buffer.Push(CommandDirection.Down, now: 0.1f);

        buffer.Clear();

        Assert.Equal(0, buffer.Current.Length);
    }

    [Fact]
    public void Clear_em_buffer_ja_vazio_nao_da_erro()
    {
        var buffer = new CommandBuffer();
        buffer.Clear();
        Assert.Equal(0, buffer.Current.Length);
    }

    [Fact]
    public void Tick_em_buffer_vazio_nao_da_erro()
    {
        var buffer = new CommandBuffer();
        buffer.Tick(now: 5f);
        Assert.Equal(0, buffer.Current.Length);
    }

    [Fact]
    public void Changed_dispara_ao_empurrar()
    {
        var buffer = new CommandBuffer();
        CommandDirection[]? recebido = null;
        buffer.Changed += seq => recebido = seq.ToArray();

        buffer.Push(CommandDirection.Right, now: 0f);

        Assert.NotNull(recebido);
        Assert.Equal([CommandDirection.Right], recebido);
    }

    [Fact]
    public void Changed_dispara_ao_expirar()
    {
        var buffer = new CommandBuffer(tokenLifetime: 0.5f, sequenceTimeout: 100f);
        buffer.Push(CommandDirection.Right, now: 0f);

        var disparos = 0;
        buffer.Changed += _ => disparos++;

        buffer.Tick(now: 0.6f);

        Assert.Equal(1, disparos);
    }

    [Fact]
    public void Changed_nao_dispara_num_Tick_que_nao_muda_nada()
    {
        var buffer = new CommandBuffer(tokenLifetime: 100f, sequenceTimeout: 100f);
        buffer.Push(CommandDirection.Right, now: 0f);

        var disparos = 0;
        buffer.Changed += _ => disparos++;

        buffer.Tick(now: 1f); // nada expira ainda

        Assert.Equal(0, disparos);
    }

    [Fact]
    public void Changed_dispara_ao_limpar_um_buffer_nao_vazio()
    {
        var buffer = new CommandBuffer();
        buffer.Push(CommandDirection.Up, now: 0f);

        var disparos = 0;
        buffer.Changed += _ => disparos++;

        buffer.Clear();

        Assert.Equal(1, disparos);
    }

    [Fact]
    public void Changed_nao_dispara_ao_limpar_um_buffer_ja_vazio()
    {
        var buffer = new CommandBuffer();
        var disparos = 0;
        buffer.Changed += _ => disparos++;

        buffer.Clear();

        Assert.Equal(0, disparos);
    }

    [Fact]
    public void Capacidade_minima_e_um()
    {
        var buffer = new CommandBuffer(capacity: 0, tokenLifetime: 100f, sequenceTimeout: 100f);
        buffer.Push(CommandDirection.Up, now: 0f);
        buffer.Push(CommandDirection.Down, now: 0.1f);

        Assert.Equal(1, buffer.Current.Length);
        Assert.Equal(CommandDirection.Down, buffer.Current[0]);
    }
}
