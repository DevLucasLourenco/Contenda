using System;
using Contenda.Input;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// A trie de prefixos que casa uma sequência com uma habilidade — ticket 13.
/// </summary>
/// <remarks>
/// Genérico sobre <c>T</c>, e não fixado em <c>AbilityDefinition</c>: esta
/// classe nasce ANTES da habilidade existir como dado (ticket 14), e
/// <c>AbilityDefinition</c> é um <c>Resource</c> — construí-lo fora do
/// processo do Godot derruba o processo (mesmo motivo do <c>StringName</c>,
/// documentado em <c>Contenda.Tests.csproj</c>). Um <c>record</c> comum
/// (<see cref="Habilidade"/>) prova a mesma lógica sem precisar da engine; o
/// ticket 14 só troca o tipo genérico na hora de montar o componente de
/// verdade.
/// </remarks>
public sealed class AbilityComboResolverTests
{
    private sealed record Habilidade(string Nome, CommandDirection[] Sequencia);

    private static AbilityComboResolver<Habilidade> Criar(params Habilidade[] habilidades)
        => new(habilidades, h => h.Sequencia, h => h.Nome);

    [Fact]
    public void Resolve_uma_sequencia_vazia_sem_nenhuma_habilidade_de_zero_tokens()
    {
        var resolver = Criar(new Habilidade("DashSlash", [CommandDirection.Up, CommandDirection.Up]));

        Assert.Null(resolver.Resolve([]));
    }

    [Fact]
    public void Resolve_casa_exato()
    {
        var dashSlash = new Habilidade("DashSlash", [CommandDirection.Up, CommandDirection.Up]);
        var resolver = Criar(dashSlash);

        var resultado = resolver.Resolve([CommandDirection.Up, CommandDirection.Up]);

        Assert.Same(dashSlash, resultado);
    }

    [Fact]
    public void Resolve_nao_casa_uma_sequencia_diferente()
    {
        var resolver = Criar(new Habilidade("DashSlash", [CommandDirection.Up, CommandDirection.Up]));

        Assert.Null(resolver.Resolve([CommandDirection.Down, CommandDirection.Down]));
    }

    [Fact]
    public void Resolve_nao_casa_um_prefixo_sem_habilidade_propria_ali()
    {
        // [W,W,A] existe: [W,W] sozinho não tem habilidade nenhuma no nó dele.
        var resolver = Criar(new Habilidade("HeavyLunge", [CommandDirection.Up, CommandDirection.Up, CommandDirection.Left]));

        Assert.Null(resolver.Resolve([CommandDirection.Up, CommandDirection.Up]));
    }

    [Fact]
    public void Resolve_nao_casa_uma_sequencia_mais_longa_que_a_declarada()
    {
        var resolver = Criar(new Habilidade("DashSlash", [CommandDirection.Up, CommandDirection.Up]));

        Assert.Null(resolver.Resolve([CommandDirection.Up, CommandDirection.Up, CommandDirection.Left]));
    }

    [Fact]
    public void Uma_sequencia_pode_ser_prefixo_de_outra_e_as_duas_resolvem_seus_proprios_nos()
    {
        var dashSlash = new Habilidade("DashSlash", [CommandDirection.Up, CommandDirection.Up]);
        var heavyLunge = new Habilidade("HeavyLunge", [CommandDirection.Up, CommandDirection.Up, CommandDirection.Left]);
        var resolver = Criar(dashSlash, heavyLunge);

        Assert.Same(dashSlash, resolver.Resolve([CommandDirection.Up, CommandDirection.Up]));
        Assert.Same(heavyLunge, resolver.Resolve([CommandDirection.Up, CommandDirection.Up, CommandDirection.Left]));
    }

    [Fact]
    public void Candidates_devolve_habilidades_cujo_prefixo_e_a_sequencia_dada()
    {
        var dashSlash = new Habilidade("DashSlash", [CommandDirection.Up, CommandDirection.Up]);
        var heavyLunge = new Habilidade("HeavyLunge", [CommandDirection.Up, CommandDirection.Up, CommandDirection.Left]);
        var risingSlash = new Habilidade("RisingSlash", [CommandDirection.Down, CommandDirection.Up]);
        var resolver = Criar(dashSlash, heavyLunge, risingSlash);

        var candidatas = resolver.Candidates([CommandDirection.Up, CommandDirection.Up]);

        Assert.Equal(2, candidatas.Count);
        Assert.Contains(dashSlash, candidatas);
        Assert.Contains(heavyLunge, candidatas);
    }

    [Fact]
    public void Candidates_de_sequencia_vazia_devolve_todas_as_habilidades()
    {
        var dashSlash = new Habilidade("DashSlash", [CommandDirection.Up, CommandDirection.Up]);
        var risingSlash = new Habilidade("RisingSlash", [CommandDirection.Down, CommandDirection.Up]);
        var resolver = Criar(dashSlash, risingSlash);

        var candidatas = resolver.Candidates([]);

        Assert.Equal(2, candidatas.Count);
    }

    [Fact]
    public void Candidates_sem_nenhum_prefixo_correspondente_devolve_lista_vazia()
    {
        var resolver = Criar(new Habilidade("DashSlash", [CommandDirection.Up, CommandDirection.Up]));

        Assert.Empty(resolver.Candidates([CommandDirection.Left]));
    }

    [Fact]
    public void Duas_habilidades_com_a_mesma_sequencia_quebram_a_construcao()
    {
        var a = new Habilidade("DashSlash", [CommandDirection.Up, CommandDirection.Up]);
        var b = new Habilidade("OutraComAMesmaSequencia", [CommandDirection.Up, CommandDirection.Up]);

        var excecao = Assert.Throws<InvalidOperationException>(() => Criar(a, b));

        // Mensagem clara -- ticket 13 exige isso explicitamente, não só a exceção.
        Assert.Contains("DashSlash", excecao.Message);
        Assert.Contains("OutraComAMesmaSequencia", excecao.Message);
    }

    [Fact]
    public void Construir_sem_nenhuma_habilidade_nao_da_erro_e_nunca_resolve_nada()
    {
        var resolver = new AbilityComboResolver<Habilidade>([], h => h.Sequencia);

        Assert.Null(resolver.Resolve([CommandDirection.Up]));
        Assert.Empty(resolver.Candidates([]));
    }

    [Fact]
    public void Duas_habilidades_com_sequencias_de_um_token_cada_nao_colidem()
    {
        var slash = new Habilidade("Slash", [CommandDirection.Up]);
        var shot = new Habilidade("Shot", [CommandDirection.Down]);
        var resolver = Criar(slash, shot);

        Assert.Same(slash, resolver.Resolve([CommandDirection.Up]));
        Assert.Same(shot, resolver.Resolve([CommandDirection.Down]));
    }

    [Fact]
    public void Descricao_e_opcional_e_cai_para_ToString_quando_ausente()
    {
        var a = new Habilidade("A", [CommandDirection.Up]);
        var b = new Habilidade("B", [CommandDirection.Up]);

        // Sem o seletor de descrição: não deve lançar NullReferenceException
        // por dentro, mesmo sem um jeito bonito de nomear os itens.
        var excecao = Record.Exception(() => new AbilityComboResolver<Habilidade>([a, b], h => h.Sequencia));

        Assert.IsType<InvalidOperationException>(excecao);
    }
}
