using Contenda.GameModes.Horde;
using Xunit;

namespace Contenda.Tests;

/// <summary>Interleaving ponderado de entradas de spawn de uma onda — ticket 27, spec 10 §3.</summary>
public sealed class SpawnQueueTests
{
    [Fact]
    public void Vazia_de_inicio_nao_e_vazia_com_contagem_positiva()
    {
        var fila = new SpawnQueue([(Count: 3, Weight: 1f, DelayBeforeFirst: 0f)]);

        Assert.False(fila.IsEmpty);
    }

    [Fact]
    public void Fica_vazia_depois_de_consumir_toda_a_contagem()
    {
        var fila = new SpawnQueue([(Count: 2, Weight: 1f, DelayBeforeFirst: 0f)]);

        fila.Consume(0);
        Assert.False(fila.IsEmpty);
        fila.Consume(0);
        Assert.True(fila.IsEmpty);
    }

    [Fact]
    public void Entrada_com_atraso_inicial_nao_e_elegivel_antes_dele_zerar()
    {
        var fila = new SpawnQueue([(Count: 1, Weight: 1f, DelayBeforeFirst: 2f)]);

        Assert.Equal(-1, fila.ChooseNext(sorteio01: 0f));

        fila.Tick(1.9f);
        Assert.Equal(-1, fila.ChooseNext(sorteio01: 0f));

        fila.Tick(0.2f);
        Assert.Equal(0, fila.ChooseNext(sorteio01: 0f));
    }

    [Fact]
    public void Entrada_esgotada_para_de_ser_elegivel()
    {
        var fila = new SpawnQueue([(Count: 1, Weight: 1f, DelayBeforeFirst: 0f)]);

        fila.Consume(0);

        Assert.Equal(-1, fila.ChooseNext(sorteio01: 0.5f));
    }

    [Fact]
    public void Sem_nenhuma_entrada_elegivel_devolve_menos_um()
    {
        var fila = new SpawnQueue([(Count: 1, Weight: 1f, DelayBeforeFirst: 5f)]);

        Assert.Equal(-1, fila.ChooseNext(sorteio01: 0.5f));
    }

    [Fact]
    public void Duas_entradas_elegiveis_intercalam_por_peso()
    {
        // Pesos iguais [1, 1]: sorteio 0,5 cai exatamente na fronteira, escolhendo a segunda (índice 1).
        var fila = new SpawnQueue([(Count: 5, Weight: 1f, DelayBeforeFirst: 0f), (Count: 5, Weight: 1f, DelayBeforeFirst: 0f)]);

        Assert.Equal(1, fila.ChooseNext(sorteio01: 0.5f));
        Assert.Equal(0, fila.ChooseNext(sorteio01: 0f));
    }

    [Fact]
    public void Peso_maior_de_uma_entrada_a_favorece_no_sorteio()
    {
        // Pesos [3, 1]: total 4. Só sorteios >= 0,75 caem na segunda entrada.
        var fila = new SpawnQueue([(Count: 5, Weight: 3f, DelayBeforeFirst: 0f), (Count: 5, Weight: 1f, DelayBeforeFirst: 0f)]);

        Assert.Equal(0, fila.ChooseNext(sorteio01: 0.5f));
        Assert.Equal(1, fila.ChooseNext(sorteio01: 0.9f));
    }

    [Fact]
    public void Entrada_esgotada_nao_disputa_mais_o_sorteio_mesmo_favorecida()
    {
        // A entrada 0 tem peso maior, mas já esgotou -- só a 1 pode ser escolhida.
        var fila = new SpawnQueue([(Count: 1, Weight: 10f, DelayBeforeFirst: 0f), (Count: 1, Weight: 1f, DelayBeforeFirst: 0f)]);
        fila.Consume(0);

        Assert.Equal(1, fila.ChooseNext(sorteio01: 0.01f));
    }
}
