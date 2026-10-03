using Godot;

namespace Contenda.Vfx;

internal enum CombatVfxPattern : byte
{
    Sweep,
    Flash,
    Tracer,
    Needle,
    Ring,
    Arc,
    Cross,
    Burst,
    Explosion,
    Death,
}

/// <summary>Uma forma mesh simples, reutilizada pelo pool de VFX.</summary>
internal sealed partial class CombatVfxEffect : Node3D
{
    private readonly MeshInstance3D _primary = new();
    private MeshInstance3D? _secondary;
    private readonly StandardMaterial3D _material = CriarMaterial(Colors.White);
    private StandardMaterial3D? _secondaryMaterial;
    private CombatVfxPattern _pattern;
    private Color _tint;
    private float _duration;
    private float _elapsed;
    private float _size;
    private float _length;
    private bool _playing;

    public bool IsAvailable => !_playing;

    public void Initialize(CombatVfxPattern pattern, float duration)
    {
        _pattern = pattern;
        _duration = duration;
        ProcessMode = ProcessModeEnum.Always;
        SetProcess(false);

        Mesh mesh = pattern switch
        {
            CombatVfxPattern.Sweep => new BoxMesh { Size = new Vector3(2.2f, 0.12f, 0.08f) },
            CombatVfxPattern.Tracer => new BoxMesh { Size = Vector3.One },
            CombatVfxPattern.Needle => new BoxMesh { Size = new Vector3(0.06f, 0.06f, 1f) },
            CombatVfxPattern.Cross => new BoxMesh { Size = new Vector3(1.3f, 0.1f, 0.08f) },
            CombatVfxPattern.Ring => new TorusMesh { InnerRadius = 0.80f, OuterRadius = 1f, RingSegments = 20, Rings = 8 },
            CombatVfxPattern.Arc => new TorusMesh { InnerRadius = 0.74f, OuterRadius = 1f, RingSegments = 20, Rings = 8 },
            CombatVfxPattern.Flash => new SphereMesh { Radius = 0.24f, Height = 0.48f, RadialSegments = 10, Rings = 6 },
            _ => new SphereMesh { Radius = 0.34f, Height = 0.68f, RadialSegments = 10, Rings = 6 },
        };

        _primary.Mesh = mesh;
        _primary.MaterialOverride = _material;
        AddChild(_primary);

        if (pattern is CombatVfxPattern.Burst or CombatVfxPattern.Explosion or CombatVfxPattern.Death or CombatVfxPattern.Cross)
        {
            _secondaryMaterial = CriarMaterial(Colors.White);
            _secondary = new MeshInstance3D
            {
                Mesh = pattern == CombatVfxPattern.Cross
                    ? new BoxMesh { Size = new Vector3(1.3f, 0.1f, 0.08f) }
                    : new TorusMesh { InnerRadius = 0.76f, OuterRadius = 1f, RingSegments = 20, Rings = 8 },
                Rotation = pattern == CombatVfxPattern.Cross ? new Vector3(0f, 0f, Mathf.Pi / 2f) : Vector3.Zero,
                MaterialOverride = _secondaryMaterial,
            };
            AddChild(_secondary);
        }

        Visible = false;
    }

    public void Play(Vector3 position, Vector3 direction, float size, Color tint, float length = 1f)
    {
        _tint = tint;
        _size = Mathf.Max(0.05f, size);
        _length = Mathf.Max(0.05f, length);
        _elapsed = 0f;
        _playing = true;
        SetProcess(true);
        GlobalPosition = position;
        if (direction.LengthSquared() > 0.001f)
        {
            var forward = direction.Normalized();
            var up = Mathf.Abs(forward.Dot(Vector3.Up)) > 0.98f ? Vector3.Forward : Vector3.Up;
            GlobalBasis = Basis.LookingAt(forward, up);
        }

        _primary.Scale = Vector3.One;
        _primary.Position = Vector3.Zero;
        if (_secondary is not null)
        {
            _secondary.Scale = Vector3.One;
            _secondary.Position = Vector3.Zero;
        }

        Visible = true;
        AtualizarAparencia(0f);
    }

    public override void _Process(double delta)
    {
        if (!_playing)
            return;

        _elapsed += (float)delta;
        var progress = Mathf.Clamp(_elapsed / _duration, 0f, 1f);
        AtualizarAparencia(progress);
        if (progress < 1f)
            return;

        _playing = false;
        Visible = false;
        SetProcess(false);
    }

    private void AtualizarAparencia(float progress)
    {
        var fade = 1f - progress;
        AplicarCor(_material, fade);
        if (_secondaryMaterial is not null)
            AplicarCor(_secondaryMaterial, fade * 0.8f);

        switch (_pattern)
        {
            case CombatVfxPattern.Sweep:
                _primary.Scale = new Vector3(_size * (0.85f + (progress * 0.25f)), _size, _size);
                break;
            case CombatVfxPattern.Tracer:
                _primary.Scale = new Vector3(0.025f * _size, 0.025f * _size, _length);
                break;
            case CombatVfxPattern.Ring:
                _primary.Scale = Vector3.One * _size * (0.25f + progress * 1.2f);
                break;
            case CombatVfxPattern.Arc:
                _primary.Scale = Vector3.One * _size * (1.1f - progress * 0.35f);
                _primary.Rotation = new Vector3(0f, progress * Mathf.Pi * 2f, 0f);
                break;
            case CombatVfxPattern.Flash:
                _primary.Scale = Vector3.One * _size * (1.15f - progress * 0.9f);
                break;
            case CombatVfxPattern.Death:
                _primary.Scale = Vector3.One * _size * (0.3f + progress * 1.1f);
                if (_secondary is not null)
                    _secondary.Scale = Vector3.One * _size * (0.2f + progress * 1.5f);
                break;
            default:
                _primary.Scale = Vector3.One * _size * (0.25f + progress * 1.1f);
                if (_secondary is not null)
                    _secondary.Scale = Vector3.One * _size * (0.15f + progress * 1.4f);
                break;
        }
    }

    private void AplicarCor(StandardMaterial3D material, float alpha)
    {
        material.AlbedoColor = new Color(_tint.R, _tint.G, _tint.B, _tint.A * alpha);
        material.Emission = new Color(_tint.R, _tint.G, _tint.B, _tint.A * alpha);
    }

    private static StandardMaterial3D CriarMaterial(Color color) => new()
    {
        AlbedoColor = color,
        EmissionEnabled = true,
        Emission = color,
        EmissionEnergyMultiplier = 2.2f,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
    };
}
