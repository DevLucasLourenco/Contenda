using System;
using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.Health;
using Contenda.Components.Stats;
using Godot;

namespace Contenda.Weapons;

/// <summary>
/// Corpo a corpo: cadeia de golpes com avanço e área de dano em janela.
/// </summary>
/// <remarks>
/// Nasceu de dentro do <c>CombatComponent</c> — ver ticket 08. Não é um
/// <c>Node</c>: o estado de cadeia e avanço já é POCO (<see cref="MeleeCombo"/>,
/// <see cref="LungeMotion"/>) por precisar de teste xUnit isolado, e mover a
/// varredura de alvos para cá não muda isso. O acesso à árvore de cena — a
/// única parte que não dá para tirar da engine — vem do <c>Node</c> dono,
/// guardado só para chamar <c>GetTree()</c>.
/// </remarks>
public sealed class MeleeWeapon : IWeapon
{
    private readonly WeaponDefinition _arma;
    private readonly CharacterContext _contexto;
    private readonly Node _dono;
    private readonly StringName _targetGroup;
    private readonly float _verticalReach;

    private readonly HashSet<ulong> _jaAtingidosNesteGolpe = [];
    private readonly List<CharacterController> _alvos = [];
    private readonly LungeMotion _avanco = new();
    private readonly MeleeCombo _combo;

    private MeleeComboStep _passoAtual;
    private bool _janelaAberta;
    private bool _criticoDoGolpeAtual;

    public MeleeWeapon(
        WeaponDefinition arma,
        CharacterContext contexto,
        Node dono,
        StringName targetGroup,
        float verticalReach)
    {
        ArgumentNullException.ThrowIfNull(arma);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(dono);

        _arma = arma;
        _contexto = contexto;
        _dono = dono;
        _targetGroup = targetGroup;
        _verticalReach = verticalReach;

        _passoAtual = PassoDe(1);
        _combo = new MeleeCombo(
            passos: Mathf.Max(1, _arma.ComboSteps.Length),
            hitStart: _passoAtual.HitWindowStart,
            hitEnd: _passoAtual.HitWindowEnd,
            comboWindowEnd: _passoAtual.ComboWindowEnd);
    }

    public bool IsAttacking => _combo.IsAttacking;
    public int ComboStep => _combo.Step;

    public event Action<int>? AttackStarted;
    public event Action<Node3D>? HitLanded;

    public void RequestBasicAttack()
    {
        var proximo = PassoDe(_combo.Step + 1);
        if (!_combo.TryStart(proximo.HitWindowStart, proximo.HitWindowEnd, proximo.ComboWindowEnd))
            return;

        _passoAtual = PassoDe(_combo.Step);

        // Cada golpe começa com a lista de atingidos limpa: é o que garante um
        // acerto por alvo por golpe, sem impedir que o próximo golpe da cadeia
        // acerte o mesmo alvo.
        _jaAtingidosNesteGolpe.Clear();

        // Sorteado AQUI, uma vez por golpe -- não em ResolverAcertos(), que
        // roda a cada Tick() enquanto a janela de acerto (vários quadros)
        // estiver aberta. Sortear lá rolaria de novo a cada quadro da mesma
        // janela, e um golpe largo o bastante para acertar alvos em quadros
        // diferentes da mesma janela poderia sair crítico para um e não para
        // outro -- exatamente a "loteria" que a spec 16 §5 proíbe. Ticket 18.
        _criticoDoGolpeAtual = CritMath.RolarNaStats(_contexto.Stats);

        // O avanco e distribuido pelo wind-up, e nao aplicado de uma vez: um
        // salto de 1 m num quadro le como teleporte. Espalhado ate a lamina
        // conectar, o personagem desliza para dentro do golpe. Ver spec 07 §4.
        _avanco.Start(_passoAtual.ForwardStep, _passoAtual.HitWindowStart);

        // O golpe novo comeca com a janela fechada. Sem zerar aqui, encadear
        // herdaria o estado do golpe anterior -- e como o encadeamento so e
        // aceito da janela de acerto em diante, o proximo Tick descartaria o
        // primeiro quadro de avanco de todo golpe 2 e 3.
        _janelaAberta = false;

        AttackStarted?.Invoke(_combo.Step);
    }

