using System.Collections.Generic;
using Contenda.Core;
using Godot;

namespace Contenda.Vfx;

/// <summary>
/// O anel no chão que avisa "um inimigo vai nascer aqui", pooled.
/// </summary>
/// <remarks>
/// Autoload: ouve <see cref="GameEvents.SpawnMarkerRequested"/> sozinho,
/// mesmo desenho de <see cref="DamageNumberPool"/> -- nenhum sistema de onda
/// segura uma referência a este pool, é o barramento quem entrega o pedido,
/// spec 01 §5.
///
/// Pooled pelo mesmo motivo de <see cref="DamageNumberPool"/>: um conjunto
/// fixo de <see cref="MeshInstance3D"/> criado uma vez no boot, reciclado em
/// round-robin -- nunca instancia nem destrói um nó por spawn. Ver
/// docs/specs/10-modos-de-jogo-horde.md §6.
///
/// <c>_PhysicsProcess</c>, não <c>_Process</c>, mesmo motivo de
/// <c>DamageNumberPool</c>: puramente visual, mas <c>_Process</c> não roda de
/// forma confiável em modo headless, e este pool precisa ser verificável por
/// probe.
/// </remarks>
public sealed partial class SpawnMarkerPool : Node
{
    /// <summary>Quantos marcadores podem estar na tela ao mesmo tempo.</summary>
    [Export(PropertyHint.Range, "1,16,1")] public int PoolSize { get; set; } = 6;

    /// <summary>Raio do anel, no tamanho cheio, em metros.</summary>
    [Export(PropertyHint.Range, "0.2,3,0.1")] public float Radius { get; set; } = 1f;

    /// <summary>Cor do anel.</summary>
    [Export] public Color MarkerColor { get; set; } = new(1f, 0.7f, 0.1f);

    /// <summary>Altura acima do chão onde o anel fica, para não brigar com o z-fighting.</summary>
    [Export(PropertyHint.Range, "0,0.5,0.01")] public float GroundOffset { get; set; } = 0.03f;

    private readonly List<MeshInstance3D> _aneis = [];
    private readonly List<StandardMaterial3D> _materiais = [];
    private readonly List<Vector3> _origens = [];
    private readonly List<float> _decorridos = [];
    private readonly List<float> _duracoes = [];
    private readonly List<bool> _ativos = [];
    private int _proximoIndiceDeSobrescrita;

    /// <summary>Quantos marcadores estão visíveis agora. Para o probe/depuração.</summary>
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
        var malha = new CylinderMesh
        {
            TopRadius = Radius,
            BottomRadius = Radius,
            Height = 0.02f,
        };

        for (var i = 0; i < PoolSize; i++)
        {
            var material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = MarkerColor,
                EmissionEnabled = true,
                Emission = MarkerColor,
                EmissionEnergyMultiplier = 1.5f,
            };

            var anel = new MeshInstance3D
            {
                Mesh = malha,
                MaterialOverride = material,
                Visible = false,
            };

            AddChild(anel);
            _aneis.Add(anel);
            _materiais.Add(material);
            _origens.Add(Vector3.Zero);
            _decorridos.Add(0f);
            _duracoes.Add(0f);
            _ativos.Add(false);
        }

        ServiceLocator.Events.SpawnMarkerRequested += AoPedirMarcador;
        GameLog.Debug($"[boot] SpawnMarkerPool pronto ({PoolSize} marcadores)");
    }

    public override void _ExitTree()
    {
        ServiceLocator.Events.SpawnMarkerRequested -= AoPedirMarcador;
    }

    public override void _PhysicsProcess(double delta)
    {
        for (var i = 0; i < _aneis.Count; i++)
        {
            if (!_ativos[i])
                continue;

            _decorridos[i] += (float)delta;

            var ativo = SpawnMarkerMath.Evaluate(_decorridos[i], _duracoes[i], out var escala, out var alfa);
            if (!ativo)
            {
                _ativos[i] = false;
                _aneis[i].Visible = false;
                continue;
            }

            _aneis[i].Scale = new Vector3(escala, 1f, escala);

            var cor = _materiais[i].AlbedoColor;
            _materiais[i].AlbedoColor = new Color(cor.R, cor.G, cor.B, alfa);
        }
    }

    private void AoPedirMarcador(SpawnMarkerEvent evento)
    {
        var indice = _ativos.IndexOf(false);
        if (indice < 0)
        {
            // Todos ocupados: sobrescreve em round-robin -- um marcador extra
            // numa onda cheia importa menos que um spawn sem aviso nenhum.
            indice = _proximoIndiceDeSobrescrita;
            _proximoIndiceDeSobrescrita = (_proximoIndiceDeSobrescrita + 1) % _aneis.Count;
        }

        var origem = evento.Position + new Vector3(0f, GroundOffset, 0f);
        var anel = _aneis[indice];

        anel.GlobalPosition = origem;
        anel.Scale = Vector3.One * 0.3f;
        anel.Visible = true;

        _materiais[indice].AlbedoColor = MarkerColor;

        _origens[indice] = origem;
        _decorridos[indice] = 0f;
        _duracoes[indice] = evento.Duration;
        _ativos[indice] = true;
    }
}
