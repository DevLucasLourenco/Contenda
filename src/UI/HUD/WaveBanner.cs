using Contenda.Core;
using Godot;

namespace Contenda.UI.HUD;

/// <summary>O anúncio de onda ("ONDA 3") que aparece no meio da tela e some sozinho.</summary>
/// <remarks>
/// Ouve <see cref="GameEvents.WaveAnnounced"/> sozinho, mesmo desenho de
/// <see cref="BossHealthBar"/>/<see cref="Contenda.Vfx.DamageNumberPool"/> --
/// nenhum sistema de onda segura uma referência a este widget, spec 01 §5.
/// "Aparece e some sozinho" (ticket 27) é literal: quem dispara o evento
/// não decide quando ele some, só quando ele aparece.
///
/// <c>_PhysicsProcess</c>, não <c>_Process</c>, mesmo motivo de
/// <see cref="HealthBar"/>: precisa ser verificável por probe headless.
/// </remarks>
public sealed partial class WaveBanner : Control
{
    /// <summary>O texto do anúncio.</summary>
    [Export] public NodePath LabelPath { get; set; } = new();

    /// <summary>Quanto tempo o banner fica totalmente visível antes de começar a sumir, em segundos.</summary>
    [Export(PropertyHint.Range, "0.1,5,0.1")] public float VisibleDuration { get; set; } = 1.5f;

    /// <summary>Quanto tempo o desaparecimento em si leva, em segundos.</summary>
    [Export(PropertyHint.Range, "0.1,3,0.1")] public float FadeDuration { get; set; } = 0.6f;

    private Label? _rotulo;
    private float _decorrido;
    private bool _mostrando;

    /// <summary>Se o banner está visível (mesmo que já sumindo) agora. Para o probe/depuração.</summary>
    public bool IsShowing => _mostrando;

    public override void _Ready()
    {
        _rotulo = GetNodeOrNull<Label>(LabelPath);
        if (_rotulo is null)
        {
            GD.PushError($"{Name}: LabelPath não resolveu.");
            SetPhysicsProcess(false);
            return;
        }

        Modulate = new Color(1f, 1f, 1f, 0f);
        Visible = false;

        ServiceLocator.Events.WaveAnnounced += AoAnunciarOnda;
    }

    public override void _ExitTree()
    {
        ServiceLocator.Events.WaveAnnounced -= AoAnunciarOnda;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_mostrando)
            return;

        _decorrido += (float)delta;

        var total = VisibleDuration + FadeDuration;
        if (_decorrido >= total)
        {
            _mostrando = false;
            Visible = false;
            return;
        }

        var alfa = _decorrido < VisibleDuration
            ? 1f
            : 1f - ((_decorrido - VisibleDuration) / FadeDuration);

        Modulate = new Color(1f, 1f, 1f, alfa);
    }

    private void AoAnunciarOnda(WaveAnnouncedEvent evento)
    {
        if (_rotulo is null)
            return;

        _rotulo.Text = evento.DisplayName;
        _decorrido = 0f;
        _mostrando = true;
        Visible = true;
        Modulate = new Color(1f, 1f, 1f, 1f);
    }
}
