using System;
using System.Collections.Generic;
using System.Text;
using Contenda.Components.Abilities;
using Contenda.Components.Mana;
using Contenda.Core;
using Contenda.Input;
using Contenda.Settings;
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

    /// <summary>Posição vertical da primeira combinação abaixo das instruções.</summary>
    [Export(PropertyHint.Range, "0,120,1")] public float RowTop { get; set; } = 44f;

    /// <summary>Quanto tempo a lista pisca depois de confirmar sem casamento.</summary>
    [Export(PropertyHint.Range, "0.05,1,0.05")] public float RejectFlashDuration { get; set; } = 0.25f;

    [Export] public NodePath InstructionsLabelPath { get; set; } = new();

    private readonly List<RichTextLabel> _linhas = [];
    private readonly List<(AbilityMatchState Estado, bool Pronta, int DecimosDeRecarga, bool CustoOk, bool Piscando)> _ultimoEstado = [];
    private readonly StringBuilder _textoDaLinha = new();

    private AbilityComponent? _habilidades;
    private ManaComponent? _mana;
    private Label? _instrucoes;
    private float _piscarRestante;
    private string _teclaDeConfirmar = "";

    /// <summary>A tecla de confirmar que a guia mostra agora ("+ Mouse 2"). Para o probe/depuração.</summary>
    public string ConfirmKeyText => _teclaDeConfirmar;

    public override void _Ready()
    {
        _instrucoes = GetNodeOrNull<Label>(InstructionsLabelPath);
        AtualizarConfiguracoes();
        ServiceLocator.Events.SettingsChanged += AoMudarConfiguracoes;
    }

    /// <remarks>
    /// Duas opções do jogador chegam aqui (ticket 32): mostrar ou esconder a
    /// guia, e a tecla de confirmar -- a guia mostra "+ tecla" depois de cada
    /// sequência, lida do `InputMap`, então remapear a confirmação nunca a
    /// deixa mentindo.
    /// </remarks>
    private void AoMudarConfiguracoes(SettingsChangedEvent evento)
    {
        AtualizarConfiguracoes();
        Reconstruir();
    }

    private void AtualizarConfiguracoes()
    {
        Visible = ServiceLocator.Session.Settings.Gameplay.ShowComboGuide;
        _teclaDeConfirmar = SettingsStore.DescribeAction(InputActions.CommandConfirm);
        AtualizarInstrucoes();
    }

    /// <summary>Quantas linhas existem agora. Para o probe/depuração.</summary>
    public int RowCount => _linhas.Count;

    /// <summary>Recebe as habilidades e a mana a exibir. Chamado uma vez pelo <see cref="HudController"/>.</summary>
    public void Bind(AbilityComponent habilidades, ManaComponent mana)
    {
        Unbind();

        _habilidades = habilidades;
        _mana = mana;

        _habilidades.AbilitiesChanged += Reconstruir;
        _habilidades.SequenceRejected += AoRejeitarSequencia;

        Reconstruir();
    }

    /// <summary>Remove assinaturas e referências ao personagem da partida encerrada.</summary>
    public void Unbind()
    {
        Desassinar();
        _habilidades = null;
        _mana = null;
        foreach (var linha in _linhas)
            linha.QueueFree();
        _linhas.Clear();
        _ultimoEstado.Clear();
        _piscarRestante = 0f;
        AtualizarInstrucoes();
    }

    public override void _ExitTree()
    {
        ServiceLocator.Events.SettingsChanged -= AoMudarConfiguracoes;
        Unbind();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_habilidades is null)
            return;

        var piscavaAntes = _piscarRestante > 0f;
        if (_piscarRestante > 0f)
            _piscarRestante = Mathf.Max(0f, _piscarRestante - (float)delta);

        if (piscavaAntes && _piscarRestante <= 0f)
            AtualizarInstrucoes();

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

    private void AoRejeitarSequencia()
    {
        _piscarRestante = RejectFlashDuration;
        AtualizarInstrucoes();
    }

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
                Position = new Vector2(12f, RowTop + i * RowHeight),
                Size = new Vector2(Size.X - 24f, RowHeight),
                ThemeTypeVariation = "HudCombo",
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

        var alpha = piscando ? 1f : estado == AbilityMatchState.Impossible ? 0.3f : !pronta ? 0.68f : 1f;
        linha.Modulate = new Color(1f, 1f, 1f, alpha);
    }

    private string MontarTexto(AbilityDefinition habilidade, AbilityMatchState estado, bool custoOk, bool pronta, float restante)
    {
        string rotulo;
        Color corDoEstado;
        if (!pronta && restante > 0.05f)
        {
            rotulo = $"RECARGA {restante:0.0}s";
            corDoEstado = GetThemeColor("cooldown", "AbilityGuideTheme");
        }
        else if (!custoOk)
        {
            rotulo = "SEM MANA";
            corDoEstado = GetThemeColor("unaffordable", "AbilityGuideTheme");
        }
        else
        {
            (rotulo, corDoEstado) = estado switch
            {
                AbilityMatchState.PartialMatch => ("CONTINUE", GetThemeColor("partial", "AbilityGuideTheme")),
                AbilityMatchState.Impossible => ("SEM COMBO", GetThemeColor("impossible", "AbilityGuideTheme")),
                AbilityMatchState.Complete => ("PRONTA", GetThemeColor("ready", "AbilityGuideTheme")),
                _ => ("DIGITE", GetThemeColor("neutral", "AbilityGuideTheme")),
            };
        }

        _textoDaLinha.Clear();
        AppendSequencia(habilidade, estado);
        _textoDaLinha.Append("[color=#").Append(corDoEstado.ToHtml(false)).Append("] + ").Append(_teclaDeConfirmar);
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
                .Append(GetThemeColor("unaffordable", "AbilityGuideTheme").ToHtml(false))
                .Append(']')
                .Append(habilidade.ManaCost.ToString("0"))
                .Append("[/color]");
        }

        _textoDaLinha.Append("  · ").Append(rotulo);

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
        _textoDaLinha.Append("[color=#").Append(GetThemeColor("unaffordable", "AbilityGuideTheme").ToHtml(false)).Append(']');
        AppendSequencia(habilidade);
        _textoDaLinha.Append("  ").Append(habilidade.DisplayName).Append("  · SEQUÊNCIA INVÁLIDA[/color]");
        return _textoDaLinha.ToString();
    }

    private void AtualizarInstrucoes()
    {
        if (_instrucoes is null)
            return;

        if (_piscarRestante > 0f)
        {
            _instrucoes.Text = "SEQUÊNCIA INVÁLIDA · tente uma das combinações abaixo";
            return;
        }

        var cima = SettingsStore.DescribeAction(InputActions.MoveUp);
        var esquerda = SettingsStore.DescribeAction(InputActions.MoveLeft);
        var baixo = SettingsStore.DescribeAction(InputActions.MoveDown);
        var direita = SettingsStore.DescribeAction(InputActions.MoveRight);
        var ataque = SettingsStore.DescribeAction(InputActions.AttackBasic);
        _instrucoes.Text = $"{cima}/{esquerda}/{baixo}/{direita} MOVER · {ataque} ATACAR\nDigite a sequência + {_teclaDeConfirmar} para habilidade";
    }

    private void AppendSequencia(AbilityDefinition habilidade, AbilityMatchState estado = AbilityMatchState.Neutral)
    {
        var quantidadeDigitada = estado == AbilityMatchState.PartialMatch
            ? _habilidades?.CurrentSequence.Length ?? 0
            : 0;

        for (var i = 0; i < habilidade.Sequence.Count; i++)
        {
            if (i > 0)
                _textoDaLinha.Append(' ');

            var simbolo = SimboloPara(habilidade.Sequence[i]);
            if (i < quantidadeDigitada)
            {
                _textoDaLinha.Append("[color=#")
                    .Append(GetThemeColor("partial", "AbilityGuideTheme").ToHtml(false))
                    .Append("][b][[")
                    .Append(simbolo)
                    .Append("][/b][/color]");
            }
            else if (estado == AbilityMatchState.PartialMatch)
            {
                _textoDaLinha.Append("[color=#")
                    .Append(GetThemeColor("neutral", "AbilityGuideTheme").ToHtml(false))
                    .Append(']')
                    .Append(simbolo)
                    .Append("[/color]");
            }
            else
            {
                _textoDaLinha.Append(simbolo);
            }
        }
    }

    /// <remarks>
    /// Usa as teclas atuais do InputMap para que o tutorial continue verdadeiro
    /// quando o jogador remapeia movimento.
    /// </remarks>
    private static string SimboloPara(CommandDirection direcao)
    {
        var acao = direcao switch
        {
            CommandDirection.Up => InputActions.MoveUp,
            CommandDirection.Down => InputActions.MoveDown,
            CommandDirection.Left => InputActions.MoveLeft,
            CommandDirection.Right => InputActions.MoveRight,
            _ => default,
        };

        if (acao == default)
        {
            var mensagem = $"AbilityGuide: {direcao} sem ação de movimento mapeada.";
            if (OS.IsDebugBuild())
                throw new NotSupportedException(mensagem);

            GD.PushError(mensagem);
            return "?";
        }

        return SettingsStore.DescribeAction(acao);
    }
}
