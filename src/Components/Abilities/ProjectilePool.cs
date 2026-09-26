using System.Collections.Generic;
using Contenda.Characters.Base;
using Contenda.Components.Health;
using Contenda.Core;
using Contenda.Weapons;
using Godot;

namespace Contenda.Components.Abilities;

/// <summary>
/// Projéteis de habilidade, pooled — voam, explodem numa área e voltam ao pool.
/// </summary>
/// <remarks>
/// Autoload: ouve <see cref="GameEvents.ProjectileFireRequested"/> sozinho,
/// mesmo desenho do <c>DamageNumberPool</c> do ticket 11 — nenhum
/// <see cref="ProjectileBehavior"/> segura uma referência a este pool. Um
/// conjunto fixo de slots é criado uma vez no boot e reciclado em round-robin;
/// nunca instancia nem destrói um nó por disparo — com Explosive Shot em
/// recarga de 6 s isso nunca seria o gargalo, mas o padrão do projeto (ticket
/// 11) é não abrir exceção por "esta arma é rara".
///
/// Sem colisão contra o mundo (paredes) ainda: um projétil que não encontra
/// alvo voa até <c>LifeTime</c> (derivado de <c>Range/Speed</c> pelo
/// <see cref="ProjectileBehavior"/>) e some sozinho no ar. A arena de hoje é
/// aberta o bastante para isso não incomodar; paredes internas de verdade só
/// chegam com o ticket 21.
/// </remarks>
public sealed partial class ProjectilePool : Node
{
    /// <summary>Quantos projéteis podem estar voando ao mesmo tempo.</summary>
    [Export(PropertyHint.Range, "2,32,1")] public int PoolSize { get; set; } = 8;

    /// <summary>Raio do visual provisório de cada projétil, em metros.</summary>
    [Export(PropertyHint.Range, "0.05,1,0.05")] public float VisualRadius { get; set; } = 0.2f;

    private readonly List<MeshInstance3D> _visuais = [];
    private readonly List<MeshInstance3D> _visuaisDeExplosao = [];
    private readonly List<float> _explosoesRestantes = [];
    private readonly List<Vector3> _posicoes = [];
    private readonly List<Vector3> _direcoes = [];
    private readonly List<float> _velocidades = [];
    private readonly List<float> _restantes = [];
    private readonly List<float> _danos = [];
    private readonly List<float> _raiosDeExplosao = [];
    private readonly List<float> _raiosDeImpacto = [];
    private readonly List<float> _alcancesVerticais = [];
    private readonly List<int> _maxAlvos = [];
    private readonly List<float> _repulsoes = [];
    private readonly List<ulong> _fontesId = [];
    private readonly List<string> _fontesTag = [];
    private readonly List<Team> _times = [];
    private readonly List<bool> _criticos = [];
    private readonly List<float> _multiplicadoresNaBorda = [];

    // Um alvo amostrado UMA VEZ, no disparo -- não a cada quadro. Cada slot
    // reutiliza sempre a MESMA List<T> (limpa e recarregada no disparo
    // seguinte), então o pool inteiro nunca aloca por quadro, só na hora de
    // amostrar. Mesma escolha do MeleeWeapon.AmostrarAlvos: um projétil atinge
    // quem estava no grupo quando partiu, não quem entrou depois -- convenções
    // §5 proíbem GetNodesInGroup dentro de um laço por quadro.
    private readonly List<List<CharacterController>> _alvosEmCache = [];

    private readonly List<bool> _ativos = [];
    private int _proximoIndiceDeSobrescrita;

    /// <summary>Quantos projéteis estão voando agora. Para o probe/depuração.</summary>
    public int ActiveCount
    {
        get
        {
            var contagem = 0;
            for (var i = 0; i < _ativos.Count; i++)
            {
                if (_ativos[i])
                    contagem++;
            }

            return contagem;
        }
    }

