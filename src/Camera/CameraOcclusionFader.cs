using System.Collections.Generic;
using System.Linq;
using Contenda.Core;
using Godot;

namespace Contenda.Camera;

/// <summary>
/// Esconde por transparência qualquer cenário entre a câmera e o alvo.
/// </summary>
/// <remarks>
/// Spec 02 §6 e spec 17 §4.3: "Fade dithered obrigatório em qualquer objeto
/// entre câmera e jogador... A câmera não se move para desviar; o objeto é
/// que desaparece." Sem colisão de câmera nenhuma -- é isto que preserva o
/// pilar de que o enquadramento nunca muda (spec 02 §4).
///
/// "Dithered" sai de graça do próprio motor: <see cref="BaseMaterial3D.TransparencyEnum.AlphaHash"/>
/// já é transparência por estipulagem (dithering), sem ordenação de
/// triângulos nem shader próprio -- exatamente o efeito pedido, sem escrever
/// um shader para isto.
///
/// Um raio fino, não uma forma larga: repetimos a consulta excluindo cada
/// corpo encontrado para revelar os outros no mesmo segmento. Continua sendo
/// uma câmera e poucos corpos por quadro, abaixo do custo dos raycasts de
/// percepção feitos por cada inimigo.
///
/// Roda em <c>_PhysicsProcess</c>, não em <c>_Process</c>: mesma disciplina
/// de <c>HealthBar</c>/<c>HudController</c> -- <c>_Process</c> não roda de
/// forma confiável em modo headless, e as sondas deste projeto dependem de
/// rodar headless.
/// </remarks>
public sealed partial class CameraOcclusionFader : Node
{
    private readonly struct GeometryAccess
    {
        private readonly MeshInstance3D? _meshInstance;
        private readonly MultiMeshInstance3D? _multiMeshInstance;

        private GeometryAccess(MeshInstance3D meshInstance)
        {
            _meshInstance = meshInstance;
            _multiMeshInstance = null;
        }

        private GeometryAccess(MultiMeshInstance3D multiMeshInstance)
        {
            _meshInstance = null;
            _multiMeshInstance = multiMeshInstance;
        }

        public static GeometryAccess From(GeometryInstance3D instance) => instance switch
        {
            MeshInstance3D mesh => new GeometryAccess(mesh),
            MultiMeshInstance3D multiMesh => new GeometryAccess(multiMesh),
            _ => default
        };

        public Mesh? Mesh => _meshInstance?.Mesh ?? _multiMeshInstance?.Multimesh?.Mesh;
        public bool IsMultiMesh => _multiMeshInstance is not null;
        public int SurfaceKey(int surface) => IsMultiMesh ? -1 : surface;

        public Material? GetOverride(int surface) => _meshInstance is not null
            ? _meshInstance.GetSurfaceOverrideMaterial(surface)
            : surface < 0 ? _multiMeshInstance?.MaterialOverride : null;

        public void SetOverride(int surface, Material? material)
        {
            if (_meshInstance is not null)
                _meshInstance.SetSurfaceOverrideMaterial(surface, material);
            else if (_multiMeshInstance is not null && surface < 0)
                _multiMeshInstance.MaterialOverride = material;
        }
    }

    /// <summary>A câmera de onde o raio parte.</summary>
    [Export] public NodePath CameraPath { get; set; } = new();

    /// <summary>Quem a câmera está tentando enquadrar.</summary>
    [Export] public NodePath TargetPath { get; set; } = new();

    /// <summary>Deslocamento sobre o alvo -- mesmo motivo de <c>CameraSettings.TargetOffset</c>: mirar no torso, não nos pés.</summary>
    [Export] public Vector3 TargetOffset { get; set; } = new(0f, 1f, 0f);

    /// <summary>Camadas físicas que podem ocluir. Só cenário -- nunca personagem.</summary>
    [Export(PropertyHint.Layers3DPhysics)] public uint OcclusionMask { get; set; } = PhysicsLayers.World;

    /// <summary>Opacidade do objeto escondido. Zero seria invisível de vez; uma sombra fraca ainda lê como "tem algo ali".</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float FadedAlpha { get; set; } = 0.15f;

