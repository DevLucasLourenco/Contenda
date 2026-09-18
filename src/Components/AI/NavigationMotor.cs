using Contenda.Characters.Base;
using Godot;

namespace Contenda.Components.AI;

/// <summary>Traduz um ponto do mundo num caminho até ele, em direção de movimento.</summary>
/// <remarks>
/// Ver docs/specs/09-inimigos-e-ia.md §4. **Não move o corpo.** Só devolve a
/// direção do próximo passo do caminho — quem realmente anda é o mesmo
/// <c>MovementComponent</c> do jogador, através do <c>IntentFrame.Move</c>
/// que o <c>EnemyBrain</c> monta a partir daqui. Um único caminho de
/// locomoção no projeto inteiro, exatamente como a spec exige.
///
/// Reaproveita a malha de navegação já existente da arena
/// (<c>data/navmesh/arena_nav.tres</c>, cozida por <c>NavmeshBaker</c>) —
/// um <see cref="NavigationAgent3D"/> encontra sozinho a
/// <see cref="NavigationRegion3D"/> que o envolve, sem precisar de
/// referência explícita nenhuma.
/// </remarks>
public sealed partial class NavigationMotor : Node, ICharacterComponent
{
    /// <summary>
    /// O <see cref="NavigationAgent3D"/> que faz o pathing de verdade.
    /// </summary>
    /// <remarks>
    /// Único `NodePath` que sobra aqui: <see cref="NavigationAgent3D"/> é um
    /// nó nativo da engine, nunca vai implementar <c>ICharacterComponent</c>
    /// nem passar pelo <c>CharacterContext</c> -- quem alcança ESTE Node por
    /// caminho é quem já é dono direto dele. Quem quiser alcançar o
    /// `NavigationMotor` em si, de fora, usa `CharacterContext.NavigationMotor`.
    /// </remarks>
    [Export] public NodePath AgentPath { get; set; } = new();

    /// <summary>Distância do alvo do caminho para considerar "chegou". Ver spec 09 §4.</summary>
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float StoppingDistance { get; set; } = 0.4f;

    /// <summary>Intervalo mínimo entre recálculos de rota, em segundos. Ver spec 09 §4.</summary>
    [Export(PropertyHint.Range, "0.05,2,0.05")] public float RepathInterval { get; set; } = 0.25f;

    /// <summary>
    /// Quanto o alvo precisa ter andado, desde o último recálculo de verdade,
    /// para forçar um novo antes do intervalo vencer. Ver spec 09 §4.
    /// </summary>
    [Export(PropertyHint.Range, "0.1,10,0.1")] public float RepathDistance { get; set; } = 1.5f;

    /// <summary>
    /// A partir de qual subida no próximo ponto do caminho vale pedir um
    /// pulo, em vez de só andar. Ver <see cref="PrecisaPular"/>.
    /// </summary>
    [Export(PropertyHint.Range, "0.1,3,0.05")] public float LimiarDeSalto { get; set; } = 0.5f;

    private NavigationAgent3D? _agente;

    /// <remarks>
    /// Ver <see cref="NavigationTimingMath"/>: repath só quando o intervalo
    /// vencer OU o alvo tiver andado <see cref="RepathDistance"/> desde o
    /// último recálculo de verdade -- nunca a cada quadro, mesmo que
    /// <see cref="SetTarget"/> seja chamado a cada quadro (é).
    ///
    /// <c>_relogio</c> nasce numa fase ALEATÓRIA (não em zero) dentro de
    /// <see cref="RepathInterval"/> -- é o escalonamento em si: com 40
    /// inimigos, todos com relógio zerado, todos cruzariam o intervalo no
    /// MESMO quadro; uma fase aleatória por instância espalha os primeiros
    /// recálculos ao longo do próprio intervalo.
    /// </remarks>
    private float _relogio;
    private Vector3 _ultimoAlvoRepathado;
    private Vector3 _ultimoAlvoBruto;
    private bool _temRepathAnterior;

    /// <remarks>
    /// Vazio de propósito: quem me registra em <c>CharacterContext.NavigationMotor</c>
    /// é o <c>Registrar</c> do <c>CharacterController</c>, na MESMA passada
    /// que já resolve <c>Health</c>/<c>Movement</c>/etc -- antes de QUALQUER
    /// <c>Bind</c> rodar. Fazer isso aqui dentro, no próprio <c>Bind</c>,
    /// deixaria a ordem entre os dois `Bind` (deste nó e do que precisa
    /// enxergá-lo) importar, e a doc do container promete que a primeira
    /// passada não tem ordem garantida.
    /// </remarks>
    public void Bind(CharacterContext contexto)
    {
    }

    public void Configure(CharacterDefinition definicao)
    {
    }

    /// <summary>
    /// Rearma o relógio de repath com uma nova fase aleatória -- um inimigo
    /// reciclado (ticket 25) que renascesse com o relógio zerado voltaria a
    /// sincronizar com qualquer outro que também tivesse acabado de nascer no
    /// mesmo quadro (um respawn em lote de uma onda inteira, por exemplo),
    /// exatamente o "todos recalculando no mesmo quadro" que o escalonamento
    /// existe para evitar.
    /// </summary>
    public void ResetForSpawn()
    {
        _relogio = (float)GD.RandRange(0f, RepathInterval);
        _temRepathAnterior = false;
    }