    public override void _Ready()
    {
        var contêinerDeExplosões = new Node3D { Name = "Explosões" };
        AddChild(contêinerDeExplosões, false, InternalMode.Back);

        for (var i = 0; i < PoolSize; i++)
        {
            var visual = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = VisualRadius, Height = VisualRadius * 2f },
                Visible = false,
            };

            AddChild(visual);
            _visuais.Add(visual);
            var visualDeExplosao = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 1f, Height = 2f },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.15f, 0.75f, 1f, 0.35f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                },
                Visible = false,
            };
            contêinerDeExplosões.AddChild(visualDeExplosao);
            _visuaisDeExplosao.Add(visualDeExplosao);
            _explosoesRestantes.Add(0f);
            _posicoes.Add(Vector3.Zero);
            _direcoes.Add(Vector3.Forward);
            _velocidades.Add(0f);
            _restantes.Add(0f);
            _danos.Add(0f);
            _raiosDeExplosao.Add(0f);
            _raiosDeImpacto.Add(0f);
            _alcancesVerticais.Add(0f);
            _maxAlvos.Add(0);
            _repulsoes.Add(0f);
            _fontesId.Add(0UL);
            _fontesTag.Add(string.Empty);
            _times.Add(Team.Neutral);
            _criticos.Add(false);
            _multiplicadoresNaBorda.Add(1f);
            _alvosEmCache.Add([]);
            _ativos.Add(false);
        }

        ServiceLocator.Events.ProjectileFireRequested += AoPedirDisparo;
        GD.Print($"[boot] ProjectilePool pronto ({PoolSize} projéteis)");
    }

    public override void _ExitTree()
    {
        ServiceLocator.Events.ProjectileFireRequested -= AoPedirDisparo;
    }

    public override void _PhysicsProcess(double delta)
    {
        for (var i = 0; i < _explosoesRestantes.Count; i++)
        {
            if (_explosoesRestantes[i] <= 0f)
                continue;

            _explosoesRestantes[i] -= (float)delta;
            if (_explosoesRestantes[i] <= 0f)
                _visuaisDeExplosao[i].Visible = false;
        }

        for (var i = 0; i < _ativos.Count; i++)
        {
            if (!_ativos[i])
                continue;

            // O último passo termina exatamente no ponto de mira/alcance.
            var passo = Mathf.Min((float)delta, _restantes[i]);
            _posicoes[i] += _direcoes[i] * _velocidades[i] * passo;
            _restantes[i] -= (float)delta;
            _visuais[i].GlobalPosition = _posicoes[i];

            if (_restantes[i] <= 0f || TocaAlgumAlvo(i))
                Explodir(i);
        }
    }

    /// <summary>Se algum alvo do cache já está dentro do raio de explosão agora.</summary>
    private bool TocaAlgumAlvo(int i)
    {
        var tocou = false;

        AbilityTargeting.ForEachValidTarget(_alvosEmCache[i], _times[i], alvo =>
        {
            var raio = _raiosDeImpacto[i] > 0f ? _raiosDeImpacto[i] : _raiosDeExplosao[i];
            var ate = alvo.GlobalPosition - _posicoes[i];
            if (_alcancesVerticais[i] > 0f)
            {
                var distanciaHorizontalQuadrada = (ate.X * ate.X) + (ate.Z * ate.Z);
                if (Mathf.Abs(ate.Y) > _alcancesVerticais[i] || distanciaHorizontalQuadrada > raio * raio)
                    return true;
            }
            else if (ate.LengthSquared() > raio * raio)
                return true;

            // Armas de projétil com raio de contato próprio detonam no centro
            // do alvo direto: ele recebe o dano inteiro; só os vizinhos sofrem falloff.
            if (_raiosDeImpacto[i] > 0f)
                _posicoes[i] = alvo.GlobalPosition;

            tocou = true;
            return false;
        });

        return tocou;
    }

    /// <summary>Aplica dano em área no ponto atual do projétil, e devolve o slot ao pool.</summary>
    private void Explodir(int i)
    {
        var raioQuadrado = _raiosDeExplosao[i] * _raiosDeExplosao[i];
        var atingidos = 0;

        AbilityTargeting.ForEachValidTarget(_alvosEmCache[i], _times[i], alvo =>
        {
            var ate = alvo.GlobalPosition - _posicoes[i];
            var distanciaQuadrada = _alcancesVerticais[i] > 0f
                ? (ate.X * ate.X) + (ate.Z * ate.Z)
                : ate.LengthSquared();
            if ((_alcancesVerticais[i] > 0f && Mathf.Abs(ate.Y) > _alcancesVerticais[i])
                || distanciaQuadrada > raioQuadrado)
                return true;

            var direcao = ate.LengthSquared() > 0.001f ? ate.Normalized() : Vector3.Up;

            // `!`: AbilityTargeting só chama este callback para alvos com
            // Health vivo -- é a própria checagem que filtra o candidato.
            alvo.Context!.Health!.ApplyDamage(new DamageInfo(
                Amount: _danos[i] * ExplosionMath.DamageMultiplier(
                    Mathf.Sqrt(distanciaQuadrada), _raiosDeExplosao[i], _multiplicadoresNaBorda[i]),
                Type: DamageType.Explosive,
                HitPoint: alvo.GlobalPosition,
                Direction: direcao,
                Knockback: _repulsoes[i],
                SourceId: _fontesId[i],
                SourceTag: _fontesTag[i],
                IsCritical: _criticos[i]));

            alvo.Context.Movement?.ApplyKnockback(direcao * _repulsoes[i]);
            atingidos++;

            return _maxAlvos[i] <= 0 || atingidos < _maxAlvos[i];
        });

        _alvosEmCache[i].Clear();
        _ativos[i] = false;
        _visuais[i].Visible = false;
        var raio = _raiosDeExplosao[i];
        _visuaisDeExplosao[i].GlobalPosition = _posicoes[i];
        _visuaisDeExplosao[i].Scale = Vector3.One * raio;
        _visuaisDeExplosao[i].Visible = true;
        _explosoesRestantes[i] = 0.12f;
    }

    private void AoPedirDisparo(ProjectileFireEvent evento)
    {
        var indice = _ativos.IndexOf(false);
        if (indice < 0)
        {
            // Todos voando: sobrescreve em round-robin em vez de descartar o
            // pedido -- mesma escolha do DamageNumberPool.
            indice = _proximoIndiceDeSobrescrita;
            _proximoIndiceDeSobrescrita = (_proximoIndiceDeSobrescrita + 1) % _ativos.Count;
        }

        _posicoes[indice] = evento.Origin;
        _direcoes[indice] = evento.Direction;
        _velocidades[indice] = evento.Speed;
        _restantes[indice] = evento.LifeTime;
        _danos[indice] = evento.Damage;
        _raiosDeExplosao[indice] = evento.ExplosionRadius;
        _raiosDeImpacto[indice] = evento.ImpactRadius;
        _alcancesVerticais[indice] = evento.VerticalReach;
        _maxAlvos[indice] = evento.MaxTargets;
        _repulsoes[indice] = evento.Knockback;
        _fontesId[indice] = evento.SourceId;
        _fontesTag[indice] = evento.SourceTag;
        _times[indice] = evento.ShooterTeam;
        _criticos[indice] = evento.IsCritical;
        _multiplicadoresNaBorda[indice] = evento.EdgeDamageMultiplier;
        _ativos[indice] = true;

        // Amostra o grupo AGORA, uma vez só -- ver o comentário de
        // _alvosEmCache. `null` para self: um projétil não é nenhum
        // personagem para se autoexcluir; o filtro de time já cuida de não
        // acertar quem disparou.
        var cache = _alvosEmCache[indice];
        cache.Clear();
        AbilityTargeting.ForEachValidTarget(GetTree(), evento.TargetGroup, self: null, evento.ShooterTeam, alvo =>
        {
            cache.Add(alvo);
            return true;
        });

        _visuais[indice].GlobalPosition = evento.Origin;
        _visuais[indice].Visible = true;
    }
}
