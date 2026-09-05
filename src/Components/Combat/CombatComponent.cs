using System;
using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.Health;
using Contenda.Components.Stats;
using Contenda.Weapons;
using Godot;

namespace Contenda.Components.Combat;

/// <summary>
/// Executa o ataque básico com a arma equipada.
/// </summary>
/// <remarks>
/// **M1 não significa "soco", significa "ataque básico".** Este componente não
/// sabe se está segurando espada ou revólver: ele pede o golpe e a
/// <see cref="WeaponDefinition"/> decide o que isso quer dizer. É a abstração
/// que permite o ticket 09 existir sem um `if` sobre qual personagem está em
/// jogo — e o terceiro, o décimo e o quinquagésimo personagem depois.
/// Ver spec 07 §1.
/// </remarks>
public sealed partial class CombatComponent : Node, ICharacterComponent
{
    /// <summary>A arma, quando a definição do personagem não traz a sua.</summary>
    [Export] public WeaponDefinition? Fallback { get; set; }

    /// <summary>Grupo varrido em busca de alvos.</summary>
    [Export] public StringName TargetGroup { get; set; } = new("damageable");

    /// <summary>
    /// Diferenca de altura tolerada entre quem golpeia e quem apanha, em metros.
    /// </summary>
    /// <remarks>
    /// Generosa de proposito: sob camera inclinada o jogador julga altura mal, e
    /// recusar um golpe por meio metro de desnivel parece bug. O combate aereo do
    /// ticket 19 vai querer isto configuravel por golpe.
    /// </remarks>
    [Export(PropertyHint.Range, "0.5,6,0.1")] public float VerticalReach { get; set; } = 2.5f;

    private readonly HashSet<ulong> _jaAtingidosNesteGolpe = [];
    private readonly List<CharacterController> _alvos = [];

    private CharacterContext? _contexto;
    private bool _janelaAberta;
    private WeaponDefinition _arma = new();
    private MeleeCombo? _combo;
    private MeleeComboStep _passoAtual = new();

    /// <summary>Se há um golpe em andamento.</summary>
    public bool IsAttacking => _combo?.IsAttacking ?? false;

    /// <summary>Passo atual da cadeia, de 1 a N. Zero quando ocioso.</summary>
    public int ComboStep => _combo?.Step ?? 0;

    /// <summary>Avisa que um golpe começou, com o passo da cadeia.</summary>
    public event Action<int>? AttackStarted;

    /// <summary>Avisa que um golpe conectou.</summary>
    public event Action<Node3D>? HitLanded;

    public void Bind(CharacterContext contexto) => _contexto = contexto;

    public void Configure(CharacterDefinition definicao)
    {
        _arma = definicao.Weapon ?? Fallback ?? new WeaponDefinition();
        _passoAtual = PassoDe(1);

        _combo = new MeleeCombo(
            passos: Mathf.Max(1, _arma.ComboSteps.Length),
            hitStart: _passoAtual.HitWindowStart,
            hitEnd: _passoAtual.HitWindowEnd,
            comboWindowEnd: _passoAtual.ComboWindowEnd);
    }

    /// <summary>Pede um ataque básico. Chamado pelo contêiner ao apertar M1.</summary>
    public void RequestBasicAttack()
    {
        if (_combo is null)
            return;

        var proximo = PassoDe(_combo.Step + 1);
        if (!_combo.TryStart(proximo.HitWindowStart, proximo.HitWindowEnd, proximo.ComboWindowEnd))
            return;

        _passoAtual = PassoDe(_combo.Step);

        // Cada golpe começa com a lista de atingidos limpa: é o que garante um
        // acerto por alvo por golpe, sem impedir que o próximo golpe da cadeia
        // acerte o mesmo alvo.
        _jaAtingidosNesteGolpe.Clear();

        Avancar(_passoAtual.ForwardStep);
        AttackStarted?.Invoke(_combo.Step);
    }

    /// <summary>Interrompe o golpe. Chamado ao morrer ou tomar atordoamento.</summary>
    public void Cancel()
    {
        _combo?.Cancel();
        _jaAtingidosNesteGolpe.Clear();
    }

    /// <summary>Devolve ao estado de recém-criado. Contrato do pool, no M5.</summary>
    public void ResetForSpawn()
    {
        _combo?.Reset();
        _jaAtingidosNesteGolpe.Clear();
    }

    /// <summary>
    /// Avança o golpe e resolve acertos. Chamado pelo contêiner.
    /// </summary>
    /// <remarks>
    /// A varredura por alvos só acontece com a janela ABERTA. Fora dela não há
    /// área de dano nenhuma — nunca existe hitbox permanentemente ligada, que é
    /// como um golpe acerta quem passa por perto muito depois.
    /// </remarks>
    public void Tick(float delta)
    {
        if (_combo is null)
            return;

        var estavaAberta = _janelaAberta;
        _combo.Advance(delta);
        _janelaAberta = _combo.IsHitWindowOpen;

        if (_janelaAberta && !estavaAberta)
            AmostrarAlvos();

        if (_janelaAberta)
            ResolverAcertos();
    }

