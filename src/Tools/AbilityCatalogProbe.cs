using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.Abilities;
using Contenda.Core;
using Contenda.Input;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica as oito habilidades do catálogo do MVP contra o manequim, na árvore real.
/// </summary>
/// <remarks>
/// Ticket 15. As seis novas <see cref="IAbilityBehavior"/> (tudo exceto
/// <c>DashAttackBehavior</c>, já coberto pelo <c>AbilityProbe</c> do ticket 14)
/// dependem de <c>GlobalTransform</c>, <c>GetTree().GetNodesInGroup</c> e do
/// <see cref="ProjectilePool"/> — nada disso existe fora do processo do Godot,
/// então xUnit não alcança. Verifica também que o pool de projéteis não cria
/// nem destrói nó nenhum ao disparar mais vezes que o próprio tamanho.
///
/// Os dois personagens ficam bem separados (30 m) para o dash/lançamento de
/// um nunca alcançar o manequim posicionado para o outro; o mesmo manequim é
/// reaproveitado entre os testes, reposicionado à frente de quem está sendo
/// testado -- mesma técnica do <c>AbilityProbe</c> quanto ao <c>FaceMode.Aim</c>
/// do Swordsman/Gunslinger não apontar para -Z fixo.
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/AbilityCatalogProbe.tscn
/// </code>
/// </remarks>
public sealed partial class AbilityCatalogProbe : Node
{
    /// <summary>Cena a inspecionar.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/debug/AbilityCatalogArena.tscn";

    /// <summary>Quadros de folga entre disparar a sequência e cobrar o resultado.</summary>
    private const int QuadrosPorTeste = 300;

    /// <summary>Quadros de assentamento do giro (FaceMode.Aim) antes do primeiro teste.</summary>
    private const int QuadrosDeProntidao = 20;

    private readonly List<string> _falhas = [];
    private readonly List<(AbilityDefinition Habilidade, CharacterController Personagem, CommandDirection[] Sequencia)> _testes = [];

    private CharacterController? _espadachim;
    private CharacterController? _pistoleira;
    private CharacterController? _alvo;
    private ProjectilePool? _projeteis;

