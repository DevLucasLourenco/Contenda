using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.AI;
using Contenda.Core;
using Godot;

namespace Contenda.Tools;

/// <summary>
/// Verifica a navegação de MUITOS inimigos na árvore de nós real: se uma
/// horda que converge chega espalhada (não empilhada), se um inimigo distante
/// pensa menos vezes por segundo, se um sem caminho válido não trava, e se a
/// escalada por ligação de navegação acontece de verdade (o corpo sobe, não
/// só um caminho geométrico existe). Ticket 23, spec 09 §4/§9.
/// </summary>
/// <remarks>
/// `EnemyBrainProbe` (ticket 22) já cobre percepção/estado/parede/perda de
/// alvo para UM inimigo instanciado direto da cena, sem pool. Este probe usa
/// <c>EnemyPool.Acquire</c> de propósito -- a força de separação só enxerga
/// vizinhos via <c>EnemyPool.ObterPosicoesAtivas</c>, e não há como testá-la
/// sem inimigos de verdade saídos do pool.
///
/// <code>
/// godot --headless --path . --scene res://scenes/debug/EnemyCrowdProbe.tscn
/// </code>
/// </remarks>
public sealed partial class EnemyCrowdProbe : Node
{
    /// <summary>Cena com o jogador e a arena urbana.</summary>
    [Export(PropertyHint.File, "*.tscn")]
    public string ScenePath { get; set; } = "res://scenes/arena/Arena.tscn";

    /// <summary>Definição de comportamento do grunt.</summary>
    [Export(PropertyHint.File, "*.tres")]
    public string DefinitionPath { get; set; } = "res://data/enemies/grunt.tres";

    private const int ContagemDaHorda = 16;

    /// <summary>O mesmo padrão de <c>EnemyBrain.PesoDeSeparacao</c> (spec 09 §4) -- reescrito aqui, não lido de lá, porque a rodada "sem separação" zera o campo na mesma instância pooled que a rodada seguinte reaproveita.</summary>
    private const float PesoDeSeparacaoPadrao = 0.35f;

    private enum Fase { EspalhamentoSemForca, EspalhamentoComForca, SemCaminho, Lod, EscaladaDeVerdade }

    private readonly List<string> _falhas = [];
    private CharacterController? _jogador;
    private EnemyPool? _pool;
    private EnemyDefinition? _definicao;

    private readonly List<CharacterController> _horda = [];
    private CharacterController? _grunt;
    private EnemyBrain? _cerebro;

    private Fase _fase = Fase.EspalhamentoSemForca;
    private int _quadroDaFase;
    private int _pensamentosNoInicioDaJanela;
    private float _mediaSemSeparacao;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(ScenePath);
        _definicao = GD.Load<EnemyDefinition>(DefinitionPath);
        if (packed is null || _definicao is null)
        {
            GD.PrintErr($"[horda] não consegui carregar {ScenePath} ou {DefinitionPath}");
            GetTree().Quit(1);
            return;
        }

        AddChild(packed.Instantiate());
        Procurar(this);

        _pool = GetNodeOrNull<EnemyPool>("/root/EnemyPool");
        if (_pool is null || _jogador?.Context?.Health is null)
        {
            GD.PrintErr("[horda] FALHA: EnemyPool ou jogador não resolveram.");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[horda] jogador e pool prontos");
    }

    public override void _PhysicsProcess(double delta)
    {
        // O prewarm padrão do EnemyPool é adiado (CallDeferred, ver o remark
        // do próprio pool) -- não conta quadro nenhum até ele terminar, em
        // vez de chutar quantos quadros isso leva. Mesmo padrão de
        // EnemyPoolProbe.
        if (!_pool!.IsReady)
            return;

        _quadroDaFase++;

        switch (_fase)
        {
            case Fase.EspalhamentoSemForca: TickEspalhamento(comSeparacao: false); break;
            case Fase.EspalhamentoComForca: TickEspalhamento(comSeparacao: true); break;
            case Fase.SemCaminho: TickSemCaminho((float)delta); break;
            case Fase.Lod: TickLod(); break;
            case Fase.EscaladaDeVerdade: TickEscaladaDeVerdade(); break;
        }
    }

    // --- fase 1: horda convergindo chega espalhada, não empilhada ---

