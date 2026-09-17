using Contenda.Characters.Base;
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
///
/// Implementa <see cref="ICharacterComponent"/> só para entrar na varredura
/// do <c>CharacterController</c> e ganhar <see cref="ResetForSpawn"/> de
/// graça na reciclagem do pool (ticket 25) -- continua resolvendo
/// <see cref="HealthPath"/> sozinha, do próprio jeito, em vez de passar a
/// depender de <c>CharacterContext.Health</c>: isto não é o problema que este
/// ticket precisa resolver.
/// </remarks>
public sealed partial class WorldHealthBar : Node3D, ICharacterComponent
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

    /// <summary>O nome exibido acima da barra -- só para elite/chefe.</summary>
    [Export] public NodePath NameLabelPath { get; set; } = new();

    /// <summary>
    /// Quanto a barra inteira cresce para uma elite/chefe, multiplicador
    /// sobre o tamanho normal.
    /// </summary>
    /// <remarks>
    /// Spec 11 §2.4: "Elites e boss têm barra maior, COM NOME" -- as duas
    /// coisas juntas, não só o tingimento da própria malha
    /// (<c>EliteMarkerComponent</c>, ticket 26). Spec 11 §4 também proíbe
    /// comunicar só por cor ("daltonismo: forma/ícone/texto sempre
    /// acompanham") -- o tingimento sozinho já seria uma violação disto.
    /// </remarks>
    [Export(PropertyHint.Range, "1,3,0.05")] public float DestaqueScale { get; set; } = 1.4f;

    private HealthComponent? _vida;
    private Node3D? _preenchimento;
    private Label3D? _nome;
    private CharacterContext? _contexto;
    private float _esconderEm;

    /// <summary>
    /// Só guarda o contexto (para <see cref="AplicarDestaqueSeForCaso"/> ler
    /// <c>EnemyBrain.Definition</c>/`Owner.Definition` depois) -- continua
    /// resolvendo <see cref="HealthPath"/>/<see cref="FillPath"/> sozinha, do
    /// próprio jeito. Ver o remark da classe.
    /// </summary>
    public void Bind(CharacterContext contexto)
    {
        _contexto = contexto;
    }

    /// <remarks>
    /// <c>EnemyBrain.Definition</c> só é lido AQUI, na segunda passada do
    /// contêiner, nunca em <c>Bind</c> -- mesmo motivo documentado em
    /// <c>EnemyBrain.Configure</c>.
    /// </remarks>
    public void Configure(CharacterDefinition definicao) => AplicarDestaqueSeForCaso();

    public override void _Ready()
    {
        _vida = GetNodeOrNull<HealthComponent>(HealthPath);
        _preenchimento = GetNodeOrNull<Node3D>(FillPath);
        _nome = GetNodeOrNull<Label3D>(NameLabelPath);

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
        AplicarDestaqueSeForCaso();

        // Adiada: filhos ficam prontos ANTES do pai, e a vida máxima só é
        // aplicada no _Ready do contêiner. Ler agora pegaria o valor
        // provisório.
        CallDeferred(nameof(Atualizar));
    }

    /// <summary>
    /// Cresce a barra inteira e mostra o nome se este personagem for uma
    /// elite ou o chefe -- spec 11 §2.4. Chamado em <see cref="Configure"/>
    /// (nascimento) e de novo em <see cref="ResetForSpawn"/> (reciclagem do
    /// pool, ticket 25): como só o primeiro roda de novo por vida inteira do
    /// nó pooled, um inimigo reciclado como uma espécie DIFERENTE (elite
    /// virando comum, ou vice-versa -- ver o fix de <c>EnemyPool.CriarInstancia</c>)
    /// precisa reafirmar isto a cada reciclagem, não só na criação.
    /// </summary>
    private void AplicarDestaqueSeForCaso()
    {
        var def = _contexto?.EnemyBrain?.Definition;
        var destacado = (def?.IsElite ?? false) || (def?.IsBoss ?? false);

        Scale = Vector3.One * (destacado ? DestaqueScale : 1f);

        if (_nome is null)
            return;

        _nome.Visible = destacado;
        _nome.Text = destacado ? _contexto?.Owner.Definition?.DisplayName ?? "" : "";
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