    private int _quadro;
    private int _indice = -1;
    private int _quadroDoTeste;
    private float _vidaAntesDoTeste;
    private bool _sequenciaEnviada;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            GD.PrintErr($"[catalogo] não consegui carregar {ScenePath}");
            GetTree().Quit(1);
            return;
        }

        AddChild(packed.Instantiate());
        Procurar(this);
        _projeteis = GetNodeOrNull<ProjectilePool>("/root/ProjectilePool");

        if (_espadachim?.Context?.Abilities is null || _pistoleira?.Context?.Abilities is null
            || _alvo?.Context?.Health is null || _projeteis is null)
        {
            GD.PrintErr("[catalogo] FALHA: não encontrei os dois personagens, o manequim ou o ProjectilePool.");
            GetTree().Quit(1);
            return;
        }

        MontarTestes();
        GD.Print($"[catalogo] {_testes.Count} habilidades para testar; pool de projéteis com {_projeteis.GetChildCount()} slots");
    }

    /// <remarks>
    /// As sequências vêm diretamente de <c>AbilityDefinition.Sequence</c>, não
    /// hardcoded aqui — se o `.tres` mudar a sequência de uma habilidade, o
    /// teste segue funcionando sem precisar de edição. Não é isto que valida
    /// "nenhuma habilidade referenciada por nome no código de produção": essa
    /// garantia vem da própria arquitetura (<c>AbilityBehaviorRegistry</c>
    /// despacha por <c>Kind</c>, nunca por <c>Id</c>), e é conferida por
    /// leitura no code review, não por este probe.
    /// </remarks>
    private void MontarTestes()
    {
        foreach (var habilidade in _espadachim!.Context!.Abilities!.Abilities)
            _testes.Add((habilidade, _espadachim, ToArray(habilidade.Sequence)));

        foreach (var habilidade in _pistoleira!.Context!.Abilities!.Abilities)
            _testes.Add((habilidade, _pistoleira, ToArray(habilidade.Sequence)));
    }

    private static CommandDirection[] ToArray(Godot.Collections.Array<CommandDirection> sequencia)
    {
        var resultado = new CommandDirection[sequencia.Count];
        for (var i = 0; i < sequencia.Count; i++)
            resultado[i] = sequencia[i];

        return resultado;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_testes.Count == 0)
            return;

        _quadro++;

        if (_indice < 0)
        {
            if (_quadro >= QuadrosDeProntidao)
                AvancarTeste();

            return;
        }

        _quadroDoTeste++;

        var (habilidade, personagem, sequencia) = _testes[_indice];
        var nome = habilidade.DisplayName;

        if (!_sequenciaEnviada)
        {
            _sequenciaEnviada = true;
            RealocarAlvoNaFrenteDe(personagem, habilidade);
            _vidaAntesDoTeste = _alvo!.Context!.Health!.Current;

            var habilidades = personagem.Context!.Abilities!;
            foreach (var direcao in sequencia)
                habilidades.PushToken(direcao);

            habilidades.RequestConfirm();

            Verificar(habilidades.Executing is not null,
                $"'{nome}': a sequência {string.Join(" ", sequencia)} deveria ter executado a habilidade");
        }

        if (_quadroDoTeste >= QuadrosPorTeste)
        {
            Verificar(_alvo!.Context!.Health!.Current < _vidaAntesDoTeste,
                $"'{nome}': deveria ter causado dano ao manequim, e não causou");

            if (_indice == _testes.Count - 1)
            {
                VerificarPoolDeProjeteis();
                Concluir();
                return;
            }

            AvancarTeste();
        }
    }

    /// <remarks>
    /// Dispara direto pelo <see cref="GameEvents"/>, sem passar por nenhuma
    /// habilidade: isola o pool do resto do sistema de execução, e permite
    /// disparar mais vezes que o tamanho do pool sem esperar a recarga de 6 s
    /// do Explosive Shot oito vezes seguidas.
    /// </remarks>
    private void VerificarPoolDeProjeteis()
    {
        var slots = _projeteis!.GetChildCount();

        for (var i = 0; i < slots + 3; i++)
        {
            ServiceLocator.Events.RaiseProjectileFire(new ProjectileFireEvent(
                Origin: new Vector3(0f, 0f, 100f + i),
                Direction: Vector3.Forward,
                Speed: 1f,
                LifeTime: 10f,
                Damage: 0f,
                ExplosionRadius: 0.1f,
                MaxTargets: 0,
                Knockback: 0f,
                SourceId: 0UL,
                SourceTag: "catalog_probe",
                ShooterTeam: Team.Neutral,
                TargetGroup: new StringName("damageable"),
                IsCritical: false));
        }

        Verificar(_projeteis.GetChildCount() == slots,
            $"disparar mais vezes que o pool ({slots} slots) não deveria criar nó nenhum; agora são {_projeteis.GetChildCount()}");
        Verificar(_projeteis.ActiveCount <= slots,
            $"o round-robin nunca deveria manter mais projéteis ativos que o pool; estão {_projeteis.ActiveCount}");
    }

    /// <remarks>
    /// 2 m de distância para a maioria: é a menor margem confortável dentro do
    /// alcance de TODAS as oito habilidades (a mais curta, Rising Slash, tem
    /// 2,5 m) — perto o bastante para nenhuma passar raspando por causa de
    /// arredondamento de ponto flutuante bem na borda do alcance.
    ///
    /// Explosive Shot é a exceção: a 2 m ela cairia dentro do próprio
    /// <c>Radius</c> (a área de explosão) e detonaria no primeiro quadro, sem
    /// o projétil ter voado de verdade. Ali o alvo fica um pouco além do raio
    /// de explosão, forçando alguns quadros de voo antes do impacto.
    /// </remarks>
    private void RealocarAlvoNaFrenteDe(CharacterController personagem, AbilityDefinition habilidade)
    {
        var distancia = habilidade.Kind == AbilityEffectKind.Projectile ? habilidade.Radius + 2f : 2f;
        var frenteReal = -personagem.GlobalTransform.Basis.Z;
        _alvo!.GlobalPosition = personagem.GlobalPosition + (frenteReal.Normalized() * distancia);
    }

    private void AvancarTeste()
    {
        _indice++;
        _quadroDoTeste = 0;
        _sequenciaEnviada = false;
    }

    private void Verificar(bool condicao, string mensagem)
    {
        if (condicao)
            return;

        _falhas.Add(mensagem);
        GD.PrintErr($"[catalogo] FALHA: {mensagem}");
    }

    private void Concluir()
    {
        if (_falhas.Count > 0)
        {
            GD.PrintErr($"[catalogo] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"[catalogo] todas as verificações passaram ({_testes.Count} habilidades)");
        GetTree().Quit();
    }

    private void Procurar(Node no)
    {
        if (no is CharacterController c)
        {
            switch (c.Name.ToString())
            {
                case "Swordsman": _espadachim = c; break;
                case "Gunslinger": _pistoleira = c; break;
                case "Manequim": _alvo = c; break;
            }
        }

        foreach (var filho in no.GetChildren())
            Procurar(filho);
    }
}
