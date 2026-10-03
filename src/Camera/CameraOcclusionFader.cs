using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
/// corpo encontrado para revelar os outros no mesmo segmento. Quando um corpo
/// reúne vários visuais, só os AABBs atravessados por esse segmento recebem
/// fade; numa MultiMesh, o proxy mantém esse fade restrito às instâncias
/// atingidas. Continua sendo uma câmera e poucos corpos por quadro, abaixo do
/// custo dos raycasts de percepção feitos por cada inimigo.
///
/// Roda em <c>_PhysicsProcess</c>, não em <c>_Process</c>: mesma disciplina
/// de <c>HealthBar</c>/<c>HudController</c> -- <c>_Process</c> não roda de
/// forma confiável em modo headless, e as sondas deste projeto dependem de
/// rodar headless.
/// </remarks>
public sealed partial class CameraOcclusionFader : Node
{
    private readonly record struct OcclusionTarget(
        Node Collider,
        GeometryInstance3D? Geometry,
        int MultiMeshInstanceIndex);

    private readonly record struct HiddenMultiMeshInstance(
        MultiMeshInstance3D Instance,
        int InstanceIndex,
        Transform3D OriginalTransform);

    private readonly struct GeometryAccess
    {
        private readonly MeshInstance3D? _meshInstance;

        private GeometryAccess(MeshInstance3D meshInstance)
        {
            _meshInstance = meshInstance;
        }

        public static GeometryAccess From(GeometryInstance3D instance) =>
            instance is MeshInstance3D mesh ? new GeometryAccess(mesh) : default;

        public Mesh? Mesh => _meshInstance?.Mesh;

        public Material? GetOverride(int surface) => _meshInstance?.GetSurfaceOverrideMaterial(surface);

        public void SetOverride(int surface, Material? material)
        {
            if (_meshInstance is not null)
                _meshInstance.SetSurfaceOverrideMaterial(surface, material);
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
    private RayCast3D? _raycast;
    private readonly List<OcclusionTarget> _oclusoresAtuais = [];
    private readonly List<OcclusionTarget> _oclusoresOcultos = [];
    private readonly List<HiddenMultiMeshInstance> _instanciasMultiMeshOcultas = [];
    private readonly List<(GeometryInstance3D Instance, int Surface, Material? OriginalOverride)> _overridesAnteriores = [];
    private readonly Dictionary<(GeometryInstance3D Instance, int Surface), BaseMaterial3D> _materiaisDeFade = [];
    private readonly Dictionary<Node, GeometryInstance3D[]> _geometriasPorOclusor = [];
    private readonly Dictionary<Node, List<GeometryInstance3D>> _geometriasEmConstrucao = [];
    private readonly Dictionary<GeometryInstance3D, Aabb[]> _limitesMundoPorGeometria = [];
    private readonly Dictionary<MultiMeshInstance3D, MultiMeshInstance3D> _proxiesDeFadeMultiMesh = [];
    private readonly Dictionary<MultiMeshInstance3D, int> _quantidadeDeInstanciasNoProxy = [];
    private readonly StringBuilder _caminhosOcultosBuffer = new();
    private int _limiteDeOclusoresNoRaio;
    private int _superficiesMultiMeshOcultas;
    private bool _medirAlocacoesGerenciadas;

    internal long TotalDeTicksDeOclusao { get; private set; }
    internal long TotalDeTestesDeRaio { get; private set; }
    internal long TotalDeColisoresEncontrados { get; private set; }
    internal ulong TempoTotalDeBuscaUsec { get; private set; }
    internal ulong TempoMaximoDeBuscaUsec { get; private set; }
    internal int MaximoDeTestesDeRaioPorTick { get; private set; }
    internal int MaximoDeColisoresPorTick { get; private set; }
    internal long TotalDeBytesGerenciadosAlocados { get; private set; }
    internal long TicksComAlocacaoGerenciada { get; private set; }
    internal long MaximoDeBytesAlocadosEmUmTick { get; private set; }
    internal int LimiteDeColisoresNoRaio => _limiteDeOclusoresNoRaio;
    internal int SuperficiesOcultasAgora => _overridesAnteriores.Count + _superficiesMultiMeshOcultas;

    internal string CaminhosDosOclusoresOcultosAgora
    {
        get
        {
            _caminhosOcultosBuffer.Clear();
            for (var i = 0; i < _oclusoresOcultos.Count; i++)
            {
                if (i > 0)
                    _caminhosOcultosBuffer.Append(", ");

                var alvo = _oclusoresOcultos[i];
                _caminhosOcultosBuffer.Append((alvo.Geometry ?? (Node)alvo.Collider).GetPath());
                if (alvo.MultiMeshInstanceIndex >= 0)
                    _caminhosOcultosBuffer.Append('[').Append(alvo.MultiMeshInstanceIndex).Append(']');
            }

            return _caminhosOcultosBuffer.ToString();
        }
    }

    public override void _Ready()
    {
        _camera = GetNodeOrNull<Camera3D>(CameraPath);
        _alvo = GetNodeOrNull<Node3D>(TargetPath);
        _raycast = new RayCast3D
        {
            Name = "OcclusionRay",
            Enabled = false,
            CollisionMask = OcclusionMask,
            CollideWithAreas = false,
            CollideWithBodies = true,
        };
        AddChild(_raycast);

        IndexarGeometrias(GetTree().Root, null);
        PrepararMateriaisDeFade();

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

        var medirAlocacoes = _medirAlocacoesGerenciadas;
        var bytesGerenciadosAntes = medirAlocacoes ? GC.GetAllocatedBytesForCurrentThread() : 0;
        try
        {
            TotalDeTicksDeOclusao++;
            var inicioBuscaUsec = Time.GetTicksUsec();
            EncontrarOclusores(_camera.GlobalPosition, _alvo.GlobalPosition + TargetOffset);
            var tempoBuscaUsec = Time.GetTicksUsec() - inicioBuscaUsec;
            TempoTotalDeBuscaUsec += tempoBuscaUsec;
            TempoMaximoDeBuscaUsec = Math.Max(TempoMaximoDeBuscaUsec, tempoBuscaUsec);
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
        finally
        {
            if (medirAlocacoes)
            {
                var bytesAlocadosNesteTick = GC.GetAllocatedBytesForCurrentThread() - bytesGerenciadosAntes;
                TotalDeBytesGerenciadosAlocados += bytesAlocadosNesteTick;
                if (bytesAlocadosNesteTick > 0)
                    TicksComAlocacaoGerenciada++;
                MaximoDeBytesAlocadosEmUmTick = Math.Max(MaximoDeBytesAlocadosEmUmTick, bytesAlocadosNesteTick);
            }
        }
    }

    internal void ResetarDiagnosticosDoBenchmark()
    {
        _medirAlocacoesGerenciadas = true;
        TotalDeTicksDeOclusao = 0;
        TotalDeTestesDeRaio = 0;
        TotalDeColisoresEncontrados = 0;
        TempoTotalDeBuscaUsec = 0;
        TempoMaximoDeBuscaUsec = 0;
        MaximoDeTestesDeRaioPorTick = 0;
        MaximoDeColisoresPorTick = 0;
        TotalDeBytesGerenciadosAlocados = 0;
        TicksComAlocacaoGerenciada = 0;
        MaximoDeBytesAlocadosEmUmTick = 0;
    }

    internal void PararDiagnosticosDeAlocacoesDoBenchmark() => _medirAlocacoesGerenciadas = false;

    /// <remarks>O raio e a lista de exceções são reutilizados entre quadros.</remarks>
    private void EncontrarOclusores(Vector3 origem, Vector3 alvo)
    {
        _oclusoresAtuais.Clear();
        if (_raycast is null)
            return;

        _raycast.GlobalPosition = origem;
        _raycast.TargetPosition = _raycast.ToLocal(alvo);
        _raycast.CollisionMask = OcclusionMask;
        _raycast.ClearExceptions();
        var testesDeRaio = 0;
        var colisoresEncontrados = 0;
        for (var quantidadeDeTestes = 0; quantidadeDeTestes < _limiteDeOclusoresNoRaio; quantidadeDeTestes++)
        {
            testesDeRaio++;
            _raycast.ForceRaycastUpdate();
            if (!_raycast.IsColliding())
                break;

            // O raio acerta o corpo de colisão, não a malha. A cidade pode ter
            // vários corpos com modelos entre a câmera e o jogador; coletamos
            // todos para que nenhum deles continue opaco.
            var collider = _raycast.GetCollider() as CollisionObject3D;
            if (collider is null)
                break;

            colisoresEncontrados++;

            if (_geometriasPorOclusor.TryGetValue(collider, out var geometrias))
                AdicionarGeometriasQueCruzamSegmento(collider, geometrias, origem, alvo);

            _raycast.AddExceptionRid(collider.GetRid());
        }

        TotalDeTestesDeRaio += testesDeRaio;
        TotalDeColisoresEncontrados += colisoresEncontrados;
        MaximoDeTestesDeRaioPorTick = Math.Max(MaximoDeTestesDeRaioPorTick, testesDeRaio);
        MaximoDeColisoresPorTick = Math.Max(MaximoDeColisoresPorTick, colisoresEncontrados);
    }

    private bool MesmoConjuntoDeOclusores()
    {
        if (_oclusoresAtuais.Count != _oclusoresOcultos.Count)
            return false;

        for (var i = 0; i < _oclusoresAtuais.Count; i++)
        {
            if (_oclusoresAtuais[i].Collider != _oclusoresOcultos[i].Collider
                || _oclusoresAtuais[i].Geometry != _oclusoresOcultos[i].Geometry
                || _oclusoresAtuais[i].MultiMeshInstanceIndex != _oclusoresOcultos[i].MultiMeshInstanceIndex)
                return false;
        }

        return true;
    }

    /// <remarks>
    /// Usa uma cópia do material original por superfície, preservando textura
    /// e albedo. Os materiais são preparados no <c>_Ready</c>, fora do caminho
    /// por quadro, e cada malha mantém o override anterior para restauração.
    /// </remarks>
    private void AdicionarGeometriasQueCruzamSegmento(
        Node collider,
        GeometryInstance3D[] instancias,
        Vector3 origem,
        Vector3 alvo)
    {
        foreach (var instancia in instancias)
        {
            if (!instancia.Visible || !_limitesMundoPorGeometria.TryGetValue(instancia, out var limitesPorInstancia))
                continue;

            var limiteDeInstanciasVisiveis = limitesPorInstancia.Length;
            if (instancia is MultiMeshInstance3D { Multimesh: { } multiMesh })
            {
                var quantidadeVisivel = multiMesh.VisibleInstanceCount;
                if (quantidadeVisivel >= 0)
                    limiteDeInstanciasVisiveis = Math.Min(quantidadeVisivel, limiteDeInstanciasVisiveis);
            }

            for (var i = 0; i < limiteDeInstanciasVisiveis; i++)
            {
                var cruza = SegmentoCruzaAabb(origem, alvo, limitesPorInstancia[i]);
                if (cruza)
                    _oclusoresAtuais.Add(new OcclusionTarget(
                        collider,
                        instancia,
                        instancia is MultiMeshInstance3D ? i : -1));
            }
        }
    }

    private void Esconder(OcclusionTarget alvo)
    {
        if (alvo.Geometry is MultiMeshInstance3D multiMesh && alvo.MultiMeshInstanceIndex >= 0)
        {
            EsconderInstanciaMultiMesh(multiMesh, alvo.MultiMeshInstanceIndex);
            return;
        }

        if (alvo.Geometry is { } geometriaEscolhida)
        {
            Esconder(geometriaEscolhida);
            return;
        }

        if (!_geometriasPorOclusor.TryGetValue(alvo.Collider, out var instancias))
            return;

        foreach (var instancia in instancias)
            Esconder(instancia);
    }

    private void Esconder(GeometryInstance3D instancia)
    {
        if (instancia is MultiMeshInstance3D)
            return;

        var geometry = GeometryAccess.From(instancia);
        var malha = geometry.Mesh;
        if (!instancia.Visible || malha is null)
            return;

        for (var superficie = 0; superficie < malha.GetSurfaceCount(); superficie++)
        {
            var chaveSuperficie = superficie;
            var materialBase = instancia.MaterialOverride
                ?? geometry.GetOverride(chaveSuperficie)
                ?? malha.SurfaceGetMaterial(superficie);
            if (materialBase is not BaseMaterial3D)
                continue;

            var key = (instancia, chaveSuperficie);
            if (!_materiaisDeFade.TryGetValue(key, out var materialFade))
                continue;

            var overrideAnterior = geometry.GetOverride(chaveSuperficie);
            AplicarOpacidadeDeFade(materialFade);
            geometry.SetOverride(chaveSuperficie, materialFade);
            _overridesAnteriores.Add((instancia, chaveSuperficie, overrideAnterior));
        }
    }

    private void EsconderInstanciaMultiMesh(MultiMeshInstance3D instancia, int indice)
    {
        if (instancia.Multimesh is not { } multimesh
            || indice < 0
            || indice >= multimesh.InstanceCount
            || (multimesh.VisibleInstanceCount >= 0 && indice >= multimesh.VisibleInstanceCount)
            || !_proxiesDeFadeMultiMesh.TryGetValue(instancia, out var proxy)
            || proxy.Multimesh is not { } multimeshProxy
            || !_quantidadeDeInstanciasNoProxy.TryGetValue(instancia, out var indiceProxy)
            || indiceProxy >= multimeshProxy.InstanceCount)
            return;

        // O material de uma MultiMesh é compartilhado por todas as instâncias:
        // colapsamos só as atingidas e as desenhamos no proxy em AlphaHash.
        var transformacaoOriginal = multimesh.GetInstanceTransform(indice);
        var transformacaoOculta = new Transform3D(
            transformacaoOriginal.Basis.Scaled(Vector3.Zero),
            transformacaoOriginal.Origin);
        multimesh.SetInstanceTransform(indice, transformacaoOculta);
        multimeshProxy.SetInstanceTransform(indiceProxy, transformacaoOriginal);
        multimeshProxy.VisibleInstanceCount = indiceProxy + 1;
        proxy.Visible = true;

        var quantidadeDeSuperficies = multimesh.Mesh?.GetSurfaceCount() ?? 0;
        _instanciasMultiMeshOcultas.Add(new HiddenMultiMeshInstance(
            instancia, indice, transformacaoOriginal));
        _quantidadeDeInstanciasNoProxy[instancia] = indiceProxy + 1;
        _superficiesMultiMeshOcultas += quantidadeDeSuperficies;
    }

    private void Restaurar()
    {
        foreach (var (instancia, superficie, overrideAnterior) in _overridesAnteriores)
        {
            if (GodotObject.IsInstanceValid(instancia))
                GeometryAccess.From(instancia).SetOverride(superficie, overrideAnterior);
        }

        _overridesAnteriores.Clear();
        foreach (var escondida in _instanciasMultiMeshOcultas)
        {
            if (GodotObject.IsInstanceValid(escondida.Instance)
                && escondida.Instance.Multimesh is { } multimesh
                && escondida.InstanceIndex < multimesh.InstanceCount)
                multimesh.SetInstanceTransform(escondida.InstanceIndex, escondida.OriginalTransform);
        }

        foreach (var (instancia, proxy) in _proxiesDeFadeMultiMesh)
        {
            if (GodotObject.IsInstanceValid(proxy))
            {
                proxy.Visible = false;
                if (proxy.Multimesh is { } multimeshProxy)
                    multimeshProxy.VisibleInstanceCount = 0;
            }

            _quantidadeDeInstanciasNoProxy[instancia] = 0;
        }

        _instanciasMultiMeshOcultas.Clear();
        _superficiesMultiMeshOcultas = 0;
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
                var malha = instancia is MultiMeshInstance3D multiMesh
                    ? multiMesh.Multimesh?.Mesh
                    : GeometryAccess.From(instancia).Mesh;
                if (malha is not null)
                    _limitesMundoPorGeometria.Add(instancia, CalcularLimitesMundo(instancia, malha));

                if (malha is not null)
                {
                    if (instancia is MultiMeshInstance3D multiMeshInstance)
                        PrepararProxyDeFadeMultiMesh(multiMeshInstance, malha);
                    else
                        capacidade += malha.GetSurfaceCount();
                }
            }
        }

        _geometriasEmConstrucao.Clear();
        _overridesAnteriores.Capacity = capacidade;
        var capacidadeDeAlvos = 0;
        foreach (var limites in _limitesMundoPorGeometria.Values)
            capacidadeDeAlvos += limites.Length;

        _oclusoresAtuais.Capacity = capacidadeDeAlvos;
        _oclusoresOcultos.Capacity = capacidadeDeAlvos;
        _instanciasMultiMeshOcultas.Capacity = capacidadeDeAlvos;
        foreach (var instancias in _geometriasPorOclusor.Values)
        foreach (var instancia in instancias)
        {
            if (instancia is MultiMeshInstance3D)
                continue;

            var geometry = GeometryAccess.From(instancia);
            var malha = geometry.Mesh;
            if (malha is null)
                continue;

            for (var superficie = 0; superficie < malha.GetSurfaceCount(); superficie++)
            {
                var chaveSuperficie = superficie;
                var materialBase = instancia.MaterialOverride
                    ?? geometry.GetOverride(chaveSuperficie)
                    ?? malha.SurfaceGetMaterial(superficie);
                if (materialBase is not BaseMaterial3D materialBase3D)
                    continue;

                var materialFade = CriarMaterialDeFade(materialBase3D);
                _materiaisDeFade.Add((instancia, chaveSuperficie), materialFade);
            }
        }
    }

    private void PrepararProxyDeFadeMultiMesh(MultiMeshInstance3D instancia, Mesh malha)
    {
        if (malha.GetSurfaceCount() == 0)
            return;

        var malhaFade = (Mesh)malha.Duplicate();

        for (var superficie = 0; superficie < malha.GetSurfaceCount(); superficie++)
        {
            var materialOriginal = instancia.MaterialOverride ?? malha.SurfaceGetMaterial(superficie);
            if (materialOriginal is not null and not BaseMaterial3D)
            {
                malhaFade.Free();
                return;
            }

            var materialBase = materialOriginal as BaseMaterial3D ?? new StandardMaterial3D();
            var materialFade = CriarMaterialDeFade(materialBase);
            malhaFade.SurfaceSetMaterial(superficie, materialFade);
        }

        var multimeshOriginal = instancia.Multimesh;
        if (multimeshOriginal is null)
        {
            malhaFade.Free();
            return;
        }

        var multimeshFade = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = malhaFade,
            InstanceCount = multimeshOriginal.InstanceCount,
            VisibleInstanceCount = 0
        };
        var proxy = new MultiMeshInstance3D
        {
            Name = $"{instancia.Name}FadeProxy",
            Multimesh = multimeshFade,
            Visible = false
        };
        instancia.AddChild(proxy);
        _proxiesDeFadeMultiMesh.Add(instancia, proxy);
        _quantidadeDeInstanciasNoProxy.Add(instancia, 0);
    }

