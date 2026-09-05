using System;
using System.Collections.Generic;
using System.Text;
using Contenda.Components.Abilities;
using Contenda.Components.Mana;
using Contenda.Input;
using Godot;

namespace Contenda.UI.HUD;

/// <summary>
/// Lista permanente das habilidades do personagem, que reage enquanto o jogador digita.
/// </summary>
/// <remarks>
/// Ticket 16 — o critério que decide se o sistema de comandos do ticket 14 é
/// aprendível. Uma linha por habilidade, texto puro (sem ícone: mesma
/// filosofia de placeholder das barras do ticket 12, arte de verdade é
/// ticket 34+). Cada linha é um <see cref="RichTextLabel"/>, não um
/// <see cref="Label"/> simples, só para poder colorir o número do custo de
/// mana sem precisar de um nó à parte.
///
/// A cor do ESTADO da linha (neutro/parcial/impossível/pronta) também vai
/// dentro do BBCode do texto, não em <see cref="CanvasItem.Modulate"/>:
/// <c>Modulate</c> multiplica TUDO que a linha desenha, cor de estado e cor do
/// custo em vermelho juntas — testado, o resultado é uma mancha, não vermelho
/// (cor de estado ciano × vermelho dá um marrom escuro, não "custo em
/// vermelho" nenhum). <c>Modulate</c> fica só para OPACIDADE (recarga, sempre
/// branco puro na base), que nunca desloca matiz nenhuma.
///
/// Reconstrói as linhas do zero a cada <see cref="AbilityComponent.AbilitiesChanged"/>
/// — trocar de personagem (tecla de debug do ticket 12) troca a lista inteira,
/// e o número de habilidades pode até mudar entre arquétipos futuros.
///
/// Lê por POLLING em <c>_PhysicsProcess</c>, mesma escolha do
/// <see cref="HealthBar"/>: `_Process` não roda de forma confiável headless, e
/// as sondas deste projeto dependem disso. Reconstruir o texto (BBCode
/// novo + <c>RichTextLabel</c> reparseando) só quando o estado exibido de fato
/// muda -- convenções §5 proíbem alocar por quadro, e um cooldown contando
/// regressivo mudaria o texto a cada quadro se comparasse o float cru.
/// </remarks>
public sealed partial class AbilityGuide : Control
{
    /// <summary>Altura de cada linha, em pixels.</summary>
    [Export(PropertyHint.Range, "12,60,1")] public float RowHeight { get; set; } = 20f;

    /// <summary>Cor de uma linha sem nada digitado ainda.</summary>
    [Export] public Color NeutralColor { get; set; } = new(0.85f, 0.85f, 0.85f);

    /// <summary>Cor de uma linha cujo prefixo digitado ainda bate.</summary>
    [Export] public Color PartialMatchColor { get; set; } = new(0.4f, 0.85f, 1f);

    /// <summary>Cor de uma linha que o digitado já descartou.</summary>
    [Export] public Color ImpossibleColor { get; set; } = new(0.4f, 0.4f, 0.4f);

    /// <summary>Cor de uma linha pronta para confirmar.</summary>
    [Export] public Color ReadyColor { get; set; } = new(1f, 0.85f, 0.2f);

    /// <summary>Cor do número de custo quando falta mana para pagá-lo.</summary>
    [Export] public Color UnaffordableCostColor { get; set; } = new(0.95f, 0.25f, 0.25f);

    /// <summary>Opacidade de uma linha em recarga.</summary>
    [Export(PropertyHint.Range, "0.1,1,0.05")] public float CooldownAlpha { get; set; } = 0.45f;

    /// <summary>Quanto tempo a lista pisca depois de confirmar sem casamento.</summary>
    [Export(PropertyHint.Range, "0.05,1,0.05")] public float RejectFlashDuration { get; set; } = 0.25f;

    private static readonly Dictionary<CommandDirection, string> SimbolosPorDirecao = new()
    {
        [CommandDirection.Up] = "W",
        [CommandDirection.Down] = "S",
        [CommandDirection.Left] = "A",
        [CommandDirection.Right] = "D",
    };

    private readonly List<RichTextLabel> _linhas = [];
    private readonly List<(AbilityMatchState Estado, bool Pronta, int DecimosDeRecarga, bool CustoOk, bool Piscando)> _ultimoEstado = [];
    private readonly StringBuilder _textoDaLinha = new();

