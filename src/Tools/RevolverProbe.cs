using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.Stats;
using Contenda.Core;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica o ataque básico do revólver e um clique M1 curto na árvore real.
/// </summary>
/// <remarks>
/// Espelha a <c>CombatProbe</c> do ticket 08: os testes de xUnit cobrem
/// <c>RevolverState</c> e <c>HitscanMath</c> isolados; o que eles não alcançam
/// é a fiação — se o `.tres` do revólver carrega, se a mira resolve, se o
/// dano chega à vida e se munição/recarga/cadência funcionam com a árvore de
/// verdade. Também confirma que a camada de animação entra na pose de mira e
/// que a borda de um clique breve chega ao combate pelo PlayerInputController.
///
/// A gunslinger e o manequim vêm de <c>RevolverArena.tscn</c>, uma cena
/// dedicada e menor que a <c>Arena</c> principal — só o necessário para a
/// mira ter uma câmera e o revólver ter um alvo. O manequim é reposicionado
/// em cima do <c>AimPoint</c> a cada quadro: como o tiro é instantâneo (sem
/// janela para encadear reposicionamento como no combo), colocar o alvo
/// exatamente onde a mira aponta isola o que se quer testar — munição,
/// recarga e cadência — da geometria exata de onde o cursor projeta na tela.
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/RevolverProbe.tscn
/// </code>
/// </remarks>
public sealed partial class RevolverProbe : Node
{
    /// <summary>Cena a inspecionar.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/debug/RevolverArena.tscn";

