using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.Combat;
using Contenda.Components.Health;
using Contenda.Components.Stats;
using Contenda.Core;
using Contenda.Vfx;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica o sistema de golpes críticos na árvore de nós real, sem teclado.
/// </summary>
/// <remarks>
/// Os testes de xUnit cobrem <c>CritMath</c> isolado — o sorteio puro e a
/// aplicação do multiplicador. O que eles não alcançam é a fiação: se a
/// chance e o multiplicador realmente vêm do `.tres` do personagem (não de um
/// padrão de classe), se o congelamento cresce de verdade, se o número de
/// dano muda de tamanho/cor/contorno, e — o critério mais fácil de quebrar por
/// engano — se um golpe em área sorteia uma vez só, nunca por alvo. Ticket 18,
/// spec 16 §5.
///
/// A parte de área usa dois manequins e repete a tentativa <see cref="TentativasDeArea"/>
/// vezes com 50 % de chance: é a única verificação probabilística deste
/// projeto, e de propósito só falha para um lado — "os dois alvos da MESMA
/// tentativa discordaram" nunca acontece com o sorteio uma vez por golpe, seja
/// qual for o resultado da moeda. Um sorteio por alvo (o bug que isto existe
/// para pegar) discordaria em ~50 % das tentativas, e dez tentativas sem
/// nenhum desacordo já é forte demais para vir de sorte.
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/CriticalProbe.tscn
/// </code>
/// </remarks>
public sealed partial class CriticalProbe : Node
{
    /// <summary>Cena com o jogador e o primeiro manequim.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/arena/Arena.tscn";

    /// <summary>Cena do segundo manequim, instanciado à parte para o teste de área.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string DummyScenePath { get; set; } = "res://scenes/debug/TrainingDummy.tscn";

    private const int TentativasDeArea = 10;
    private const int QuadrosPorTentativa = 25;

    /// <summary>
    /// Folga antes do primeiro pedido de ataque de cada fase.
    /// </summary>
    /// <remarks>
    /// A primeira chamada de <see cref="TickCriticoOuNormal"/> acontece bem
    /// no início da árvore -- sem isto, pedir o golpe já no quadro 1 corre o
    /// risco de pegar o jogador antes do primeiro `MoveAndSlide` de verdade,
    /// quando `MovementComponent.IsGrounded` ainda não se acomodou. Golpe de
    /// solo pedido "no ar" por engano rotearia para o combo AÉREO (ticket
    /// 19, mais fraco) assim que `sword.tres` passou a ter um definido --
    /// mesma causa raiz já corrigida em <c>CombatProbe</c>/<c>ImpactProbe</c>.
    /// 10, não 5: a arena urbana (ticket 21) tem bem mais colisores estáticos
    /// para o servidor de física assentar no boot, e `IsGrounded` (1 quadro
    /// atrás de `IsOnFloor()` por design) levava mais tempo para acompanhar.
    /// </remarks>
    private const int QuadroDoPedido = 10;

    private enum Fase { Dados, Critico, Normal, Area }

    private readonly List<string> _falhas = [];
    private CharacterController? _jogador;
    private CharacterController? _alvo1;
    private CharacterController? _alvo2;
    private DamageNumberPool? _numeros;

    private Fase _fase = Fase.Dados;
    private int _quadroDaFase;
    private int _quadrosCongelado;

    private DamageInfo? _golpeAlvo1;
    private DamageInfo? _golpeAlvo2;

    private float _danoCriticoObservado;
    private float _danoNormalObservado;
    private int _quadrosCongeladoCritico;
    private int _quadrosCongeladoNormal;

    private int _tentativaDeArea;
    private int _criticosNaArea;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        if (packed is null)
        {
            GD.PrintErr($"[critico] não consegui carregar {ScenePath}");
            GetTree().Quit(1);
            return;
        }

        AddChild(packed.Instantiate());
        Procurar(this);

        var dummyPacked = GD.Load<PackedScene>(DummyScenePath);
        if (dummyPacked is null || _jogador?.Context?.Combat is null || _jogador.Context.Stats is null
            || _alvo1?.Context?.Health is null || _alvo1.Context.Stats is null)
        {
            GD.PrintErr("[critico] FALHA: jogador sem Combat/Stats ou primeiro manequim sem vida/stats.");
            GetTree().Quit(1);
            return;
        }

        _alvo2 = (CharacterController)dummyPacked.Instantiate();
        AddChild(_alvo2);

        _numeros = GetNodeOrNull<DamageNumberPool>("/root/DamageNumberPool");
        if (_numeros is null || _alvo2.Context?.Health is null)
        {
            GD.PrintErr("[critico] FALHA: pool de números ou segundo manequim sem vida.");
            GetTree().Quit(1);
            return;
        }

        // Longe do cone/alcance da espada até a fase de área precisar dele --
        // as fases anteriores testam um alvo só, e o segundo não deveria
        // conectar por engano.
        _alvo2.GlobalPosition = _jogador.GlobalPosition + new Vector3(50f, 0f, 50f);