    private AbilityComponent? _habilidades;
    private ManaComponent? _mana;
    private float _piscarRestante;

    /// <summary>Quantas linhas existem agora. Para o probe/depuração.</summary>
    public int RowCount => _linhas.Count;

    /// <summary>Recebe as habilidades e a mana a exibir. Chamado uma vez pelo <see cref="HudController"/>.</summary>
    public void Bind(AbilityComponent habilidades, ManaComponent mana)
    {
        Desassinar();

        _habilidades = habilidades;
        _mana = mana;

        _habilidades.AbilitiesChanged += Reconstruir;
        _habilidades.SequenceRejected += AoRejeitarSequencia;

        Reconstruir();
    }

    public override void _ExitTree() => Desassinar();

    public override void _PhysicsProcess(double delta)
    {
        if (_habilidades is null)
            return;

        if (_piscarRestante > 0f)
            _piscarRestante = Mathf.Max(0f, _piscarRestante - (float)delta);

        for (var i = 0; i < _linhas.Count; i++)
            AtualizarLinha(i);
    }

    /// <summary>O estado visual da linha <paramref name="indice"/> agora. Para o probe/depuração.</summary>
    public AbilityMatchState StateOf(int indice)
    {
        if (_habilidades is null || indice < 0 || indice >= _habilidades.Abilities.Count)
            return AbilityMatchState.Neutral;

        return AbilityMatchEvaluator.Evaluate(_habilidades.Abilities[indice].Sequence, _habilidades.CurrentSequence);
    }

    /// <summary>A opacidade da linha <paramref name="indice"/> agora. Para o probe/depuração.</summary>
    public float AlphaOf(int indice) => indice >= 0 && indice < _linhas.Count ? _linhas[indice].Modulate.A : 0f;

    /// <summary>Se a lista está piscando por "confirmou sem casamento" agora. Para o probe/depuração.</summary>
    public bool IsFlashing => _piscarRestante > 0f;

    private void Desassinar()
    {
        if (_habilidades is null)
            return;

        _habilidades.AbilitiesChanged -= Reconstruir;
        _habilidades.SequenceRejected -= AoRejeitarSequencia;
    }

    private void AoRejeitarSequencia() => _piscarRestante = RejectFlashDuration;

    /// <remarks>
    /// Chamado no <see cref="Bind"/> inicial e sempre que
    /// <see cref="AbilityComponent.AbilitiesChanged"/> disparar — a troca de
    /// arquétipo (tecla de debug, ticket 12) reconfigura o MESMO
    /// <c>AbilityComponent</c>, então sem isto a lista ficaria presa nas
    /// habilidades do personagem anterior.
    /// </remarks>
    private void Reconstruir()
    {
        foreach (var linha in _linhas)
            linha.QueueFree();

        _linhas.Clear();
        _ultimoEstado.Clear();

        if (_habilidades is null)
            return;

        for (var i = 0; i < _habilidades.Abilities.Count; i++)
        {
            var linha = new RichTextLabel
            {
                BbcodeEnabled = true,
                ScrollActive = false,
                Position = new Vector2(0f, i * RowHeight),
                Size = new Vector2(Size.X, RowHeight),
            };

            AddChild(linha);
            _linhas.Add(linha);

            // Sentinela que nenhum estado real produz (DecimosDeRecarga
            // negativo): garante que a primeira AtualizarLinha depois de
            // Reconstruir sempre desenhe o texto, mesmo que por coincidência
            // bata com o que Evaluate devolveria por padrão.
            _ultimoEstado.Add((AbilityMatchState.Neutral, false, -1, false, false));
        }

        for (var i = 0; i < _linhas.Count; i++)
            AtualizarLinha(i);
    }

