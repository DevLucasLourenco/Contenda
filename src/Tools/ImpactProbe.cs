using System.Collections.Generic;
using Contenda.Camera;
using Contenda.Characters.Base;
using Contenda.Components.Health;
using Contenda.Core;
using Contenda.Vfx;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica o peso de impacto na árvore real, sem teclado — ticket 11.
/// </summary>
/// <remarks>
/// Os testes de xUnit cobrem <c>HitstopState</c>, <c>KnockbackState</c> e
/// <c>FloatingDamageMath</c> isolados. O que eles não alcançam é a fiação: um
/// golpe REAL do combo (não uma chamada direta a <c>ApplyDamage</c>) tem que
/// disparar as cinco coisas ao mesmo tempo — congelar os dois envolvidos,
/// empurrar o alvo, piscar a malha, mostrar um número e sacudir a câmera —
/// exatamente como vai acontecer no jogo de verdade.
///
/// As verificações de hitstop/flash são relativas ao QUADRO EM QUE O GOLPE
/// CONECTOU (<see cref="_quadroDoAcerto"/>), não a um número de quadro fixo:
/// a janela de acerto do combo dura vários quadros, e o golpe pode conectar em
/// qualquer um deles. Hitstop dura só 0,04-0,09 s (2-5 quadros) — checar num
/// quadro fixo escolhido sem saber exatamente quando o golpe conectou
/// facilmente cai FORA da janela do efeito, e reporta uma falha que não é
/// real.
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/ImpactProbe.tscn
/// </code>
///
/// `!` em `_jogador`/`_alvo`/`_camera`/`_numeros`/`_flashDoAlvo` e seus
/// membros, por todo o arquivo a partir do <c>_Ready</c>: o guard ali já
/// mataria a árvore (<c>Quit(1)</c>) se qualquer um não tivesse resolvido, e
/// só depois disso alguma outra callback deste probe roda.
/// </remarks>
public sealed partial class ImpactProbe : Node
{
    /// <summary>Cena a inspecionar.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/arena/Arena.tscn";