    public void Cancel()
    {
        _combo.Cancel();
        _avanco.Cancel();
        _jaAtingidosNesteGolpe.Clear();
    }

    public void ResetForSpawn()
    {
        _combo.Reset();
        _avanco.Cancel();
        _jaAtingidosNesteGolpe.Clear();
    }

    /// <remarks>
    /// A varredura por alvos só acontece com a janela ABERTA. Fora dela não há
    /// área de dano nenhuma — nunca existe hitbox permanentemente ligada, que é
    /// como um golpe acerta quem passa por perto muito depois.
    ///
    /// <paramref name="triggerHeld"/> é ignorado: corpo a corpo exige um clique
    /// novo por passo do combo (spec 07 §4). É a arma hitscan que interpreta
    /// "segurando" como cadência automática.
    /// </remarks>
    public void Tick(float delta, bool triggerHeld)
    {
        // O avanço é consumido ANTES de o relógio andar: quando a lâmina
        // conecta o deslize já acabou, e o alcance é medido de onde o
        // personagem realmente parou.
        Avancar(_avanco.Consume(delta));

        var estavaAberta = _janelaAberta;
        _combo.Advance(delta);
        _janelaAberta = _combo.IsHitWindowOpen;

        // Abrir a janela encerra o deslize: a sobra de arredondamento não vaza
        // para dentro dela nem para o golpe seguinte. É a ÚNICA parada do
        // avanço — duplicá-la num guard acima só cria duas regras para
        // divergirem.
        if (_janelaAberta || !_combo.IsAttacking)
            _avanco.Cancel();

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

    /// <summary>Empurra o corpo à frente pela distância deste quadro.</summary>
    private void Avancar(float metros)
    {
        if (metros <= 0f)
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
        foreach (var no in _dono.GetTree().GetNodesInGroup(_targetGroup))
        {
            if (no is CharacterController alvo)
                _alvos.Add(alvo);
        }
    }

    private void ResolverAcertos()
    {
        var corpo = _contexto.Body;
        var origem = corpo.GlobalPosition;
        var frenteBruta = -corpo.GlobalTransform.Basis.Z;
        var frente = new Vector3(frenteBruta.X, 0f, frenteBruta.Z).Normalized();
        var alcanceQuadrado = _arma.Range * _arma.Range;
        var cosseno = Mathf.Cos(Mathf.DegToRad(_arma.HalfAngle));

        var dano = CritMath.AplicarNaStats(
            _contexto.Stats,
            _arma.BaseDamage * _passoAtual.DamageMultiplier * (_contexto.Stats?.Get(StatId.DamageMultiplier) ?? 1f),
            _criticoDoGolpeAtual);

        var hitstop = _passoAtual.HitstopSeconds + (_criticoDoGolpeAtual ? _passoAtual.CriticalHitstopBonus : 0f);

        for (var i = 0; i < _alvos.Count; i++)
        {
            var alvo = _alvos[i];
            if (alvo == corpo || !GodotObject.IsInstanceValid(alvo))
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
                || Mathf.Abs(ate.Y) > _verticalReach)
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
                IsCritical: _criticoDoGolpeAtual));

            alvo.Context?.Movement?.ApplyKnockback(
                direcao * _arma.Knockback * _passoAtual.KnockbackMultiplier);

            // Hitstop nos DOIS envolvidos, pela duração DESTE passo -- é só
            // isso que faz o finalizador congelar mais que um golpe normal,
            // sem nenhum código distinguindo "é o último passo". Ticket 11.
            // O bônus de crítico (ticket 18) empilha por cima do mesmo jeito.
            _contexto.Health?.ApplyHitstop(hitstop);
            vida.ApplyHitstop(hitstop);

            HitLanded?.Invoke(alvo);
        }
    }
}