    /// <remarks>
    /// Comparativa, não um limiar absoluto -- mesma técnica de
    /// <c>AerialCombatProbe.VerificarImpulsoPorComparacao</c>: com
    /// <see cref="ContagemDaHorda"/> convergindo para um `AttackRange` de
    /// 2,2 m e um raio de separação de só 1,2 m, não cabem todos numa borda
    /// respeitando 1,2 m de vizinho a vizinho (a circunferência a 2,2 m só
    /// tem espaço para ~11) -- um limiar fixo tipo "ninguém a menos de 1,2 m"
    /// seria fisicamente impossível de cumprir com esta contagem, não uma
    /// falha real do mecanismo. O que a força de separação promete é
    /// "mais espalhado que sem ela", e é isto que as duas rodadas (uma com
    /// <c>PesoDeSeparacao = 0</c>, outra com o padrão) provam por
    /// comparação direta, sob as MESMAS condições.
    /// </remarks>
    private void TickEspalhamento(bool comSeparacao)
    {
        if (_quadroDaFase == 1)
        {
            var rng = new RandomNumberGenerator();
            rng.Seed = 42; // mesmo ponto de partida nas duas rodadas -- só o peso muda
            for (var i = 0; i < ContagemDaHorda; i++)
            {
                var offset = new Vector3(rng.RandfRange(-0.4f, 0.4f), 0f, rng.RandfRange(-0.4f, 0.4f));
                var grunt = _pool!.Acquire(_definicao!, new Vector3(10f, 0.1f, 10f) + offset);
                if (grunt is null)
                    continue;

                // Ambos os ramos escrevem o peso, nunca só o "sem separação"
                // -- a MESMA instância pooled (pilha LIFO) pode voltar aqui
                // na segunda rodada, e sem reescrever o padrão de propósito
                // ela chegaria com o peso zerado herdado da primeira.
                var cerebro = grunt.GetNodeOrNull<EnemyBrain>("EnemyBrain");
                if (cerebro is not null)
                    cerebro.PesoDeSeparacao = comSeparacao ? PesoDeSeparacaoPadrao : 0f;

                _horda.Add(grunt);
            }

            Verificar(_horda.Count == ContagemDaHorda,
                $"deveria ter adquirido {ContagemDaHorda} grunts do pool, adquiriu {_horda.Count}");

            Teleportar(_jogador!, new Vector3(10f, 0.1f, 16.5f));
            return;
        }

        // Tempo de sobra para perceber, perseguir, convergir e a separação
        // agir -- não precisa ter chegado exatamente, só ter se movido e se
        // espalhado ao se aproximar.
        if (_quadroDaFase < 300)
            return;

        var media = MediaDoVizinhoMaisProximo(_horda);

        foreach (var grunt in _horda)
        {
            if (GodotObject.IsInstanceValid(grunt))
                _pool!.Release(grunt);
        }

        _horda.Clear();

        if (!comSeparacao)
        {
            _mediaSemSeparacao = media;
            AvancarFase(Fase.EspalhamentoComForca);
            return;
        }

        Verificar(media > _mediaSemSeparacao,
            "a força de separação deveria deixar a horda mais espalhada que sem ela; "
            + $"sem separação={_mediaSemSeparacao:0.00} m de vizinho médio, com separação={media:0.00} m");

        AvancarFase(Fase.SemCaminho);
    }

    /// <summary>Média, entre todos, da distância até o vizinho mais próximo -- uma métrica só de "quão espalhados".</summary>
    private static float MediaDoVizinhoMaisProximo(List<CharacterController> horda)
    {
        var validos = new List<Vector3>(horda.Count);
        foreach (var g in horda)
        {
            if (GodotObject.IsInstanceValid(g))
                validos.Add(g.GlobalPosition);
        }

        if (validos.Count < 2)
            return 0f;

        var soma = 0f;
        for (var i = 0; i < validos.Count; i++)
        {
            var maisProximo = float.PositiveInfinity;
            for (var j = 0; j < validos.Count; j++)
            {
                if (i == j)
                    continue;

                maisProximo = Mathf.Min(maisProximo, validos[i].DistanceTo(validos[j]));
            }

            soma += maisProximo;
        }

        return soma / validos.Count;
    }

    // --- fase 2: sem caminho válido, o inimigo não trava ---

    private void TickSemCaminho(float delta)
    {
        if (_quadroDaFase == 1)
        {
            _grunt = _pool!.Acquire(_definicao!, new Vector3(10f, 0.1f, 10f));
            var motor = _grunt?.Context?.NavigationMotor;

            if (_grunt is null || motor is null)
            {
                Verificar(false, "Acquire ou NavigationMotor não resolveram para o teste de caminho inválido.");
                AvancarFase(Fase.Lod);
                return;
            }

            return;
        }

        var motorDoGrunt = _grunt!.Context!.NavigationMotor!;

        // Fora do mapa inteiro (60×60 m, centrado na origem) -- sem navmesh
        // nenhum ali, um alvo genuinamente inalcançável. Chamado direto no
        // NavigationMotor (não via EnemyBrain/percepção): é a peça exata que
        // tem o fallback, "avança na direção do jogador e tenta de novo" em
        // vez de travar (ticket 23) -- testar aqui, não pela percepção
        // inteira, isola o mecanismo do que dispara ele.
        motorDoGrunt.SetTarget(new Vector3(0f, 0.1f, -60f), delta);

        if (_quadroDaFase < 30)
            return;

        var direcao = motorDoGrunt.GetDesiredDirection(_grunt.GlobalPosition);

        Verificar(direcao.LengthSquared() > 0.5f,
            $"sem caminho válido o inimigo não deveria congelar; direção devolvida foi {direcao}");
        Verificar(direcao.Z < 0f,
            $"a direção de fallback deveria apontar para o alvo (z negativo, o alvo está a -60); veio {direcao}");

        if (GodotObject.IsInstanceValid(_grunt))
            _pool!.Release(_grunt);

        AvancarFase(Fase.Lod);
    }

