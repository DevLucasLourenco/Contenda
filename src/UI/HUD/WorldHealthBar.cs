using Contenda.Components.Health;
using Godot;

namespace Contenda.UI.HUD;

/// <summary>
/// Barra de vida flutuando sobre uma entidade.
/// </summary>
/// <remarks>
/// **É 3D, não 2D.** A regra número 1 do projeto proíbe nó 2D no mundo; barras
/// sobre inimigos usam malha 3D em billboard ou `Control` projetado por
/// `UnprojectPosition`. Aqui é malha, que é mais barata com dezenas de inimigos
/// e não precisa de um viewport por alvo — ver spec 11 §2.4.
///
/// Vida de inimigo fica sobre o inimigo, e não no HUD central: em modo horda são
/// muitos, e o jogador precisa saber de qual está perto de derrubar.
/// </remarks>
public sealed partial class WorldHealthBar : Node3D
{
    /// <summary>De quem esta barra mostra a vida.</summary>
    [Export] public NodePath HealthPath { get; set; } = new();

    /// <summary>A malha que encolhe conforme a vida cai.</summary>
    [Export] public NodePath FillPath { get; set; } = new();

    /// <summary>Segundos que a barra permanece após o último golpe. Zero = sempre visível.</summary>
    [Export(PropertyHint.Range, "0,10,0.5")] public float HideAfter { get; set; } = 3f;

    /// <summary>
    /// Largura da malha da barra, em metros.
    /// </summary>
    /// <remarks>
    /// Precisa bater com o tamanho do `QuadMesh` na cena. Deduzir 0,5 de meia
    /// largura fixa faz o preenchimento deslizar para fora do fundo conforme
    /// esvazia — quanto mais larga a barra, mais visível o erro.
    /// </remarks>
    [Export(PropertyHint.Range, "0.2,4,0.1")] public float BarWidth { get; set; } = 1.2f;

    private HealthComponent? _vida;
    private Node3D? _preenchimento;
    private float _esconderEm;

    public override void _Ready()
    {
        _vida = GetNodeOrNull<HealthComponent>(HealthPath);
        _preenchimento = GetNodeOrNull<Node3D>(FillPath);

        if (_vida is null || _preenchimento is null)
        {
            GD.PushError($"{Name}: HealthPath ou FillPath não resolveram.");
            SetProcess(false);
            return;
        }

        _vida.Damaged += AoApanhar;

        // Nasce escondida: barra cheia sobre todo mundo polui a tela sem informar
        // nada. Ela aparece quando passa a ter o que dizer.
        Visible = HideAfter <= 0f;

        // Adiada: filhos ficam prontos ANTES do pai, e a vida máxima só é
        // aplicada no _Ready do contêiner. Ler agora pegaria o valor
        // provisório.
        CallDeferred(nameof(Atualizar));
    }

    public override void _ExitTree()
    {
        if (_vida is not null)
            _vida.Damaged -= AoApanhar;
    }

    public override void _Process(double delta)
    {
        if (HideAfter <= 0f || !Visible)
            return;

        if (Time.GetTicksMsec() / 1000f >= _esconderEm)
            Visible = false;
    }

    /// <summary>Devolve ao estado de recém-criada. Contrato do pool, no M5.</summary>
    public void ResetForSpawn()
    {
        _esconderEm = 0f;
        Visible = HideAfter <= 0f;

        // Adiada: filhos ficam prontos ANTES do pai, e a vida máxima só é
        // aplicada no _Ready do contêiner. Ler agora pegaria o valor
        // provisório.
        CallDeferred(nameof(Atualizar));
    }

    private void AoApanhar(DamageInfo golpe)
    {
        Atualizar();
        Visible = true;
        _esconderEm = (Time.GetTicksMsec() / 1000f) + HideAfter;
    }

    private void Atualizar()
    {
        if (_vida is null || _preenchimento is null)
            return;

        var fracao = Mathf.Clamp(_vida.Percent, 0f, 1f);

        // Encolhe pela esquerda: escalar sozinho encolheria pelos dois lados, e a
        // barra pareceria sumir para o centro em vez de esvaziar.
        _preenchimento.Scale = new Vector3(fracao, 1f, 1f);
        _preenchimento.Position = new Vector3(-(1f - fracao) * BarWidth * 0.5f, 0f, 0f);
    }
}
