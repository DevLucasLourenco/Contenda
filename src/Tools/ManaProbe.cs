using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.Stats;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica a fiação da mana na árvore de nós real, sem teclado.
/// </summary>
/// <remarks>
/// Os testes de xUnit cobrem a <c>ManaState</c> isolada — inclusive a
/// precisão da pausa de regeneração, com controle exato do relógio. O que eles
/// não alcançam é a fiação: se o <c>swordsman.tres</c> carrega os valores de
/// mana, se o <c>ManaComponent.Tick</c> é chamado sozinho pelo
/// <c>CharacterController</c> a cada quadro, e se um modificador de
/// <c>MaxMana</c> no <c>StatBlock</c> chega até o estado sem ninguém ligar os
/// dois à mão. Por isso as verificações aqui são folgadas no tempo — a
/// precisão já está garantida em xUnit.
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/ManaProbe.tscn
/// </code>
/// </remarks>
public sealed partial class ManaProbe : Node
{
    /// <summary>Cena a inspecionar. O Swordsman sozinho já basta — mana não precisa de arena.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/characters/Character.tscn";

    private readonly List<string> _falhas = [];
    private CharacterController? _jogador;
    private int _quadro;
    private int _zerou;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            GD.PrintErr($"[mana] não consegui carregar {ScenePath}");
            GetTree().Quit(1);
            return;
        }

        var instancia = packed.Instantiate();
        AddChild(instancia);
        _jogador = instancia as CharacterController;

        if (_jogador?.Context?.Mana is null || _jogador.Context.Stats is null)
        {
            GD.PrintErr("[mana] FALHA: jogador sem ManaComponent ou StatsComponent.");
            GetTree().Quit(1);
            return;
        }

        _jogador.Context.Mana.Depleted += () => _zerou++;

        GD.Print($"[mana] jogador pronto; mana inicial {_jogador.Context.Mana.Current:0}/{_jogador.Context.Mana.Max:0}");
    }

    public override void _PhysicsProcess(double delta)
    {
        var mana = _jogador?.Context?.Mana;
        var stats = _jogador?.Context?.Stats;
        if (mana is null || stats is null)
            return;

        _quadro++;

        switch (_quadro)
        {
            case 1:
                Verificar(Mathf.IsEqualApprox(mana.Max, 100f, 0.5f),
                    $"MaxMana deveria vir do swordsman.tres (100), veio {mana.Max}");
                Verificar(Mathf.IsEqualApprox(mana.Current, mana.Max, 0.5f), "deveria nascer com a mana cheia");
                break;

            case 2:
                Verificar(mana.TryConsume(40f), "deveria conseguir gastar 40 de 100");
                Verificar(Mathf.IsEqualApprox(mana.Current, 60f, 0.5f), $"esperava ~60 depois do gasto, tinha {mana.Current}");
                break;

            case 3:
                // Consumo atômico: sem saldo, NADA muda -- nem um resíduo parcial.
                Verificar(!mana.TryConsume(1000f), "não deveria conseguir gastar sem saldo");
                Verificar(Mathf.IsEqualApprox(mana.Current, 60f, 0.5f), "o gasto que falhou não pode ter alterado nada");
                break;

            case 90:
                // ~1,45 s depois do gasto do quadro 2: bem além da pausa de 1 s
                // do mana_swordsman.tres. A quantidade exata é assunto do xUnit;
                // aqui só importa que ALGUMA regeneração aconteceu -- prova que
                // o CharacterController está chamando Tick sozinho, quadro a
                // quadro, sem ninguém pedir.
                Verificar(mana.Current > 61f, $"deveria ter regenerado depois da pausa; tinha {mana.Current}");
                break;

            case 91:
                var consumido = mana.Drain(9999f); // pede muito mais do que existe
                Verificar(consumido > 0f, "Drain deveria ter consumido o que sobrava");
                Verificar(Mathf.IsEqualApprox(mana.Current, 0f, 0.01f), $"Drain deveria zerar; sobrou {mana.Current}");
                break;

            case 92:
                Verificar(_zerou >= 1, "Depleted deveria ter disparado ao zerar via Drain");
                break;

            case 93:
                // Um modificador de MaxMana (o que uma transformação faria no
                // M4) tem que chegar ao ManaState sozinho, via StatChanged --
                // ninguém liga os dois manualmente aqui.
                stats.AddModifier(new StatModifier(StatId.MaxMana, ModifierOp.PercentMult, 1f, "mana_probe"));
                break;

            case 94:
                Verificar(Mathf.IsEqualApprox(mana.Max, 200f, 0.5f),
                    $"dobrar o modificador de MaxMana deveria refletir no ManaState; virou {mana.Max}");
                break;

            case 95:
                stats.RemoveBySource("mana_probe");
                break;

            case 96:
                Verificar(Mathf.IsEqualApprox(mana.Max, 100f, 0.5f),
                    $"remover o modificador deveria devolver MaxMana a 100; ficou {mana.Max}");
                break;

            case 97:
                // `!`: nenhum é nulo aqui -- o guard no _Ready já teria matado
                // a árvore se _jogador não tivesse Context/Mana.
                _jogador!.Context!.Mana!.ResetForSpawn();
                break;

            case 98:
                Verificar(Mathf.IsEqualApprox(mana.Current, mana.Max, 0.01f),
                    $"ResetForSpawn deveria devolver a mana cheia; ficou {mana.Current}/{mana.Max}");
                break;

            case 100:
                Concluir();
                break;

            default:
                break;
        }
    }

    private void Verificar(bool condicao, string mensagem)
    {
        if (condicao)
            return;

        _falhas.Add(mensagem);
        GD.PrintErr($"[mana] FALHA: {mensagem}");
    }

    private void Concluir()
    {
        if (_falhas.Count > 0)
        {
            GD.PrintErr($"[mana] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[mana] todas as verificações passaram");
        GetTree().Quit();
    }
}
