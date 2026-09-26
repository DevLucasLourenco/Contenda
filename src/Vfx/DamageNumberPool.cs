using System.Collections.Generic;
using Contenda.Core;
using Godot;

namespace Contenda.Vfx;

/// <summary>
/// Números de dano flutuantes, pooled — sobem e somem sobre quem apanhou.
/// </summary>
/// <remarks>
/// Autoload: ouve <see cref="GameEvents.DamageNumberRequested"/> sozinho, sem
/// que ninguém precise segurar uma referência a este pool. Nenhuma arma, nenhum
/// personagem sabe que ele existe — é o barramento quem entrega o pedido, spec
/// 01 §5.
///
/// **Pooled de propósito, ver ticket 11**: um conjunto fixo de <c>Label3D</c>
/// criado uma vez no boot e reciclado em round-robin, preferindo um slot
/// ocioso quando existe. Nunca instancia nem destrói um nó por golpe — com
/// horda de 40 inimigos no M5, isso seria alocação por quadro na pior hora.
///
/// Tem <c>_PhysicsProcess</c> próprio, apesar de ser puramente visual — o que
/// as convenções §4 mandam para <c>_Process</c>. Duas razões: (1) a regra de
/// "nenhum <c>_PhysicsProcess</c> por componente" (spec 01 §6) vale para
/// componentes de PERSONAGEM orquestrados pelo <c>CharacterController</c> —
/// isto é um sistema independente, um autoload, fora dessa cadeia (o
/// <c>CameraRig</c>, esse sim, usa <c>_Process</c>, por ser apresentação de
/// verdade). (2) <c>_Process</c> não roda de forma confiável em modo
/// <c>--headless</c> — mesmo motivo pelo qual <c>HealthBar</c>/<c>ManaBar</c>
/// migraram para <c>_PhysicsProcess</c> no ticket 12. Como este pool também
/// precisa ser verificável pela <c>ImpactProbe</c> headless, o mesmo desvio
/// deliberado se aplica aqui.
/// </remarks>
public sealed partial class DamageNumberPool : Node
{
    /// <summary>Quantos números podem estar na tela ao mesmo tempo.</summary>
    [Export(PropertyHint.Range, "4,64,1")] public int PoolSize { get; set; } = 16;

    /// <summary>Quanto tempo um número vive, em segundos.</summary>
    [Export(PropertyHint.Range, "0.1,2,0.05")] public float Lifetime { get; set; } = 0.6f;

    /// <summary>Quanto o número sobe no total, em metros.</summary>
    [Export(PropertyHint.Range, "0.1,5,0.1")] public float RiseHeight { get; set; } = 1f;

    /// <summary>Altura acima do ponto de acerto onde o número nasce, em metros.</summary>
    [Export(PropertyHint.Range, "0,3,0.1")] public float SpawnHeightOffset { get; set; } = 1.6f;

    /// <summary>Tamanho da fonte.</summary>
    [Export(PropertyHint.Range, "8,64,1")] public int FontSize { get; set; } = 32;

    /// <summary>Cor de um número comum.</summary>
    [Export] public Color NormalColor { get; set; } = Colors.White;

    /// <summary>Cor de um número crítico.</summary>
    [Export] public Color CriticalColor { get; set; } = Colors.Gold;

    /// <summary>
    /// Quanto um número crítico cresce em relação ao normal. Spec 16 §5: 1,6×.
    /// </summary>
    [Export(PropertyHint.Range, "1,3,0.05")] public float CriticalScale { get; set; } = 1.6f;

    /// <summary>
    /// Espessura do contorno de um número crítico, em pixels. Zero desliga o
    /// contorno -- é o "brilho" que separa o crítico do normal além do
    /// tamanho e da cor, spec 16 §5.
    /// </summary>
    [Export(PropertyHint.Range, "0,16,1")] public int CriticalOutlineSize { get; set; } = 8;

    /// <summary>Cor do contorno de um número crítico.</summary>
    [Export] public Color CriticalOutlineColor { get; set; } = Colors.White;

    private readonly List<Label3D> _rotulos = [];
    private readonly List<Vector3> _origens = [];
    private readonly List<float> _decorridos = [];
    private readonly List<bool> _ativos = [];
    private int _proximoIndiceDeSobrescrita;

    /// <summary>Escala aplicada ao último número mostrado. Para o probe/depuração.</summary>
    public float LastScale { get; private set; } = 1f;

    /// <summary>Espessura de contorno do último número mostrado. Para o probe/depuração.</summary>
    public int LastOutlineSize { get; private set; }

    /// <summary>Quantos números estão visíveis agora. Para o probe/depuração.</summary>
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
        for (var i = 0; i < PoolSize; i++)
        {
            var rotulo = new Label3D
            {
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                FontSize = FontSize,
                Visible = false,
            };

            AddChild(rotulo);
            _rotulos.Add(rotulo);
            _origens.Add(Vector3.Zero);
            _decorridos.Add(0f);
            _ativos.Add(false);
        }

        ServiceLocator.Events.DamageNumberRequested += AoPedirNumero;
        GD.Print($"[boot] DamageNumberPool pronto ({PoolSize} números)");
    }

    public override void _ExitTree()
    {
        ServiceLocator.Events.DamageNumberRequested -= AoPedirNumero;
    }

    public override void _PhysicsProcess(double delta)
    {
        for (var i = 0; i < _rotulos.Count; i++)
        {
            if (!_ativos[i])
                continue;

            _decorridos[i] += (float)delta;

            var ativo = FloatingDamageMath.Evaluate(_decorridos[i], Lifetime, RiseHeight, out var altura, out var alfa);
            if (!ativo)
            {
                _ativos[i] = false;
                _rotulos[i].Visible = false;
                continue;
            }

            _rotulos[i].GlobalPosition = _origens[i] + new Vector3(0f, altura, 0f);

            var cor = _rotulos[i].Modulate;
            _rotulos[i].Modulate = new Color(cor.R, cor.G, cor.B, alfa);
        }
    }

    private void AoPedirNumero(DamageNumberEvent evento)
    {
        // Opção do jogador (ticket 32): "mostrar números de dano".
        if (!ServiceLocator.Session.Settings.Gameplay.ShowDamageNumbers)
            return;

        var indice = _ativos.IndexOf(false);
        if (indice < 0)
        {
            // Todos ocupados: sobrescreve em round-robin em vez de descartar o
            // pedido -- um número extra num quadro cheio importa menos que um
            // acerto que nunca aparece.
            indice = _proximoIndiceDeSobrescrita;
            _proximoIndiceDeSobrescrita = (_proximoIndiceDeSobrescrita + 1) % _rotulos.Count;
        }

        var origem = evento.Position + new Vector3(0f, SpawnHeightOffset, 0f);
        var rotulo = _rotulos[indice];

        var escala = evento.IsCritical ? CriticalScale : 1f;
        var contorno = evento.IsCritical ? CriticalOutlineSize : 0;

        rotulo.Text = Mathf.RoundToInt(evento.Amount).ToString();
        rotulo.Modulate = evento.IsCritical ? CriticalColor : NormalColor;
        rotulo.Scale = Vector3.One * escala;
        rotulo.OutlineSize = contorno;
        rotulo.OutlineModulate = CriticalOutlineColor;
        rotulo.GlobalPosition = origem;
        rotulo.Visible = true;

        _origens[indice] = origem;
        _decorridos[indice] = 0f;
        _ativos[indice] = true;

        LastScale = escala;
        LastOutlineSize = contorno;
    }
}