    public override void _Ready()
    {
        _agente = GetNodeOrNull<NavigationAgent3D>(AgentPath);
        if (_agente is null)
        {
            GD.PushError($"{Name}: AgentPath não resolveu para um NavigationAgent3D.");
            return;
        }

        _agente.TargetDesiredDistance = StoppingDistance;

        // Mesma fase aleatória de ResetForSpawn -- cobre quem nunca passa
        // pelo pool (o probe/debug que instancia a cena direto).
        _relogio = (float)GD.RandRange(0f, RepathInterval);
    }

    /// <summary>
    /// Onde o agente deveria estar tentando chegar -- mas só recalcula a
    /// rota de verdade quando <see cref="NavigationTimingMath.ShouldRepath"/>
    /// manda, mesmo chamado a cada quadro (é). Ver spec 09 §4.
    /// </summary>
    public void SetTarget(Vector3 posicaoNoMundo, float delta)
    {
        _relogio += delta;
        _ultimoAlvoBruto = posicaoNoMundo;

        if (_agente is null)
            return;

        var distanciaDoUltimoRepath = _temRepathAnterior
            ? posicaoNoMundo.DistanceTo(_ultimoAlvoRepathado)
            : float.PositiveInfinity; // primeiro pedido desta vida: sempre repatha

        if (!NavigationTimingMath.ShouldRepath(_relogio, distanciaDoUltimoRepath, RepathInterval, RepathDistance))
            return;

        _agente.TargetPosition = posicaoNoMundo;
        _ultimoAlvoRepathado = posicaoNoMundo;
        _temRepathAnterior = true;
        _relogio = 0f;
    }

    /// <summary>
    /// Para onde andar AGORA para seguir o caminho até o alvo mais recente.
    /// </summary>
    /// <remarks>
    /// Achatada no plano: o agente pensa em 3D (rampas, plataformas), mas
    /// quem consome isto é <c>IntentFrame.Move</c>, que é puramente
    /// horizontal -- a altura já é resolvida à parte, pela gravidade do
    /// <c>MovementComponent</c>.
    ///
    /// Sem caminho válido (<see cref="NavigationAgent3D.IsTargetReachable"/>
    /// falso -- um alvo fora da malha, ou temporariamente isolado por uma
    /// geometria em obra), anda em linha reta na direção CRUA do alvo em vez
    /// de congelar: "um inimigo sem caminho válido não trava, avança na
    /// direção do jogador e tenta de novo" (ticket 23). O próprio
    /// <see cref="SetTarget"/> já vai tentar um repath de verdade de novo
    /// assim que o intervalo vencer -- não é preciso pedir de novo aqui.
    /// </remarks>
    public Vector3 GetDesiredDirection(Vector3 posicaoAtual)
    {
        if (_agente is null)
            return Vector3.Zero;

        if (_agente.IsNavigationFinished())
            return Vector3.Zero;

        if (!_agente.IsTargetReachable())
            return DirecaoCruaAoAlvo(posicaoAtual);

        var proximoPonto = _agente.GetNextPathPosition();
        var direcao = proximoPonto - posicaoAtual;
        direcao.Y = 0f;

        return direcao.LengthSquared() > 0.0001f ? direcao.Normalized() : Vector3.Zero;
    }

    /// <summary>
    /// Se o próximo trecho do caminho sobe alto demais para o desnível normal
    /// de rampa/degrau chegar sozinho (o próprio `MoveAndSlide`/colagem ao
    /// chão do <c>MovementComponent</c> já resolve isso) -- uma ligação de
    /// navegação (`NavigationLink3D`) pulando de rua para cima de um objeto
    /// escalável, por exemplo. Ver ticket 23, spec 09 §4/§5: "inimigos sobem
    /// nos objetos escaláveis pelas ligações de navegação, sem travar".
    /// </summary>
    /// <remarks>
    /// O caminho geométrico já existe (o agente encontra o `NavigationLink3D`
    /// sozinho); o que faltava é o CORPO saber que precisa de um pulo de
    /// verdade para acompanhar -- sem isto, o inimigo anda até a base do
    /// objeto e para ali, esbarrando na parede vertical dele, achando que
    /// "travou" (o sintoma que este ticket existe para eliminar).
    /// </remarks>
    public bool PrecisaPular(Vector3 posicaoAtual)
    {
        if (_agente is null || _agente.IsNavigationFinished())
            return false;

        return _agente.GetNextPathPosition().Y - posicaoAtual.Y > LimiarDeSalto;
    }

    private Vector3 DirecaoCruaAoAlvo(Vector3 posicaoAtual)
    {
        var direcao = _ultimoAlvoBruto - posicaoAtual;
        direcao.Y = 0f;

        return direcao.LengthSquared() > 0.0001f ? direcao.Normalized() : Vector3.Zero;
    }
}