    private MeleeComboStep PassoDe(int passo)
    {
        var indice = Mathf.Clamp(passo - 1, 0, _arma.ComboSteps.Length - 1);
        return _arma.ComboSteps.Length > 0 ? _arma.ComboSteps[indice] : new MeleeComboStep();
    }

    private void Avancar(float metros)
    {
        if (_contexto is null || metros <= 0f)
            return;

        var corpo = _contexto.Body;
        var frenteBruta = -corpo.GlobalTransform.Basis.Z;
        var frente = new Vector3(frenteBruta.X, 0f, frenteBruta.Z).Normalized();

        // MoveAndCollide, e nao soma direta em GlobalPosition: a soma acontece
        // depois do MoveAndSlide do movimento, sem varredura de colisao, e
        // atravessa parede. Com 1 m de avanco no terceiro golpe o jogador
        // terminava DENTRO da parede da borda, e a despenetracao do quadro
        // seguinte o mandava para fora da arena.
        corpo.MoveAndCollide(frente * metros);
    }

    /// <summary>
    /// Fotografa quem pode ser atingido, uma vez por golpe.
    /// </summary>
    /// <remarks>
    /// <c>GetNodesInGroup</c> devolve um <c>Godot.Collections.Array</c> novo a
    /// cada chamada. A janela de acerto dura vários quadros, então chamar por
    /// quadro alocaria por quadro — proibido pelas convenções §5. Amostrar na
    /// abertura da janela também é mais previsível: o golpe atinge quem estava
    /// ali quando a lâmina passou, não quem entrou depois.
    /// </remarks>
    private void AmostrarAlvos()
    {
        _alvos.Clear();

        // Fronteira com a engine: Godot.Collections só aqui, uma vez por golpe.
        foreach (var no in GetTree().GetNodesInGroup(TargetGroup))
        {
            if (no is CharacterController alvo)
                _alvos.Add(alvo);
        }
    }

    private void ResolverAcertos()
    {
        if (_contexto is null)
            return;

        var corpo = _contexto.Body;
        var origem = corpo.GlobalPosition;
        var frenteBruta = -corpo.GlobalTransform.Basis.Z;
        var frente = new Vector3(frenteBruta.X, 0f, frenteBruta.Z).Normalized();
        var alcanceQuadrado = _arma.Range * _arma.Range;
        var cosseno = Mathf.Cos(Mathf.DegToRad(_arma.HalfAngle));

        var dano = _arma.BaseDamage
                   * _passoAtual.DamageMultiplier
                   * (_contexto.Stats?.Get(StatId.DamageMultiplier) ?? 1f);

        for (var i = 0; i < _alvos.Count; i++)
        {
            var alvo = _alvos[i];
            if (alvo == corpo || !IsInstanceValid(alvo))
                continue;

            // Fogo amigo é desligado em CÓDIGO, além das camadas de física: uma
            // camada mal configurada no editor é fácil de introduzir e difícil
            // de notar. Defesa em profundidade — spec 07 §6.
            if (alvo.Team == _contexto.Team)
                continue;

            var id = alvo.GetInstanceId();
            if (_jaAtingidosNesteGolpe.Contains(id))
                continue;

            var vida = alvo.Context?.Health;
            if (vida is null || !vida.IsAlive)
                continue;

            // Alcance e cone medidos NO PLANO. Em 3D, a diferenca de altura
            // entre quem golpeia e quem apanha reprova o acerto -- e ela existe
            // sempre, porque a origem do corpo fica no centro da capsula e cada
            // um assenta no chao numa altura. A folga vertical e tratada a parte.
            var ate = alvo.GlobalPosition - origem;
            var noPlano = new Vector3(ate.X, 0f, ate.Z);

            if (noPlano.LengthSquared() > alcanceQuadrado
                || Mathf.Abs(ate.Y) > VerticalReach)
                continue;

            var direcao = noPlano.Length() > 0.001f ? noPlano.Normalized() : frente;
            if (frente.Dot(direcao) < cosseno)
                continue;

            // O registro entra só aqui, imediatamente antes de aplicar. Marcar
            // antes das checagens e desmarcar na falha funciona, mas qualquer
            // saída antecipada acrescentada no meio daria imunidade silenciosa
            // pelo resto do golpe.
            _jaAtingidosNesteGolpe.Add(id);

            vida.ApplyDamage(new DamageInfo(
                Amount: dano,
                Type: DamageType.Physical,
                HitPoint: alvo.GlobalPosition,
                Direction: direcao,
                Knockback: _arma.Knockback * _passoAtual.KnockbackMultiplier,
                SourceId: corpo.GetInstanceId(),
                SourceTag: _arma.Id,
                IsCritical: false));

            alvo.Context?.Movement?.ApplyKnockback(
                direcao * _arma.Knockback * _passoAtual.KnockbackMultiplier);

            HitLanded?.Invoke(alvo);
        }
    }
}