    private readonly List<string> _falhas = [];
    private CharacterController? _jogador;
    private CharacterController? _alvo;
    private int _quadro;
    private int _acertos;
    private float _vidaAntes;
    private int _acertosAntesDoPrimeiroHold;
    private int _acertosAntesDoSegundoHold;
    private int _cadenciaBase;
    private int _tirosSeguradosSemBuff;
    private int _disparosBasicos;
    private int _disparosAntesDoCliqueFisico;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            GD.PrintErr($"[revolver] não consegui carregar {ScenePath}");
            GetTree().Quit(1);
            return;
        }

        AddChild(packed.Instantiate());
        Procurar(this);

        if (_jogador?.Context?.Combat is null || _jogador?.Context?.Targeting is null || _alvo?.Context?.Health is null)
        {
            GD.PrintErr("[revolver] FALHA: jogador sem CombatComponent/TargetingComponent ou alvo sem vida.");
            GetTree().Quit(1);
            return;
        }

        _jogador.Context.Combat.HitLanded += _ => _acertos++;
        _jogador.Context.Combat.AttackStarted += _ => _disparosBasicos++;
        _vidaAntes = _alvo.Context.Health.Current;

        GD.Print($"[revolver] jogador e alvo prontos; vida do alvo {_vidaAntes:0}");
    }

    public override void _PhysicsProcess(double delta)
    {
        var combate = _jogador?.Context?.Combat;
        var mira = _jogador?.Context?.Targeting;
        var vida = _alvo?.Context?.Health;
        if (combate is null || mira is null || vida is null)
            return;

        _quadro++;

        if (_quadro == 20)
        {
            Verificar(mira.HasAim, "o cursor deveria produzir um ponto de mira válido");
            var tree = _jogador?.Context?.Animator?.Tree;
            var poseDeMiraAtiva = tree is not null && tree.Get("parameters/Alert/active").AsBool();
            Verificar(poseDeMiraAtiva, "a Gunslinger deveria manter a pose de mira enquanto o cursor tem um alvo");
        }

        // O tiro é instantâneo: reposicionar o alvo em cima da mira, a cada
        // quadro, isola munição/recarga/cadência da geometria de projeção do
        // cursor -- ver o comentário da classe.
        // `!`: _alvo não é nulo aqui -- o guard acima já teria retornado, já
        // que `vida` vem de `_alvo?.Context?.Health`.
        if (mira.HasAim)
            _alvo!.GlobalPosition = mira.AimPoint;

        switch (_quadro)
        {
            // Seis tiros espaçados de 20 quadros (0,33 s a 60 Hz), acima dos
            // 0,30 s de AttackInterval do revolver.tres: cada um deve conectar,
            // provando tambor de 6 sem disparo perdido por cadência.
            case 5: DispararEVerificar(1); break;
            case 25: DispararEVerificar(2); break;
            case 45: DispararEVerificar(3); break;
            case 65: DispararEVerificar(4); break;
            case 85: DispararEVerificar(5); break;
            case 105:
                DispararEVerificar(6);
                _cadenciaBase = _acertos;
                break;

            case 106:
                // Um quadro depois do 6º tiro: o tambor acabou de esvaziar e a
                // recarga (1,6 s) mal começou. Este pedido não pode conectar.
                combate.RequestBasicAttack();
                Verificar(_acertos == _cadenciaBase,
                    $"não deveria atirar durante a recarga; acertos foram de {_cadenciaBase} para {_acertos}");
                break;

            case 210:
                // 105 (tambor esvaziou) + 96 quadros (1,6 s) + folga: a recarga
                // já devia ter enchido o tambor sozinha.
                combate.RequestBasicAttack();
                Verificar(_acertos == _cadenciaBase + 1,
                    $"a recarga automática deveria ter devolvido o tiro; acertos {_acertos}, esperado {_cadenciaBase + 1}");
                break;

            case 220:
                // Clique físico simulado: percorre PlayerInputController._Input
                // e o mesmo pedido de ataque usado pelo mouse em jogo. Os testes
                // anteriores chamavam RequestBasicAttack diretamente.
                combate.ResetForSpawn();
                _disparosAntesDoCliqueFisico = _disparosBasicos;
                SimularCliqueMouse1(pressionado: true);
                SimularCliqueMouse1(pressionado: false);
                break;

            case 222:
                Verificar(_disparosBasicos == _disparosAntesDoCliqueFisico + 1,
                    $"um clique M1 breve deveria iniciar um tiro; disparos {_disparosBasicos - _disparosAntesDoCliqueFisico}");
                break;

            case 260:
                _acertosAntesDoPrimeiroHold = _acertos;
                Godot.Input.ActionPress(InputActionNames.AttackBasic);
                break;

            case 320:
                // Segurado por 60 quadros (1 s) a 0,30 s de cadência: pelo
                // menos 2 tiros extras, com folga contra o limite exato de ~4.
                Godot.Input.ActionRelease(InputActionNames.AttackBasic);
                _tirosSeguradosSemBuff = _acertos - _acertosAntesDoPrimeiroHold;
                Verificar(_tirosSeguradosSemBuff >= 2,
                    $"segurar o gatilho deveria manter a cadência; só {_tirosSeguradosSemBuff} tiro(s) em 1 s");
                break;

            case 321:
                // A primeira leva de disparos segurados (quadros 260-320) deixa
                // o tambor com uma quantidade de cartuchos que depende só de
                // quando cada tiro coube na cadência -- nem cheio nem vazio de
                // forma garantida. ResetForSpawn devolve ao tambor cheio, sem
                // cooldown nem recarga pendente, na hora, para o segundo teste
                // de cadência (com o atributo dobrado) começar de um estado
                // conhecido em vez de depender da aritmética do primeiro.
                combate.ResetForSpawn();
                break;

            case 322:
                // Dobra AttackSpeed sem tocar no revolver.tres -- é o critério
                // de aceite mais importante do ticket 09: "prova a arquitetura
                // data-driven".
                // `!`: nenhum é nulo aqui -- _Ready já teria matado a árvore
                // se o jogador não tivesse Combat, e Character.tscn sempre traz
                // StatsComponent junto.
                _jogador!.Context!.Stats!.AddModifier(new StatModifier(
                    StatId.AttackSpeed, ModifierOp.PercentMult, 1f, "revolver_probe_cadencia"));
                break;

            case 325:
                _acertosAntesDoSegundoHold = _acertos;
                Godot.Input.ActionPress(InputActionNames.AttackBasic);
                break;

            case 385:
                // Mesma janela de 60 quadros do teste anterior, agora com
                // AttackSpeed dobrado e o tambor garantidamente cheio no
                // início: deveria disparar visivelmente mais vezes que a linha
                // de base do quadro 320, sem o revolver.tres ter mudado um
                // número sequer -- o critério de aceite central do ticket 09.
                Godot.Input.ActionRelease(InputActionNames.AttackBasic);
                var comBuff = _acertos - _acertosAntesDoSegundoHold;
                Verificar(comBuff > _tirosSeguradosSemBuff,
                    $"dobrar AttackSpeed deveria acelerar a cadência; base {_tirosSeguradosSemBuff}, com buff {comBuff}");
                break;

            case 400:
                Concluir();
                break;

            default:
                break;
        }
    }

    private void DispararEVerificar(int numeroDoTiro)
    {
        // `!`: só chamado depois do guard de _PhysicsProcess confirmar que
        // `combate` (o mesmo _jogador.Context.Combat) não é nulo.
        var combate = _jogador!.Context!.Combat!;
        combate.RequestBasicAttack();
        Verificar(_acertos == numeroDoTiro, $"tiro {numeroDoTiro} deveria conectar; acertos totais {_acertos}");
    }

    private void SimularCliqueMouse1(bool pressionado)
    {
        var viewport = _jogador?.GetViewport();
        if (viewport is null)
            return;

        var posicao = viewport.GetMousePosition();
        Godot.Input.ParseInputEvent(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = pressionado,
            Position = posicao,
            GlobalPosition = posicao,
        });
    }

    private void Verificar(bool condicao, string mensagem)
    {
        if (condicao)
            return;

        _falhas.Add(mensagem);
        GD.PrintErr($"[revolver] FALHA: {mensagem}");
    }

    private void Concluir()
    {
        if (_falhas.Count > 0)
        {
            GD.PrintErr($"[revolver] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"[revolver] todas as verificações passaram ({_acertos} acertos registrados)");
        GetTree().Quit();
    }

    private void Procurar(Node no)
    {
        foreach (var filho in no.GetChildren())
        {
            if (filho is CharacterController c)
            {
                if (c.Team == Team.Player && c.Context?.Combat is not null)
                    _jogador ??= c;
                else if (c.Team == Team.Enemy && c.Context?.Health is not null)
                    _alvo ??= c;
            }

            Procurar(filho);
        }
    }
}