    private Camera3D? _camera;
    private Node3D? _alvo;
    private PhysicsRayQueryParameters3D? _rayQuery;
    private readonly List<Node> _oclusoresAtuais = [];
    private readonly List<Node> _oclusoresOcultos = [];
    private readonly List<(GeometryInstance3D Instance, int Surface, Material? OriginalOverride)> _overridesAnteriores = [];
    private readonly Dictionary<(GeometryInstance3D Instance, int Surface), BaseMaterial3D> _materiaisDeFade = [];
    private readonly Dictionary<Node, GeometryInstance3D[]> _geometriasPorOclusor = [];
    private readonly Dictionary<Node, List<GeometryInstance3D>> _geometriasEmConstrucao = [];
    private int _limiteDeOclusoresNoRaio;

    public override void _Ready()
    {
        _camera = GetNodeOrNull<Camera3D>(CameraPath);
        _alvo = GetNodeOrNull<Node3D>(TargetPath);
        _rayQuery = PhysicsRayQueryParameters3D.Create(Vector3.Zero, Vector3.Zero, OcclusionMask);

        IndexarGeometrias(GetTree().Root, null);
        PrepararMateriaisDeFade();
        _oclusoresAtuais.Capacity = _geometriasPorOclusor.Count;
        _oclusoresOcultos.Capacity = _geometriasPorOclusor.Count;

        if (_rayQuery is not null)
        {
            var excluidos = _rayQuery.Exclude;
            excluidos.Resize(_limiteDeOclusoresNoRaio);
            excluidos.Clear();
            _rayQuery.Exclude = excluidos;
        }

        if (_camera is null)
        {
            GD.PushError($"{Name}: CameraPath não resolveu.");
            SetPhysicsProcess(false);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_camera is null || _alvo is null)
            return;


        EncontrarOclusores(_camera.GlobalPosition, _alvo.GlobalPosition + TargetOffset);
        if (MesmoConjuntoDeOclusores())
            return;

        Restaurar();
        for (var i = 0; i < _oclusoresAtuais.Count; i++)
        {
            var oclusor = _oclusoresAtuais[i];
            Esconder(oclusor);
            _oclusoresOcultos.Add(oclusor);
        }
    }

    /// <remarks>
    /// Fronteira com a engine: <c>Godot.Collections.Dictionary</c> só aqui,
    /// mesma disciplina de <c>Perception.LinhaDeVisaoLivre</c>.
    /// </remarks>
    private void EncontrarOclusores(Vector3 origem, Vector3 alvo)
    {
        _oclusoresAtuais.Clear();
        if (_rayQuery is null)
            return;

        var espaco = _camera!.GetViewport().World3D.DirectSpaceState;
        _rayQuery.From = origem;
        _rayQuery.To = alvo;

        // A coleção é parte da API de entrada exigida pela engine para excluir
        // colisores já atingidos. Reutilizamos o buffer do próprio query em vez
        // de criar uma Godot.Collections.Array a cada quadro.
        var excluidos = _rayQuery.Exclude;
        excluidos.Clear();
        _rayQuery.Exclude = excluidos;
        for (var quantidadeDeTestes = 0; quantidadeDeTestes < _limiteDeOclusoresNoRaio; quantidadeDeTestes++)
        {
            var resultado = espaco.IntersectRay(_rayQuery);
            if (resultado.Count == 0)
                return;

            // O raio acerta o corpo de colisão, não a malha. A cidade pode ter
            // vários corpos com modelos entre a câmera e o jogador; coletamos
            // todos para que nenhum deles continue opaco.
            var collider = resultado["collider"].As<CollisionObject3D>();
            if (collider is null)
                return;

            if (_geometriasPorOclusor.ContainsKey(collider))
                _oclusoresAtuais.Add(collider);
            var rid = collider.GetRid();
            if (excluidos.Contains(rid))
                return;
            excluidos.Add(rid);
            _rayQuery.Exclude = excluidos;
        }
    }

    private bool MesmoConjuntoDeOclusores()
    {
        if (_oclusoresAtuais.Count != _oclusoresOcultos.Count)
            return false;

        for (var i = 0; i < _oclusoresAtuais.Count; i++)
        {
            if (_oclusoresAtuais[i] != _oclusoresOcultos[i])
                return false;
        }

        return true;
    }

