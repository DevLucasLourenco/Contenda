using System;
using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.Abilities;
using Contenda.Components.Combat;
using Contenda.Components.Health;
using Contenda.Components.Stats;
using Contenda.Core;
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
    /// <summary>Fonte da trava aplicada durante a recuperação da estocada de queda.</summary>
    private const string DiveRecoverySource = "weapon.dive_recovery";

    private readonly WeaponDefinition _arma;
    private readonly CharacterContext _contexto;
    private readonly Node _dono;
    private readonly StringName _targetGroup;
    private readonly float _verticalReach;

    private readonly HashSet<ulong> _jaAtingidosNesteGolpe = [];
    private readonly List<CharacterController> _alvos = [];
    private readonly LungeMotion _avanco = new();
    private readonly MeleeCombo _combo;
    private readonly MeleeCombo _comboAereo;

    private MeleeComboStep _passoAtual;
    private bool _janelaAberta;
    private bool _criticoDoGolpeAtual;
    private bool _emCombateAereo;
    private bool _mergulhando;

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

        // Janelas de sobra: sempre sobrescritas pelo TryStart de verdade em
        // RequestBasicAttack, que nunca usa o TryStart() sem parâmetros --
        // ver o mesmo comentário em _combo, acima.
        _comboAereo = new MeleeCombo(
            passos: Mathf.Max(1, _arma.AerialComboSteps.Length),
            hitStart: 0f, hitEnd: 0f, comboWindowEnd: 0f);
    }

    /// <summary>
    /// Também true durante a estocada de queda: é o que faz
    /// <c>CombatComponent.Tick</c> travar Movimento e Rotação sozinho
    /// enquanto ela durar, com o mesmo refresco por quadro que já usa para
    /// um golpe comum -- nenhuma trava nova precisou ser ensinada a ele.
    /// </summary>
    public bool IsAttacking => _combo.IsAttacking || _comboAereo.IsAttacking || _mergulhando;

    public int ComboStep => ComboAtivo.Step;

    public event Action<int>? AttackStarted;
    public event Action<Node3D>? HitLanded;
    public event Action? ReloadStarted { add { } remove { } }
    public event Action? DiveStarted;

    public void RequestBasicAttack()
    {
        // Decide se a cadeia INTEIRA é aérea só ao começar do zero: uma
        // cadeia que começou no ar continua aérea mesmo se aterrissar no
        // meio (e vice-versa) -- o jogador já comprometeu com aquele ritmo
        // de golpes ao dar o primeiro clique. Arma sem combo aéreo definido
        // (`AerialComboSteps` vazio) nunca entra neste modo -- o corpo a
        // corpo simplesmente não ataca no ar. Ticket 19, spec 16 §6.
        if (!_combo.IsAttacking && !_comboAereo.IsAttacking)
            _emCombateAereo = !EstaApoiado() && _arma.AerialComboSteps.Length > 0;

        var combo = ComboAtivo;
        var proximo = PassoDe(combo.Step + 1);
        if (!combo.TryStart(proximo.HitWindowStart, proximo.HitWindowEnd, proximo.ComboWindowEnd))
            return;

        _passoAtual = PassoDe(combo.Step);

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

        AttackStarted?.Invoke(combo.Step);

        var corpo = _contexto.Body;
        var frente = -corpo.GlobalTransform.Basis.Z;
        var direcao = new Vector3(frente.X, 0f, frente.Z).Normalized();
        ServiceLocator.Events.RaiseMeleeSwing(new MeleeSwingEvent(
            corpo.GlobalPosition,
            direcao,
            combo.Step,
            _emCombateAereo));
    }

    public void Cancel()
    {
        _combo.Cancel();
        _comboAereo.Cancel();
        _avanco.Cancel();
        _jaAtingidosNesteGolpe.Clear();
        _mergulhando = false;
        ZerarGravidadeExterna();
    }

    public void ResetForSpawn()
    {
        _combo.Reset();
        _comboAereo.Reset();
        _avanco.Cancel();
        _jaAtingidosNesteGolpe.Clear();
        _emCombateAereo = false;
        _mergulhando = false;
        ZerarGravidadeExterna();
    }

    /// <summary>Devolve a gravidade do atacante a 1x. Chamado sempre que uma cadeia/mergulho encerra fora do fluxo normal.</summary>
    private void ZerarGravidadeExterna()
    {
        if (_contexto.Movement is not null)
            _contexto.Movement.ExternalGravityScale = 1f;
    }

    /// <remarks>
    /// A varredura por alvos só acontece com a janela ABERTA. Fora dela não há
    /// área de dano nenhuma — nunca existe hitbox permanentemente ligada, que é
    /// como um golpe acerta quem passa por perto muito depois.
    ///
    /// <paramref name="triggerHeld"/> é ignorado para o combo (corpo a corpo
    /// exige um clique novo por passo, spec 07 §4 — é a arma hitscan que
    /// interpreta "segurando" como cadência automática), mas passa a decidir
    /// a estocada de queda a partir do ticket 19: segurar depois do pico do
    /// pulo, sem golpe algum em andamento, mergulha. Ver
    /// <see cref="AerialCombatMath.DeveComecarMergulho"/>.
    /// </remarks>
    public void Tick(float delta, bool triggerHeld)
    {
        if (_mergulhando)
        {
            AtualizarMergulho(delta);
            return;
        }

        var combo = ComboAtivo;
        var noAr = !EstaApoiado();
        var velocidadeY = _contexto.Movement?.Velocity.Y ?? 0f;

        if (AerialCombatMath.DeveComecarMergulho(noAr, velocidadeY, triggerHeld, combo.IsAttacking, _mergulhando))
        {
            _mergulhando = true;
            DiveStarted?.Invoke();
            AtualizarMergulho(delta);
            return;
        }

        // O avanço é consumido ANTES de o relógio andar: quando a lâmina
        // conecta o deslize já acabou, e o alcance é medido de onde o
        // personagem realmente parou.
        Avancar(_avanco.Consume(delta));

        var estavaAberta = _janelaAberta;
        combo.Advance(delta);
        _janelaAberta = combo.IsHitWindowOpen;

        // Abrir a janela encerra o deslize: a sobra de arredondamento não vaza
        // para dentro dela nem para o golpe seguinte. É a ÚNICA parada do
        // avanço — duplicá-la num guard acima só cria duas regras para
        // divergirem.
        if (_janelaAberta || !combo.IsAttacking)
            _avanco.Cancel();

        if (_janelaAberta && !estavaAberta)
            AmostrarAlvos();

        if (_janelaAberta)
            ResolverAcertos();

        // Sempre um valor definido, nunca só quando a redução está ativa --
        // ver o comentário de MovementComponent.ExternalGravityScale sobre
        // por que "esquecer de devolver a 1" é o bug óbvio aqui.
        if (_contexto.Movement is not null)
        {
            _contexto.Movement.ExternalGravityScale =
                _emCombateAereo && _janelaAberta ? _arma.AerialGravityScale : 1f;
        }
    }

    private MeleeCombo ComboAtivo => _emCombateAereo ? _comboAereo : _combo;

    private MeleeComboStep PassoDe(int passo)
    {
        var passos = _emCombateAereo ? _arma.AerialComboSteps : _arma.ComboSteps;
        var indice = Mathf.Clamp(passo - 1, 0, passos.Length - 1);
        return passos.Length > 0 ? passos[indice] : new MeleeComboStep();
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

        // Alcance/cone/folga vertical menores no ar (spec 16 §6) -- e a folga
        // vertical na verdade MAIOR, porque um alvo em pleno juggle sobe e
        // desce mais do que um golpe de solo espera. Ticket 19.
        var alcance = _emCombateAereo ? _arma.AerialRange : _arma.Range;
        var meiaAbertura = _emCombateAereo ? _arma.AerialHalfAngle : _arma.HalfAngle;
        var alcanceVertical = _emCombateAereo ? _arma.AerialVerticalReach : _verticalReach;
        var alcanceQuadrado = alcance * alcance;
        var cosseno = Mathf.Cos(Mathf.DegToRad(meiaAbertura));

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
                || Mathf.Abs(ate.Y) > alcanceVertical)
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

            // Sustenta o combo aéreo: empurrãozinho vertical no alvo E em
            // quem golpeou -- spec 16 §6. `RegistrarAcertoAereo` já incrementa
            // o contador de juggle DO ALVO como efeito colateral; só o
            // avaliamos (e só aplicamos o impulso, aos dois) enquanto o golpe
            // for aéreo -- o `&&` de curto-circuito garante que um golpe de
            // solo nunca conta como acerto aéreo. Depois do teto (4, ticket
            // 19), o impulso para para os dois, e a gravidade normal retoma
            // sozinha -- é assim que o alvo "cai".
            //
            // `!EstaApoiado()` também entra aqui, e não só em `_emCombateAereo`:
            // uma cadeia que começou no ar continua usando os PASSOS aéreos
            // (dano/alcance mais fracos) mesmo se aterrissar no meio -- é a
            // decisão de RequestBasicAttack, documentada lá -- mas o impulso
            // vertical é outra história. Sem esta checagem, um passo que
            // conecta DEPOIS de quem golpeou já ter aterrissado lançaria um
            // personagem apoiado no chão (e o alvo) para cima do nada, o que
            // a spec nunca pediu -- ela fala em sustentar um combo que já
            // está no ar, não decolar um do chão.
            if (_emCombateAereo && !EstaApoiado() && vida.RegistrarAcertoAereo())
            {
                var impulso = Vector3.Up * _arma.AerialVerticalKnockback;
                alvo.Context?.Movement?.ApplyKnockback(impulso);
                _contexto.Movement?.ApplyKnockback(impulso);
            }

            HitLanded?.Invoke(alvo);
        }
    }

    /// <summary>
    /// Avança a estocada de queda: puxa a gravidade para baixo com força e
    /// desliza para a frente, até tocar o chão.
    /// </summary>
    /// <remarks>
    /// Nenhuma varredura de "toquei o chão" própria: <c>MovementComponent.Tick</c>
    /// já rodou ANTES deste, no mesmo quadro (spec 01 §6), então
    /// <see cref="EstaApoiado"/> aqui já reflete o `MoveAndSlide` de agora
    /// mesmo -- ao contrário do `noChao` que o próprio `MovementComponent`
    /// lê no topo do seu Tick, aqui não há defasagem de quadro nenhuma para
    /// corrigir.
    /// </remarks>
    private void AtualizarMergulho(float delta)
    {
        if (_contexto.Movement is not null)
            _contexto.Movement.ExternalGravityScale = _arma.DiveGravityScale;

        Avancar(_arma.DiveForwardSpeed * delta);

        if (EstaApoiado())
            AterrissarMergulho();
    }

    /// <summary>
    /// Se o corpo deste atacante está apoiado no chão.
    /// </summary>
    /// <remarks>
    /// Lê <c>MovementComponent.IsGrounded</c> (cacheado, nunca `corpo.IsOnFloor()`
    /// direto): um `CharacterBody3D` que nunca rodou `MoveAndSlide` devolve
    /// falso ali, e só quem pede um ataque básico DIRETO (sem passar pelo
    /// fluxo normal de intenção -- todo probe deste projeto) consegue pedir
    /// antes do primeiro Tick de movimento algum dia acontecer. Sem
    /// personagem com movimento (um manequim sem `MovementComponent`, por
    /// exemplo), assume apoiado -- nunca teria como atacar no ar mesmo.
    /// </remarks>
    private bool EstaApoiado() => _contexto.Movement?.IsGrounded ?? true;

    /// <summary>Dano em área ao aterrissar, recuperação longa, fim do mergulho.</summary>
    private void AterrissarMergulho()
    {
        _mergulhando = false;
        ZerarGravidadeExterna();
        ResolverAcertosDoMergulho();

        // Mesma trava de Movimento+Rotação do golpe comum, pedida direto
        // (a estocada já não conta como "atacando" a esta altura) -- é o
        // risco que equilibra o poder da estocada, spec 16 §6.
        _contexto.Combat?.ApplyLock(
            DiveRecoverySource, ActionLock.Movement | ActionLock.Rotation, _arma.DiveRecoverySeconds);
    }

    /// <summary>
    /// Dano em área ao redor do ponto de pouso, com repulsão radial.
    /// </summary>
    /// <remarks>
    /// Uma rolagem de crítico só, compartilhada por toda a explosão -- mesma
    /// disciplina do ticket 18 -- MAS sobrescrita para sempre-crítico contra
    /// qualquer alvo que ainda esteja no ar no instante do pouso (spec 16
    /// §6: "sempre crítico se acertar um alvo aéreo"). Isto não reintroduz a
    /// "loteria" que o ticket 18 evita: a condição não é um segundo sorteio
    /// por alvo, é um fato objetivo e visível (o alvo estava no ar ou não) --
    /// o jogador entende por que um alvo criticou e o outro não sem
    /// precisar acreditar em sorte.
    /// </remarks>
    private void ResolverAcertosDoMergulho()
    {
        var corpo = _contexto.Body;
        var origem = corpo.GlobalPosition;
        var raioQuadrado = _arma.DiveRadius * _arma.DiveRadius;

        var criticoSorteado = CritMath.RolarNaStats(_contexto.Stats);
        var danoBase = _arma.DiveDamage * (_contexto.Stats?.Get(StatId.DamageMultiplier) ?? 1f);

        AbilityTargeting.ForEachValidTarget(_dono.GetTree(), _targetGroup, corpo, _contexto.Team, alvo =>
        {
            var ate = alvo.GlobalPosition - origem;
            if (ate.LengthSquared() > raioQuadrado)
                return true;

            var direcao = ate.LengthSquared() > 0.001f
                ? new Vector3(ate.X, 0.3f, ate.Z).Normalized()
                : Vector3.Up;

            var critico = criticoSorteado || !(alvo.Context?.Movement?.IsGrounded ?? true);
            var dano = CritMath.Aplicar(danoBase, critico, _contexto.Stats?.Get(StatId.CritMultiplier) ?? 1f);

            // `!`: ForEachValidTarget só chama este callback para alvos com
            // Health vivo -- é a própria checagem que filtra o candidato.
            alvo.Context!.Health!.ApplyDamage(new DamageInfo(
                Amount: dano,
                Type: DamageType.Physical,
                HitPoint: alvo.GlobalPosition,
                Direction: direcao,
                Knockback: _arma.DiveKnockback,
                SourceId: corpo.GetInstanceId(),
                SourceTag: _arma.Id,
                IsCritical: critico));

            alvo.Context.Movement?.ApplyKnockback(direcao * _arma.DiveKnockback);

            var hitstop = _arma.DiveHitstopSeconds + (critico ? _arma.DiveCriticalHitstopBonus : 0f);
            _contexto.Health?.ApplyHitstop(hitstop);
            alvo.Context.Health!.ApplyHitstop(hitstop);

            return true;
        });
    }
}