    private BaseMaterial3D CriarMaterialDeFade(BaseMaterial3D materialBase)
    {
        var materialFade = (BaseMaterial3D)materialBase.Duplicate();
        materialFade.Transparency = BaseMaterial3D.TransparencyEnum.AlphaHash;
        AplicarOpacidadeDeFade(materialFade);
        return materialFade;
    }

    private void AplicarOpacidadeDeFade(BaseMaterial3D materialFade)
    {
        var corBase = materialFade.AlbedoColor;
        materialFade.AlbedoColor = new Color(corBase.R, corBase.G, corBase.B, FadedAlpha);
    }

    private static Aabb TransformarAabb(Aabb local, Transform3D transform)
    {
        var minimo = transform * local.Position;
        var maximo = minimo;
        var fim = local.End;
        for (var mask = 1; mask < 8; mask++)
        {
            var cantoLocal = new Vector3(
                (mask & 1) == 0 ? local.Position.X : fim.X,
                (mask & 2) == 0 ? local.Position.Y : fim.Y,
                (mask & 4) == 0 ? local.Position.Z : fim.Z);
            var cantoMundo = transform * cantoLocal;
            minimo = new Vector3(
                Mathf.Min(minimo.X, cantoMundo.X),
                Mathf.Min(minimo.Y, cantoMundo.Y),
                Mathf.Min(minimo.Z, cantoMundo.Z));
            maximo = new Vector3(
                Mathf.Max(maximo.X, cantoMundo.X),
                Mathf.Max(maximo.Y, cantoMundo.Y),
                Mathf.Max(maximo.Z, cantoMundo.Z));
        }

        return new Aabb(minimo, maximo - minimo);
    }