    // --- fase 3: inimigo distante pensa menos vezes por segundo ---

    private void TickLod()
    {
        if (_quadroDaFase == 1)
        {
            _grunt = _pool!.Acquire(_definicao!, new Vector3(-14f, 0.1f, 14f));
            _cerebro = _grunt?.GetNodeOrNull<EnemyBrain>("EnemyBrain");

            if (_grunt is null || _cerebro is null)
            {
                Verificar(false, "Acquire ou EnemyBrain não resolveram para o teste de LOD.");
                AvancarFase(Fase.EscaladaDeVerdade);
                return;
            }

            // ~42 m de distância -- bem além dos 35 m de DistanciaDeLod.
            Teleportar(_jogador!, new Vector3(14f, 0.1f, -17f));
            _pensamentosNoInicioDaJanela = _cerebro.PensamentosCompletos;
            return;
        }

        if (_quadroDaFase == 120)
        {
            var pensados = _cerebro!.PensamentosCompletos - _pensamentosNoInicioDaJanela;

            // 120 quadros a 60 Hz = 2 s; a 5 Hz (IntervaloDeLodSegundos
            // padrão de 0,2 s) seriam ~10 pensamentos, nunca perto de 120.
            Verificar(pensados < 30,
                $"distante, o grunt deveria pensar bem menos que 1x por quadro; pensou {pensados}x em 120 quadros");

            // Agora perto: o mesmo relógio deveria virar ~1 pensamento por
            // quadro (60 Hz) outra vez.
            Teleportar(_jogador!, _grunt!.GlobalPosition + new Vector3(3f, 0f, 0f));
            _pensamentosNoInicioDaJanela = _cerebro.PensamentosCompletos;
            return;
        }

        if (_quadroDaFase < 150)
            return;

        var pensadosDePerto = _cerebro!.PensamentosCompletos - _pensamentosNoInicioDaJanela;

        // 30 quadros de folga (metade do teto de "longe" acima) -- perto,
        // deveria pensar em quase todo quadro, não a cada 12.
        Verificar(pensadosDePerto > 15,
            $"perto, o grunt deveria voltar a pensar quase todo quadro; pensou só {pensadosDePerto}x em 30 quadros");

        if (GodotObject.IsInstanceValid(_grunt))
            _pool!.Release(_grunt);

        AvancarFase(Fase.EscaladaDeVerdade);
    }

    // --- fase 4: a escalada por ligação de navegação acontece de verdade ---

    private void TickEscaladaDeVerdade()
    {
        if (_quadroDaFase == 1)
        {
            // Na rua, perto do ônibus -- rua -> ônibus -> andaime é a cadeia
            // de ligações de navegação que este teste quer ver ACONTECENDO,
            // não só existindo (UrbanArenaProbe, ticket 21, já confere que o
            // NavigationServer3D acha o caminho geométrico; aqui é o
            // NavigationAgent3D de um inimigo de verdade seguindo-o com o
            // próprio corpo).
            _grunt = _pool!.Acquire(_definicao!, new Vector3(6f, 0.1f, -9f));
            _cerebro = _grunt?.GetNodeOrNull<EnemyBrain>("EnemyBrain");

            if (_grunt is null || _cerebro is null)
            {
                Verificar(false, "Acquire ou EnemyBrain não resolveram para o teste de escalada.");
                Concluir();
                return;
            }

            // Bem no topo do andaime -- dentro do alcance de ataque só dá
            // para chegar tendo subido de verdade.
            Teleportar(_jogador!, new Vector3(11f, 1.6f, -11.5f));
            return;
        }

        if (_quadroDaFase < 400)
        {
            if (_grunt!.GlobalPosition.Y > 1.2f)
            {
                Verificar(true, "");
                Concluir();
            }

            return;
        }

        Verificar(false,
            $"o grunt deveria ter subido rua -> ônibus -> andaime pelas ligações de navegação; "
            + $"ainda em y={_grunt!.GlobalPosition.Y:0.00} depois de {_quadroDaFase} quadros");
        Concluir();
    }

    private void Teleportar(CharacterController quem, Vector3 posicao)
    {
        quem.Velocity = Vector3.Zero;
        quem.GlobalPosition = posicao;
    }

    private void Verificar(bool condicao, string mensagem)
    {
        if (condicao)
            return;

        _falhas.Add(mensagem);
        GD.PrintErr($"[horda] FALHA: {mensagem}");
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
            GD.PrintErr($"[horda] {_falhas.Count} verificação(ões) falharam");
            GetTree().Quit(1);
            return;
        }

        GD.Print("[horda] todas as verificações passaram");
        GetTree().Quit();
    }

    private void Procurar(Node no)
    {
        foreach (var filho in no.GetChildren())
        {
            if (filho is CharacterController c && c.Team == Team.Player && c.Context?.Combat is not null)
                _jogador ??= c;

            Procurar(filho);
        }
    }
}
