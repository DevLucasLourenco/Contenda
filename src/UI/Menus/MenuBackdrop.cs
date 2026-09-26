using Contenda.Camera;
using Contenda.Characters.Base;
using Contenda.GameModes.Horde;
using Godot;

namespace Contenda.UI.Menus;

/// <summary>
/// O fundo do menu: a própria arena, sem ninguém dentro, vista por uma câmera
/// em órbita lenta. Spec 11 §3.1, ticket 30.
/// </summary>
/// <remarks>
/// Reusa <c>Arena.tscn</c> (geometria, luz, céu -- "mostra o jogo antes de o
/// jogador entrar nele e reusa assets que já existem") e tira dela, ANTES de
/// entrar na árvore, tudo que é vida de partida: o jogador, os bonecos de
/// treino, o rig de câmera e os diretores de onda. Um nó removido antes do
/// `AddChild` nunca roda `_Ready`, então nada se anuncia ao `GameSession` e o
/// HUD não encontra jogador nenhum para mostrar. Por TIPO, não por nome de nó:
/// renomear `Jogador` na arena não quebra o fundo.
/// </remarks>
public sealed partial class MenuBackdrop : Node3D
{
    [Export] public PackedScene? ArenaScene { get; set; }

    /// <summary>Tempo de uma volta completa, em segundos.</summary>
    [Export(PropertyHint.Range, "20,600,5")] public float OrbitSeconds { get; set; } = 150f;

    /// <summary>Distância horizontal da câmera ao centro da praça, em metros. Fora dos prédios (~23 m).</summary>
    [Export(PropertyHint.Range, "10,80,1")] public float Radius { get; set; } = 30f;

    /// <summary>Altura da câmera, em metros -- acima dos prédios, olhando a cidade de cima.</summary>
    [Export(PropertyHint.Range, "5,60,1")] public float Height { get; set; } = 17f;

    private Camera3D? _camera;
    private float _angulo = Mathf.Pi * 0.25f;

    /// <summary>A câmera do fundo. Para o probe/depuração.</summary>
    public Camera3D? BackdropCamera => _camera;

    public override void _Ready()
    {
        var arena = ArenaScene?.Instantiate();
        if (arena is null)
        {
            GD.PushError($"{Name}: atribua ArenaScene.");
            return;
        }

        foreach (var filho in arena.GetChildren())
        {
            if (filho is CharacterController or CameraRig or SpawnDirector or WaveDirector or HordeGameMode)
                filho.Free();
        }

        AddChild(arena);

        _camera = new Camera3D { Current = true, Fov = 50f, Far = 300f };
        AddChild(_camera);
        Posicionar();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_camera is null)
            return;

        _angulo += Mathf.Tau / OrbitSeconds * (float)delta;
        Posicionar();
    }

    private void Posicionar()
    {
        if (_camera is null)
            return;

        _camera.Position = new Vector3(Mathf.Cos(_angulo) * Radius, Height, Mathf.Sin(_angulo) * Radius);
        _camera.LookAt(Vector3.Zero, Vector3.Up);
    }
}