    /// <remarks>
    /// Usa uma cópia do material original por superfície, preservando textura
    /// e albedo. Os materiais são preparados no <c>_Ready</c>, fora do caminho
    /// por quadro, e cada malha mantém o override anterior para restauração.
    /// </remarks>
    private void Esconder(Node oclusor)
    {
        if (!_geometriasPorOclusor.TryGetValue(oclusor, out var instancias))
            return;

        foreach (var instancia in instancias)
        {
            var geometry = GeometryAccess.From(instancia);
            var malha = geometry.Mesh;
            if (!instancia.Visible || malha is null)
                continue;

            for (var superficie = 0; superficie < malha.GetSurfaceCount(); superficie++)
            {
                if (geometry.IsMultiMesh && superficie > 0)
                    break;

                var chaveSuperficie = geometry.SurfaceKey(superficie);
                var materialBase = instancia.MaterialOverride
                    ?? geometry.GetOverride(chaveSuperficie)
                    ?? malha.SurfaceGetMaterial(superficie);
                if (materialBase is not BaseMaterial3D materialBase3D)
                    continue;

                var key = (instancia, chaveSuperficie);
                if (!_materiaisDeFade.TryGetValue(key, out var materialFade))
                    continue;

                var overrideAnterior = geometry.GetOverride(chaveSuperficie);
                var corBase = materialFade.AlbedoColor;
                materialFade.AlbedoColor = new Color(corBase.R, corBase.G, corBase.B, FadedAlpha);
                geometry.SetOverride(chaveSuperficie, materialFade);
                _overridesAnteriores.Add((instancia, chaveSuperficie, overrideAnterior));
            }
        }
    }

    private void Restaurar()
    {
        foreach (var (instancia, superficie, overrideAnterior) in _overridesAnteriores)
        {
            if (GodotObject.IsInstanceValid(instancia))
                GeometryAccess.From(instancia).SetOverride(superficie, overrideAnterior);
        }

        _overridesAnteriores.Clear();
        _oclusoresOcultos.Clear();
    }

    private void IndexarGeometrias(Node raiz, CollisionObject3D? oclusorAtual)
    {
        if (raiz is CollisionObject3D corpo && (corpo.CollisionLayer & OcclusionMask) != 0)
        {
            oclusorAtual = corpo;
            // Também contamos colisões sem malha: elas precisam ser excluídas
            // para que o raio continue até um objeto visível mais atrás.
            _limiteDeOclusoresNoRaio++;
        }

        if (oclusorAtual is not null && raiz is GeometryInstance3D instancia)
        {
            if (!_geometriasEmConstrucao.TryGetValue(oclusorAtual, out var encontradas))
                _geometriasEmConstrucao.Add(oclusorAtual, encontradas = []);

            encontradas.Add(instancia);
        }

        // GetChildren é uma coleção Godot; convertê-la aqui no _Ready mantém
        // Godot.Collections fora do caminho por quadro do fade.
        foreach (var filho in raiz.GetChildren().ToArray())
            IndexarGeometrias(filho, oclusorAtual);
    }

    private void PrepararMateriaisDeFade()
    {
        var capacidade = 0;
        foreach (var (oclusor, instancias) in _geometriasEmConstrucao)
        {
            var fotografadas = instancias.ToArray();
            _geometriasPorOclusor.Add(oclusor, fotografadas);
            foreach (var instancia in fotografadas)
            {
                var geometry = GeometryAccess.From(instancia);
                if (geometry.Mesh is { } malha)
                    capacidade += geometry.IsMultiMesh ? 1 : malha.GetSurfaceCount();
            }
        }

        _geometriasEmConstrucao.Clear();
        _overridesAnteriores.Capacity = capacidade;
        foreach (var instancias in _geometriasPorOclusor.Values)
        foreach (var instancia in instancias)
        {
            var geometry = GeometryAccess.From(instancia);
            var malha = geometry.Mesh;
            if (malha is null)
                continue;

            for (var superficie = 0; superficie < malha.GetSurfaceCount(); superficie++)
            {
                if (geometry.IsMultiMesh && superficie > 0)
                    break;

                var chaveSuperficie = geometry.SurfaceKey(superficie);
                var materialBase = instancia.MaterialOverride
                    ?? geometry.GetOverride(chaveSuperficie)
                    ?? malha.SurfaceGetMaterial(superficie);
                if (materialBase is not BaseMaterial3D materialBase3D)
                    continue;

                var materialFade = (BaseMaterial3D)materialBase3D.Duplicate();
                materialFade.Transparency = BaseMaterial3D.TransparencyEnum.AlphaHash;
                _materiaisDeFade.Add((instancia, chaveSuperficie), materialFade);
            }
        }
    }

}