        _alvo1.Context.Health.Damaged += golpe => _golpeAlvo1 = golpe;
        _alvo2.Context.Health.Damaged += golpe => _golpeAlvo2 = golpe;

        Reposicionar(_alvo1);

        GD.Print("[critico] jogador e manequins prontos");
    }

    public override void _PhysicsProcess(double delta)
    {
        var combate = _jogador?.Context?.Combat;
        var stats = _jogador?.Context?.Stats;
        if (combate is null || stats is null || _alvo1?.Context?.Health is null || _alvo2?.Context?.Health is null)
            return;

        _quadroDaFase++;

        switch (_fase)
        {
            case Fase.Dados:
                TickDados();
                break;

            case Fase.Critico:
                TickCriticoOuNormal(combate, stats, forcarCritico: true);
                break;

            case Fase.Normal:
                TickCriticoOuNormal(combate, stats, forcarCritico: false);
                break;

            case Fase.Area:
                TickArea(combate);
                break;

            default:
                break;
        }
    }

    private void TickDados()
    {
        // Carregados direto do disco, sem instanciar uma segunda árvore: a
        // diferença de frequência entre os dois personagens é DADO, não
        // comportamento de árvore -- comparar os `.tres` já prova a fiação.
        var espadachim = GD.Load<CharacterDefinition>("res://data/characters/swordsman.tres");
        var pistoleira = GD.Load<CharacterDefinition>("res://data/characters/gunslinger.tres");

        Verificar(espadachim?.Stats is not null && pistoleira?.Stats is not null,
            "os dois personagens deveriam ter StatsDefinition atribuída");

        if (espadachim?.Stats is not null && pistoleira?.Stats is not null)
        {
            Verificar(Mathf.IsEqualApprox(espadachim.Stats.CritChance, 0.10f, 0.001f),
                $"espadachim deveria criticar 10% (spec 16 §5), tem {espadachim.Stats.CritChance:0.000}");
            Verificar(Mathf.IsEqualApprox(pistoleira.Stats.CritChance, 0.05f, 0.001f),
                $"pistoleira deveria criticar 5% (spec 16 §5), tem {pistoleira.Stats.CritChance:0.000}");
            Verificar(Mathf.IsEqualApprox(espadachim.Stats.CritChance, pistoleira.Stats.CritChance * 2f, 0.001f),
                "o espadachim deveria criticar com o dobro da frequência da pistoleira");
        }

        // A fiação de verdade: o StatsComponent do jogador EM CENA carregou
        // esse mesmo valor do `.tres` via CharacterDefinition.Stats, não ficou
        // no padrão neutro da classe (0).
        Verificar(Mathf.IsEqualApprox(_jogador!.Context!.Stats!.Get(StatId.CritChance), 0.10f, 0.001f),
            $"CritChance do jogador em cena deveria ser 10%, é {_jogador.Context.Stats.Get(StatId.CritChance):0.000}");
        Verificar(Mathf.IsEqualApprox(_jogador.Context.Stats.Get(StatId.CritMultiplier), 2.0f, 0.001f),
            $"CritMultiplier do jogador em cena deveria ser 2.0, é {_jogador.Context.Stats.Get(StatId.CritMultiplier):0.00}");

        // Inimigos não criticam: o manequim não tem StatsDefinition nenhuma na
        // sua CharacterDefinition, e isso já basta -- sem `if` nenhum sobre
        // quem é o personagem.
        Verificar(_alvo1!.Context!.Stats!.Get(StatId.CritChance) == 0f,
            "o manequim (inimigo) não deveria ter chance de crítico nenhuma");

        AvancarFase(Fase.Critico);
    }

    /// <summary>
    /// Golpe único contra <see cref="_alvo1"/>, com a chance de crítico forçada
    /// para 100% ou 0% -- isola o que se quer medir (dano e congelamento) do
    /// sorteio de verdade, que a fase de área já cobre.
    /// </summary>
    private void TickCriticoOuNormal(CombatComponent combate, StatsComponent stats, bool forcarCritico)
    {
        if (combate.IsAttacking)
            Reposicionar(_alvo1!);

        if (_quadroDaFase == QuadroDoPedido)
        {
            Reposicionar(_alvo1!);
            _golpeAlvo1 = null;
            _quadrosCongelado = 0;
            stats.SetBase(StatId.CritChance, forcarCritico ? 1f : 0f);
            combate.RequestBasicAttack();
        }

        if (_alvo1!.Context!.Health!.TimeScale <= 0f)
            _quadrosCongelado++;

        if (_quadroDaFase < QuadroDoPedido + QuadrosPorTentativa)
            return;

        var rotulo = forcarCritico ? "crítico" : "normal";
        Verificar(_golpeAlvo1 is { } golpe && golpe.IsCritical == forcarCritico,
            $"o golpe forçado deveria ter saído {rotulo}, mas {(_golpeAlvo1 is null ? "não conectou" : $"IsCritical={_golpeAlvo1.Value.IsCritical}")}");

        var dano = _golpeAlvo1?.Amount ?? 0f;
        Verificar(dano > 0f, $"o golpe {rotulo} deveria ter causado dano");

        var escalaEsperada = forcarCritico;
        Verificar(escalaEsperada ? _numeros!.LastScale > 1f : Mathf.IsEqualApprox(_numeros!.LastScale, 1f, 0.01f),
            $"escala do número {rotulo} incorreta: {_numeros!.LastScale:0.00}");
        Verificar(escalaEsperada ? _numeros.LastOutlineSize > 0 : _numeros.LastOutlineSize == 0,
            $"contorno (brilho) do número {rotulo} incorreto: {_numeros.LastOutlineSize}");

        combate.ResetForSpawn();
        _alvo1.Context.Health.Heal(999999f);

        if (forcarCritico)
        {
            _danoCriticoObservado = dano;
            _quadrosCongeladoCritico = _quadrosCongelado;
            AvancarFase(Fase.Normal);
            return;
        }

        _danoNormalObservado = dano;
        _quadrosCongeladoNormal = _quadrosCongelado;

        Verificar(Mathf.IsEqualApprox(_danoCriticoObservado, _danoNormalObservado * 2.0f, _danoNormalObservado * 0.05f),
            "o crítico deveria valer 2.0x o normal (CritMultiplier do espadachim), sem hardcode nenhum "
            + $"do lado do probe; crítico {_danoCriticoObservado:0.0}, normal {_danoNormalObservado:0.0}");
        Verificar(_quadrosCongeladoCritico > _quadrosCongeladoNormal,
            $"o congelamento do crítico deveria durar mais quadros que o normal; crítico "
            + $"{_quadrosCongeladoCritico}, normal {_quadrosCongeladoNormal}");

        // 50%: qualquer resultado é válido por tentativa -- só a CONSISTÊNCIA
        // entre os dois alvos da mesma tentativa é verificada. Ver o
        // comentário da classe.
        stats.SetBase(StatId.CritChance, 0.5f);
        PosicionarParaArea();

        AvancarFase(Fase.Area);
    }

    private void TickArea(CombatComponent combate)
    {
        if (combate.IsAttacking)
            PosicionarParaArea();

        if (_quadroDaFase == 1)
        {
            _golpeAlvo1 = null;
            _golpeAlvo2 = null;
            PosicionarParaArea();
            combate.RequestBasicAttack();
        }

        if (_quadroDaFase < QuadrosPorTentativa)
            return;

        Verificar(_golpeAlvo1 is not null && _golpeAlvo2 is not null,
            $"tentativa {_tentativaDeArea + 1}: os dois manequins deveriam ter sido acertados na mesma janela");

        if (_golpeAlvo1 is { } g1 && _golpeAlvo2 is { } g2)
        {
            Verificar(g1.IsCritical == g2.IsCritical,
                $"tentativa {_tentativaDeArea + 1}: um golpe em área saiu crítico só num dos alvos "
                + $"(alvo1={g1.IsCritical}, alvo2={g2.IsCritical}) -- deveria ser tudo ou nada");

            if (g1.IsCritical)
                _criticosNaArea++;
        }

        combate.ResetForSpawn();
        _alvo1!.Context!.Health!.Heal(999999f);
        _alvo2!.Context!.Health!.Heal(999999f);

        _tentativaDeArea++;
        if (_tentativaDeArea >= TentativasDeArea)
        {
            Concluir();
            return;
        }

        _quadroDaFase = 0;
    }

    /// <summary>Alinha os dois manequins lado a lado, dentro do cone e alcance da espada.</summary>
    private void PosicionarParaArea()
    {
        Reposicionar(_alvo1!);

        var frente = -_jogador!.GlobalTransform.Basis.Z;
        var lado = frente.Cross(Vector3.Up).Normalized();
        _alvo2!.GlobalPosition = _alvo1!.GlobalPosition + (lado * 0.8f);
    }

    private void Reposicionar(CharacterController alvo)
    {
        var frente = -_jogador!.GlobalTransform.Basis.Z;
        var plano = new Vector3(frente.X, 0f, frente.Z).Normalized();
        alvo.GlobalPosition = _jogador.GlobalPosition + (plano * 1.5f);
    }

    private void Verificar(bool condicao, string mensagem)
    {
        if (condicao)
            return;

        _falhas.Add(mensagem);
        GD.PrintErr($"[critico] FALHA: {mensagem}");
    }

    private void AvancarFase(Fase proxima)
    {
        _fase = proxima;
        _quadroDaFase = 0;
    }

    private void Concluir()
    {
        if (_falhas.Count > 0)
        {
            GD.PrintErr($"[critico] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"[critico] todas as verificações passaram ({_criticosNaArea}/{TentativasDeArea} "
                 + "tentativas de área saíram crítico, nenhuma dividida entre os dois alvos)");
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
                    _alvo1 ??= c;
            }

            Procurar(filho);
        }
    }
}