    private void AtualizarLinha(int indice)
    {
        // `!`: só chamado a partir de Reconstruir/_PhysicsProcess, ambos já
        // atrás de um guard de _habilidades não nulo.
        var habilidade = _habilidades!.Abilities[indice];
        var linha = _linhas[indice];

        var estado = AbilityMatchEvaluator.Evaluate(habilidade.Sequence, _habilidades.CurrentSequence);
        var pronta = _habilidades.IsReady(habilidade.Id);
        var restante = _habilidades.CooldownRemaining(habilidade.Id);
        var custoOk = _mana?.CanConsume(habilidade.ManaCost) ?? true;
        var piscando = _piscarRestante > 0f;

        // Arredondado à mesma casa decimal exibida no texto ("0.0s"): um
        // cooldown decrescendo de verdade mudaria isto a cada quadro se
        // comparasse o float cru, e o dirty-check nunca gatilharia.
        var decimosDeRecarga = Mathf.RoundToInt(restante * 10f);

        var novoEstado = (estado, pronta, decimosDeRecarga, custoOk, piscando);
        if (novoEstado != _ultimoEstado[indice])
        {
            linha.Text = piscando
                ? MontarTextoDePisco(habilidade)
                : MontarTexto(habilidade, estado, custoOk, pronta, restante);

            _ultimoEstado[indice] = novoEstado;
        }

        // Só opacidade aqui -- ver o comentário da classe sobre por que a cor
        // de estado e a cor do custo vivem no BBCode, não em Modulate.
        linha.Modulate = new Color(1f, 1f, 1f, piscando || pronta ? 1f : CooldownAlpha);
    }

    private string MontarTexto(AbilityDefinition habilidade, AbilityMatchState estado, bool custoOk, bool pronta, float restante)
    {
        var corDoEstado = estado switch
        {
            AbilityMatchState.PartialMatch => PartialMatchColor,
            AbilityMatchState.Impossible => ImpossibleColor,
            AbilityMatchState.Complete => ReadyColor,
            _ => NeutralColor,
        };

        _textoDaLinha.Clear();
        _textoDaLinha.Append("[color=#").Append(corDoEstado.ToHtml(false)).Append(']');

        AppendSequencia(habilidade);
        _textoDaLinha.Append("  ").Append(habilidade.DisplayName).Append("  ");

        if (custoOk)
        {
            _textoDaLinha.Append(habilidade.ManaCost.ToString("0"));
        }
        else
        {
            // Tag de cor ANINHADA, não Modulate: um vermelho aninhado dentro
            // da cor de estado continua vermelho puro -- é a própria correção
            // do problema descrito no comentário da classe.
            _textoDaLinha.Append("[color=#")
                .Append(UnaffordableCostColor.ToHtml(false))
                .Append(']')
                .Append(habilidade.ManaCost.ToString("0"))
                .Append("[/color]");
        }

        if (!pronta)
            _textoDaLinha.Append("  (").Append(restante.ToString("0.0")).Append("s)");

        _textoDaLinha.Append("[/color]");
        return _textoDaLinha.ToString();
    }

    /// <summary>
    /// O aviso de "confirmou sem casamento nenhum" (ticket 16) simplifica de
    /// propósito: nada de custo em vermelho nem cor de estado competindo por
    /// atenção no meio de um pisco que já é, ele mesmo, o aviso.
    /// </summary>
    private string MontarTextoDePisco(AbilityDefinition habilidade)
    {
        _textoDaLinha.Clear();
        _textoDaLinha.Append("[color=#").Append(UnaffordableCostColor.ToHtml(false)).Append(']');
        AppendSequencia(habilidade);
        _textoDaLinha.Append("  ").Append(habilidade.DisplayName).Append("[/color]");
        return _textoDaLinha.ToString();
    }

    private void AppendSequencia(AbilityDefinition habilidade)
    {
        for (var i = 0; i < habilidade.Sequence.Count; i++)
        {
            if (i > 0)
                _textoDaLinha.Append(' ');

            _textoDaLinha.Append(SimboloPara(habilidade.Sequence[i]));
        }
    }

    /// <remarks>
    /// Erro de PROGRAMAÇÃO, não de conteúdo -- <see cref="CommandDirection"/>
    /// é um enum fechado que só o próprio código pode estender, então isto só
    /// dispararia se alguém adicionasse uma direção nova sem atualizar
    /// <see cref="SimbolosPorDirecao"/> junto. Falha alto em debug, loga e
    /// mostra "?" em release -- convenções §9, mesmo padrão da
    /// `WeaponFactory`/`AbilityBehaviorRegistry` para conteúdo sem implementação.
    /// </remarks>
    private static string SimboloPara(CommandDirection direcao)
    {
        if (SimbolosPorDirecao.TryGetValue(direcao, out var simbolo))
            return simbolo;

        var mensagem = $"AbilityGuide: {direcao} sem símbolo mapeado em SimbolosPorDirecao.";
        if (OS.IsDebugBuild())
            throw new NotSupportedException(mensagem);

        GD.PushError(mensagem);
        return "?";
    }
}