    private readonly List<string> _falhas = [];
    private CharacterController? _jogador;
    private CharacterController? _alvo;
    private DamageFlashComponent? _flashDoAlvo;
    private CameraRig? _camera;
    private DamageNumberPool? _numeros;
    private int _quadro;
    private int _acertos;
    private int _quadroDoAcerto = -1;
    private Vector3 _posicaoDoAlvoAntesDoGolpe;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            GD.PrintErr($"[impacto] não consegui carregar {ScenePath}");
            GetTree().Quit(1);
            return;
        }

        AddChild(packed.Instantiate());
        Procurar(this);
        _flashDoAlvo = _alvo?.GetNodeOrNull<DamageFlashComponent>("DamageFlashComponent");
        _numeros = GetNodeOrNull<DamageNumberPool>("/root/DamageNumberPool");

        if (_jogador?.Context?.Combat is null || _alvo?.Context?.Health is null
            || _flashDoAlvo is null || _camera is null || _numeros is null)
        {
            GD.PrintErr("[impacto] FALHA: não encontrei jogador, alvo, flash, câmera ou pool de números.");
            GetTree().Quit(1);
            return;
        }

        _jogador.Context.Combat.HitLanded += AoAcertar;

        GD.Print("[impacto] jogador, alvo, câmera e pool prontos");
    }

    public override void _PhysicsProcess(double delta)
    {
        var combate = _jogador?.Context?.Combat;
        if (combate is null)
            return;

        _quadro++;

        if (combate.IsAttacking)
            Reposicionar();

        if (_quadro == 1)
        {
            Reposicionar();
            _posicaoDoAlvoAntesDoGolpe = _alvo!.GlobalPosition;
            combate.RequestBasicAttack();
        }

        if (_quadroDoAcerto >= 0)
            VerificarAposOAcerto(_quadro - _quadroDoAcerto);

        // Sem golpe algum depois de 60 quadros (1 s): a janela de acerto do
        // slash 1 (0,18-0,32 s) já teria passado de sobra. Falha explícita em
        // vez de rodar para sempre.
        if (_quadroDoAcerto < 0 && _quadro > 60)
        {
            Verificar(false, "o golpe deveria ter conectado dentro de 1 s e não conectou");
            Concluir();
        }
    }

    private void VerificarAposOAcerto(int desdeOAcerto)
    {
        switch (desdeOAcerto)
        {
            case 2:
                // Duas folgas de quadro: ApplyHitstop é direto (mesmo quadro
                // do acerto), mas o flash passa pela fila de dano do alvo,
                // resolvida no PRÓPRIO _PhysicsProcess do alvo -- pode ficar
                // um quadro atrás do atacante dependendo da ordem entre
                // irmãos na árvore.
                Verificar(_jogador!.Context!.Health!.TimeScale <= 0f,
                    "o atacante deveria estar congelado logo após o próprio golpe conectar");
                Verificar(_alvo!.Context!.Health!.TimeScale <= 0f,
                    "o alvo deveria estar congelado logo após apanhar");
                Verificar(_flashDoAlvo!.IsFlashing, "a malha do alvo deveria estar piscando");
                Verificar(_numeros!.ActiveCount >= 1, "deveria haver ao menos um número de dano visível");
                Verificar(_camera!.ShakeOffset.LengthSquared() > 0f, "a câmera deveria estar tremendo");
                break;

            // 0,09 s (o maior hitstop possível, do finalizador) são ~6
            // quadros; 15 quadros (0,25 s) é folga generosa.
            case 15:
                Verificar(_jogador!.Context!.Health!.TimeScale >= 1f, "o hitstop do atacante deveria ter acabado");
                Verificar(_alvo!.Context!.Health!.TimeScale >= 1f, "o hitstop do alvo deveria ter acabado");
                break;

            // O flash (0,08 s) também já devia ter acabado bem antes disto.
            case 20:
                Verificar(!_flashDoAlvo!.IsFlashing, "o flash deveria ter apagado");
                break;

            // Knockback: duração fixa de 0,25 s. Por volta daqui o alvo já
            // deveria ter se afastado da posição pré-golpe.
            case 30:
                var deslocamento = _alvo!.GlobalPosition - _posicaoDoAlvoAntesDoGolpe;
                var deslocamentoNoPlano = new Vector3(deslocamento.X, 0f, deslocamento.Z);
                Verificar(deslocamentoNoPlano.Length() > 0.05f,
                    $"o alvo deveria ter sido empurrado; deslocou só {deslocamentoNoPlano.Length():0.000} m");
                break;

            // Número de dano: vida útil de 0,6 s (36 quadros). Folga de mais
            // 30 quadros por cima.
            case 66:
                Verificar(_numeros!.ActiveCount == 0,
                    $"o número de dano deveria ter sumido; ainda há {_numeros.ActiveCount} visível(is)");
                Concluir();
                break;

            default:
                break;
        }
    }

    private void AoAcertar(Node3D alvo)
    {
        _acertos++;

        if (_quadroDoAcerto < 0)
            _quadroDoAcerto = _quadro;
    }

    private void Reposicionar()
    {
        var frente = -_jogador!.GlobalTransform.Basis.Z;
        var plano = new Vector3(frente.X, 0f, frente.Z).Normalized();
        _alvo!.GlobalPosition = _jogador.GlobalPosition + (plano * 1.5f);
    }

    private void Verificar(bool condicao, string mensagem)
    {
        if (condicao)
            return;

        _falhas.Add(mensagem);
        GD.PrintErr($"[impacto] FALHA: {mensagem}");
    }

    private void Concluir()
    {
        if (_falhas.Count > 0)
        {
            GD.PrintErr($"[impacto] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[impacto] todas as verificações passaram");
        GetTree().Quit();
    }

    private void Procurar(Node no)
    {
        if (no is CameraRig rig)
            _camera = rig;

        if (no is CharacterController c)
        {
            if (c.Team == Team.Player && c.Context?.Combat is not null)
                _jogador ??= c;
            else if (c.Team == Team.Enemy && c.Context?.Health is not null)
                _alvo ??= c;
        }

        foreach (var filho in no.GetChildren())
            Procurar(filho);
    }
}