    private static Aabb[] CalcularLimitesMundo(GeometryInstance3D instancia, Mesh malha)
    {
        if (instancia is not MultiMeshInstance3D { Multimesh: { } multimesh })
            return [TransformarAabb(malha.GetAabb(), instancia.GlobalTransform)];

        var limites = new Aabb[multimesh.InstanceCount];
        var transformacaoGlobal = instancia.GlobalTransform;
        var limitesLocais = malha.GetAabb();
        for (var i = 0; i < limites.Length; i++)
            limites[i] = TransformarAabb(limitesLocais, transformacaoGlobal * multimesh.GetInstanceTransform(i));

        return limites;
    }

    private static bool SegmentoCruzaAabb(Vector3 origem, Vector3 alvo, Aabb aabb)
    {
        var direcao = alvo - origem;
        var entrada = 0f;
        var saida = 1f;
        var maximo = aabb.End;
        return CortarSegmentoNoEixo(origem.X, direcao.X, aabb.Position.X, maximo.X, ref entrada, ref saida)
            && CortarSegmentoNoEixo(origem.Y, direcao.Y, aabb.Position.Y, maximo.Y, ref entrada, ref saida)
            && CortarSegmentoNoEixo(origem.Z, direcao.Z, aabb.Position.Z, maximo.Z, ref entrada, ref saida);
    }

    private static bool CortarSegmentoNoEixo(
        float origem,
        float direcao,
        float minimo,
        float maximo,
        ref float entrada,
        ref float saida)
    {
        if (Mathf.Abs(direcao) < 0.000001f)
            return origem >= minimo && origem <= maximo;

        var primeiro = (minimo - origem) / direcao;
        var segundo = (maximo - origem) / direcao;
        if (primeiro > segundo)
            (primeiro, segundo) = (segundo, primeiro);

        entrada = Mathf.Max(entrada, primeiro);
        saida = Mathf.Min(saida, segundo);
        return entrada <= saida;
    }
}
