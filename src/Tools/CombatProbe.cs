using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Core;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica o ataque básico na árvore de nós real, sem teclado.
/// </summary>
/// <remarks>
/// Os testes de xUnit cobrem a <c>MeleeCombo</c> isolada. O que não alcançam é a
/// fiação: se o `.tres` da espada carregou com os três passos, se o alvo é
/// achado no grupo, se o dano chega à vida e se o mesmo golpe acerta uma vez só.
///
/// Fogo amigo NÃO é verificado aqui: exigiria um segundo personagem do mesmo
/// time com arma, e no ticket 08 só o jogador tem uma.
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/CombatProbe.tscn
/// </code>
/// </remarks>
public sealed partial class CombatProbe : Node
{
    /// <summary>Cena a inspecionar.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/arena/Arena.tscn";

    private readonly List<string> _falhas = [];
    private CharacterController? _jogador;
    private CharacterController? _alvo;
    private int _quadro;
    private int _acertos;
    private float _vidaAntes;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            GD.PrintErr($"[combate] não consegui carregar {ScenePath}");
            GetTree().Quit(1);
            return;
        }

        AddChild(packed.Instantiate());
        Procurar(this);

        if (_jogador?.Context?.Combat is null || _alvo?.Context?.Health is null)
        {
            GD.PrintErr("[combate] FALHA: jogador sem CombatComponent ou alvo sem vida.");
            GetTree().Quit(1);
            return;
        }

        _jogador.Context.Combat.HitLanded += _ => _acertos++;

        // Encosta o alvo na frente do jogador, dentro do alcance e do cone.
        var frente = -_jogador.GlobalTransform.Basis.Z;
        _alvo.GlobalPosition = _jogador.GlobalPosition + (frente.Normalized() * 1.5f);
        _vidaAntes = _alvo.Context.Health.Current;

        GD.Print($"[combate] jogador e alvo prontos; vida do alvo {_vidaAntes:0}");
    }

    public override void _PhysicsProcess(double delta)
    {
        var combate = _jogador?.Context?.Combat;
        var vida = _alvo?.Context?.Health;
        if (combate is null || vida is null)
            return;

        _quadro++;

        // O jogador encara o CURSOR, e em modo headless o cursor fica no canto
        // da tela — ele gira para la logo apos o pedido de golpe. Manter o alvo
        // a frente durante o golpe isola o que se quer verificar: alcance, cone
        // e um acerto por golpe, sem a deriva de orientacao mascarar tudo.
        if (combate.IsAttacking)
            Reposicionar();

        switch (_quadro)
        {
            case 1:
                Reposicionar();
                combate.RequestBasicAttack();
                Verificar(combate.ComboStep == 1, $"primeiro golpe deveria ser o passo 1, foi {combate.ComboStep}");
                break;

            case 30:
                Reposicionar();
                // A janela de acerto (0,18–0,32 s) já abriu e fechou.
                Verificar(_acertos == 1, $"o golpe acertou {_acertos}x, esperado exatamente 1");
                Verificar(vida.Current < _vidaAntes, "o alvo deveria ter perdido vida");

                // Encadeia: dentro da janela de combo, o passo avança.
                combate.RequestBasicAttack();
                Verificar(combate.ComboStep == 2, $"deveria encadear para 2, foi {combate.ComboStep}");
                break;

            case 60:
                Reposicionar();
                combate.RequestBasicAttack();
                Verificar(combate.ComboStep == 3, $"deveria encadear para 3, foi {combate.ComboStep}");
                break;

            case 150:
                Reposicionar();
                // Janela de combo expirada: recomeça do 1, sem penalidade.
                combate.RequestBasicAttack();
                Verificar(combate.ComboStep == 1,
                    $"depois de perder a janela deveria voltar ao passo 1, foi {combate.ComboStep}");
                break;

            case 180:
                Verificar(_acertos >= 3, $"os três golpes deveriam ter conectado; conectaram {_acertos}");
                Concluir();
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Recoloca o alvo a frente do jogador.
    /// </summary>
    /// <remarks>
    /// Cada golpe avanca o personagem, entao depois de tres ele passaria do
    /// alvo. Reposicionar isola o que esta sendo verificado: a mecanica do
    /// golpe, nao a deriva de posicao.
    /// </remarks>
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
        GD.PrintErr($"[combate] FALHA: {mensagem}");
    }

    private void Concluir()
    {
        if (_falhas.Count > 0)
        {
            GD.PrintErr($"[combate] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"[combate] todas as verificações passaram ({_acertos} acertos registrados)");
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
