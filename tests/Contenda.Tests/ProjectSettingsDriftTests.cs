using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Contenda.Core;
using Xunit;

namespace Contenda.Tests;

/// <summary>
/// Garante que <c>project.godot</c> e as constantes em C# não divirjam.
/// </summary>
/// <remarks>
/// Esta é a classe de bug que o projeto mais convida: alguém renomeia uma camada
/// no editor, ou acrescenta uma ação de input, e o espelho em C# fica para trás.
/// Nada quebra na compilação — quebra em runtime, numa máscara de colisão errada
/// que só aparece quando um golpe atravessa o inimigo.
/// </remarks>
public sealed class ProjectSettingsDriftTests
{
    private static readonly string ProjectGodot = ReadProjectGodot();

    [Fact]
    public void CamadasDeFisica_batem_com_PhysicsLayers_na_mesma_ordem()
    {
        var noArquivo = new List<string>();
        for (int bit = 1; bit <= PhysicsLayers.OrderedNames.Length; bit++)
        {
            var m = Regex.Match(
                ProjectGodot,
                $@"^3d_physics/layer_{bit}=""(?<nome>[^""]*)""",
                RegexOptions.Multiline);

            Assert.True(m.Success, $"project.godot não declara 3d_physics/layer_{bit}.");
            noArquivo.Add(m.Groups["nome"].Value);
        }

        Assert.Equal(PhysicsLayers.OrderedNames, noArquivo.ToArray());
    }

    [Fact]
    public void AcoesDeInput_batem_com_InputActionNames()
    {
        var secao = SecaoInput();

        var noArquivo = Regex.Matches(secao, @"^(?<acao>[a-z_][a-z0-9_]*)=\{", RegexOptions.Multiline)
            .Select(m => m.Groups["acao"].Value)
            .ToHashSet();

        var emCodigo = InputActionNames.All.ToHashSet();

        var faltandoNoArquivo = emCodigo.Except(noArquivo).OrderBy(x => x).ToArray();
        var faltandoNoCodigo = noArquivo.Except(emCodigo).OrderBy(x => x).ToArray();

        Assert.True(
            faltandoNoArquivo.Length == 0,
            $"Declaradas em C# mas ausentes do project.godot: {string.Join(", ", faltandoNoArquivo)}");
        Assert.True(
            faltandoNoCodigo.Length == 0,
            $"Presentes no project.godot mas ausentes de InputActionNames: {string.Join(", ", faltandoNoCodigo)}");
    }

    [Fact]
    public void TodaAcaoDeInput_tem_deadzone_declarada()
    {
        var secao = SecaoInput();
        var acoes = Regex.Matches(secao, @"^(?<acao>[a-z_][a-z0-9_]*)=\{(?<corpo>.*?)^\}",
                RegexOptions.Multiline | RegexOptions.Singleline);

        Assert.NotEmpty(acoes);
        foreach (Match acao in acoes)
        {
            Assert.True(
                acao.Groups["corpo"].Value.Contains("\"deadzone\""),
                $"A ação '{acao.Groups["acao"].Value}' não declara deadzone.");
        }
    }

    [Fact]
    public void BitsDeCamada_sao_potencias_de_dois_distintas_e_batem_com_os_nomes()
    {
        // Descoberto por reflexão, de propósito. Listar as constantes à mão aqui
        // criaria uma TERCEIRA cópia da lista de camadas — e este arquivo existe
        // justamente para impedir que cópias divirjam.
        var constantes = typeof(PhysicsLayers)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(uint))
            .ToDictionary(f => f.Name, f => (uint)f.GetRawConstantValue()!);

        Assert.Equal(PhysicsLayers.OrderedNames, constantes.Keys.ToArray());
        Assert.Equal(constantes.Count, constantes.Values.Distinct().Count());

        foreach (var (nome, bit) in constantes)
        {
            Assert.True(bit != 0u, $"A camada {nome} tem valor zero.");
            Assert.True((bit & (bit - 1)) == 0u, $"A camada {nome} não é potência de dois.");
        }
    }

    [Fact]
    public void MascaraDeGolpeDoJogador_nao_alcanca_o_proprio_time()
    {
        // Fogo amigo é desligado por camada E por código. Aqui verificamos a
        // camada: um golpe do jogador não pode enxergar a hurtbox do jogador.
        Assert.Equal(0u, PhysicsMasks.PlayerHitscanTargets & PhysicsLayers.PlayerHurtbox);
        Assert.Equal(0u, PhysicsMasks.EnemyHitscanTargets & PhysicsLayers.EnemyHurtbox);
    }

    private static string SecaoInput()
    {
        var m = Regex.Match(ProjectGodot, @"^\[input\]\r?\n(?<corpo>.*?)(^\[|\z)",
            RegexOptions.Multiline | RegexOptions.Singleline);
        Assert.True(m.Success, "project.godot não tem seção [input].");
        return m.Groups["corpo"].Value;
    }

    private static string ReadProjectGodot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException(
                "Não encontrei project.godot subindo a partir de " + AppContext.BaseDirectory);

        return File.ReadAllText(Path.Combine(dir.FullName, "project.godot"));
    }
}
