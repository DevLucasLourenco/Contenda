using System.Collections.Generic;
using Contenda.Core;
using Godot;

namespace Contenda.Arena;

/// <summary>
/// Troca o visual cinza da arena por módulos do Kenney City Kit sem acoplar
/// as colisões de jogo às malhas renderizadas.
/// </summary>
/// <remarks>
/// A colisão continua sendo a geometria simples que sustenta saltos, rotas e
/// o bake determinístico do navmesh. Os modelos importados são filhos
/// visuais dos corpos correspondentes; os dois carros estacionados recebem
/// caixas de colisão próprias e são incluídos no bake da NavigationRegion.
/// </remarks>
public sealed partial class CityArtDirector : Node3D
{
    private readonly record struct ModelPlacement(
        string AssetPath, string NodeName, Vector3 Scale, Vector3 Position, float Yaw = 0f);

    private const string Commercial = "res://assets/city/kenney/commercial/Models/";
    private const string Industrial = "res://assets/city/kenney/industrial/Models/";
    private const string Roads = "res://assets/city/kenney/roads/Models/";
    private const string Cars = "res://assets/city/kenney/cars/Models/";

    [ExportGroup("Veículos e obstáculos")]
    [Export] public Vector3 ContainerVisualScale { get; set; } = new(3.623f, 1.163f, 0.821f);
    [Export] public Vector3 ContainerVisualPosition { get; set; } = new(0f, -0.75f, 0f);
    [Export] public Vector3 VanVisualScale { get; set; } = new(1.5f, 1.1f, 1.1f);
    [Export] public Vector3 VanVisualPosition { get; set; } = new(0f, -0.32f, 0f);
    [Export] public float VanVisualYaw { get; set; } = Mathf.Pi / 2f;
    [Export] public Vector3 DumpsterVisualScale { get; set; } = new(10.9f, 5.8f, 5.4f);
    [Export] public Vector3 DumpsterVisualPosition { get; set; } = new(0f, -0.6f, 0f);

    [ExportGroup("Canteiro")]
    [Export] public Vector3 WarningSignScale { get; set; } = new(3f, 3f, 3f);
    [Export] public Vector3 WarningSignPosition { get; set; } = new(13.8f, 0.015f, -14.8f);
    [Export] public Vector3 ConeScale { get; set; } = new(10f, 10f, 10f);
    [Export] public Vector3 FirstConePosition { get; set; } = new(13.2f, 0.015f, -14.2f);
    [Export] public Vector3 SecondConePosition { get; set; } = new(14.1f, 0.015f, -14.2f);

    [ExportGroup("Fachadas")]
    [Export] public Vector3 NorthFacadeStart { get; set; } = new(-25.8f, -5f, 3.3f);
    [Export] public Vector3 SouthFacadeStart { get; set; } = new(-25.8f, -5f, -3.3f);
    [Export] public Vector3 WestFacadeStart { get; set; } = new(2.9f, -5f, -12f);
    [Export] public Vector3 EastFacadeStart { get; set; } = new(-2.9f, -5f, -12f);
    [Export] public float NorthSouthFacadeSpacing { get; set; } = 8.6f;
    [Export] public float EastWestFacadeSpacing { get; set; } = 6f;
    [Export] public Vector3 NorthSouthFacadeScale { get; set; } = new(9.2f, 5f, 8.2f);
    [Export] public Vector3 NorthSouthLowDetailScale { get; set; } = new(17.2f, 3.25f, 15.2f);
    [Export] public Vector3 EastWestFacadeScale { get; set; } = new(8.2f, 5f, 8.2f);
    [Export] public Vector3 EastWestLowDetailScale { get; set; } = new(15.2f, 3.25f, 12f);

    [ExportGroup("Carros estacionados")]
    [Export] public Vector3 ParkedSedanPosition { get; set; } = new(-13f, 0f, -16f);
    [Export] public Vector3 ParkedSuvPosition { get; set; } = new(13f, 0f, 15.2f);
    [Export] public Vector3 ParkedCarScale { get; set; } = new(1.4f, 1.2f, 1.1f);

    /// <summary>Referências exportadas às peças do bloqueio visual que recebem os módulos da cidade.</summary>
    [Export] public Godot.Collections.Array<NodePath> SceneNodePaths { get; set; } = new()
    {
        new("%NavigationRegion3D"),
        new("%Conteiner"), new("%Onibus"), new("%Cacamba"), new("%Andaime"),
        new("%PredioNorte"), new("%PredioSul"), new("%PredioOeste"), new("%PredioLeste"),
        new("%RuaNorteOeste"), new("%RuaNorteLeste"), new("%RuaNorteNotch"),
        new("%RuaSulOeste"), new("%RuaSulLeste"), new("%RuaSulNotch"),
        new("%RuaOesteSul"), new("%RuaOesteNorte"), new("%RuaOesteNotch"),
        new("%RuaLesteSul"), new("%RuaLesteNorte"), new("%RuaLesteNotch"),
        new("%CalcadaOeste"), new("%CalcadaLeste")
    };

    private readonly Dictionary<string, Node> _sceneNodes = [];

    public override void _Ready()
    {
        foreach (var path in SceneNodePaths)
        {
            if (GetNodeOrNull(path) is { } node)
                _sceneNodes[node.Name.ToString()] = node;
            else
                GD.PushError($"{Name}: SceneNodePaths não resolveu '{path}'.");
        }

        DecorateObstacle("Conteiner", new ModelPlacement(Industrial + "shipping-container-a.glb", "Visual",
            ContainerVisualScale, ContainerVisualPosition));
        DecorateObstacle("Onibus", new ModelPlacement(Cars + "van.glb", "Visual",
            VanVisualScale, VanVisualPosition, VanVisualYaw));
        DecorateObstacle("Cacamba", new ModelPlacement(Roads + "dumpster.glb", "Visual",
            DumpsterVisualScale, DumpsterVisualPosition));

        DecorateScaffold();
        DecorateConstructionSite();
        DecorateBuildingRows();
        DecorateStreetsAndSidewalks();
        AddCrosswalks();
        AddParkedCars();
    }

    private void DecorateObstacle(string nodeName, ModelPlacement visual)
    {
        var body = GetSceneNode<StaticBody3D>(nodeName);
        if (body is null)
        {
            GD.PushError($"{Name}: StaticBody3D da peça urbana '{nodeName}' não resolveu.");
            return;
        }

        HideBlockoutMesh(body);
        AddModel(body, visual);
    }

    private void DecorateScaffold()
    {
        var scaffold = GetSceneNode<StaticBody3D>("Andaime");
        if (scaffold is null)
            return;

        // A caixa cinza era uma plataforma sólida e não lia como andaime.
        // Mantemos a colisão para preservar o salto/navmesh e substituímos
        // só o desenho por montantes, travessas e pranchas de obra.
        HideBlockoutMesh(scaffold);
        BuildScaffold(scaffold);
    }

    private void DecorateConstructionSite()
    {
        var navRegion = GetSceneNode<Node3D>("NavigationRegion3D");
        if (navRegion is null)
            return;

        // Sinalização e cones ficam nas bordas do canteiro, longe das linhas
        // de navegação e dos pontos de surgimento.
        AddModel(navRegion, new ModelPlacement(Roads + "road-sign-warning.glb", "SinalObra",
            WarningSignScale, WarningSignPosition));
        AddModel(navRegion, new ModelPlacement(Roads + "construction-cone.glb", "ConeObra1",
            ConeScale, FirstConePosition));
        AddModel(navRegion, new ModelPlacement(Roads + "construction-cone.glb", "ConeObra2",
            ConeScale, SecondConePosition));
    }

    private void DecorateBuildingRows()
    {
        var north = GetSceneNode<StaticBody3D>("PredioNorte");
        var south = GetSceneNode<StaticBody3D>("PredioSul");
        var west = GetSceneNode<StaticBody3D>("PredioOeste");
        var east = GetSceneNode<StaticBody3D>("PredioLeste");
        if (north is null || south is null || west is null || east is null)
            return;

        foreach (var building in new[] { north, south, west, east })
            HideBlockoutMesh(building);

        var facades = new[]
        {
            Commercial + "building-a.glb",
            Commercial + "building-d.glb",
            Commercial + "building-g.glb",
            Commercial + "building-j.glb",
            Commercial + "low-detail-building-a.glb",
            Commercial + "low-detail-building-f.glb",
            Industrial + "building-a.glb"
        };

        // A linha externa substitui os quatro blocos de limite por fachadas
        // modulares. As construções ficam fora da área jogável e do navmesh.
        for (var i = 0; i < facades.Length; i++)
        {
            var northSouthScale = FacadeScale(facades[i], NorthSouthFacadeScale, NorthSouthLowDetailScale);
            var northPosition = NorthFacadeStart + new Vector3(i * NorthSouthFacadeSpacing, 0f, 0f);
            var southPosition = SouthFacadeStart + new Vector3(i * NorthSouthFacadeSpacing, 0f, 0f);
            AddModel(north, new ModelPlacement(facades[i], $"FacadeNorth{i}", northSouthScale,
                northPosition, Mathf.Pi));
            var southFacade = facades[(i + 3) % facades.Length];
            AddModel(south, new ModelPlacement(southFacade, $"FacadeSouth{i}",
                FacadeScale(southFacade, NorthSouthFacadeScale, NorthSouthLowDetailScale), southPosition));
        }

        for (var i = 0; i < 5; i++)
        {
            var facadePath = facades[(i + 1) % facades.Length];
            var eastFacade = facades[(i + 4) % facades.Length];
            var westPosition = WestFacadeStart + new Vector3(0f, 0f, i * EastWestFacadeSpacing);
            var eastPosition = EastFacadeStart + new Vector3(0f, 0f, i * EastWestFacadeSpacing);
            AddModel(west, new ModelPlacement(facadePath, $"FacadeWest{i}",
                FacadeScale(facadePath, EastWestFacadeScale, EastWestLowDetailScale), westPosition, -Mathf.Pi / 2f));
            AddModel(east, new ModelPlacement(eastFacade, $"FacadeEast{i}",
                FacadeScale(eastFacade, EastWestFacadeScale, EastWestLowDetailScale), eastPosition, Mathf.Pi / 2f));
        }
    }

    private void AddCrosswalks()
    {
        var packed = GD.Load<PackedScene>(Roads + "road-crossing.glb");
        var prototype = packed?.Instantiate<Node3D>();
        if (prototype is null)
        {
            GD.PushError($"{Name}: não carregou a peça modular de faixa de pedestres.");
            return;
        }

        var meshInstance = GetMeshInstance(prototype);
        if (meshInstance?.Mesh is null)
        {
            prototype.Free();
            GD.PushError($"{Name}: a malha da faixa de pedestres não foi encontrada.");
            return;
        }

        var transforms = new List<Transform3D>();
        AddCrosswalkRow(transforms, new Vector3(-4f, 0.045f, -10f), 0f);
        AddCrosswalkRow(transforms, new Vector3(0f, 0.045f, 10f), 0f);
        AddCrosswalkRow(transforms, new Vector3(10f, 0.045f, 0f), Mathf.Pi / 2f);
        AddCrosswalkRow(transforms, new Vector3(-10f, 0.045f, 0f), Mathf.Pi / 2f);
        AddInstancedMesh(this, "FaixasDePedestres", meshInstance.Mesh, transforms);
        prototype.Free();
    }

    private static void AddCrosswalkRow(List<Transform3D> transforms, Vector3 center, float yaw)
    {
        var basis = Basis.FromEuler(new Vector3(0f, yaw, 0f));
        for (var i = 0; i < 6; i++)
        {
            var localX = (i - 2.5f) * 1f;
            var offset = basis * new Vector3(localX, 0f, 0f);
            transforms.Add(new Transform3D(basis, center + offset));
        }
    }

    private void DecorateStreetsAndSidewalks()
    {
        var roadMesh = LoadModelMesh(Roads + "road-straight.glb", "road-straight");
        if (roadMesh is not null)
        {
            var roadNames = new[]
            {
                "RuaNorteOeste", "RuaNorteLeste", "RuaNorteNotch",
                "RuaSulOeste", "RuaSulLeste", "RuaSulNotch",
                "RuaOesteSul", "RuaOesteNorte", "RuaOesteNotch",
                "RuaLesteSul", "RuaLesteNorte", "RuaLesteNotch"
            };

            foreach (var roadName in roadNames)
            {
                var road = GetSceneNode<StaticBody3D>(roadName);
                var blockout = GetBlockoutMesh(road);
                if (road is null || blockout?.Mesh is not BoxMesh bounds)
                    continue;

                blockout.Visible = false;
                var transforms = new List<Transform3D>();
                var cellsX = Mathf.RoundToInt(bounds.Size.X);
                var cellsZ = Mathf.RoundToInt(bounds.Size.Z);
                for (var x = 0; x < cellsX; x++)
                for (var z = 0; z < cellsZ; z++)
                {
                    var local = new Vector3(
                        x - (cellsX - 1) * 0.5f,
                        bounds.Size.Y * 0.5f + 0.02f,
                        z - (cellsZ - 1) * 0.5f);
                    transforms.Add(new Transform3D(Basis.Identity, local));
                }

                AddInstancedMesh(road, "KenneyRoadSurface", roadMesh, transforms);
            }

            var sidewalkMesh = LoadModelMesh(Roads + "road-side.glb", "road-side");
            if (sidewalkMesh is not null)
            {
                DecorateSidewalk(GetSceneNode<StaticBody3D>("CalcadaOeste"), sidewalkMesh);
                DecorateSidewalk(GetSceneNode<StaticBody3D>("CalcadaLeste"), sidewalkMesh);
            }
        }
    }

    private static void DecorateSidewalk(StaticBody3D? sidewalk, Mesh sidewalkMesh)
    {
        var blockout = GetBlockoutMesh(sidewalk);
        if (sidewalk is null || blockout?.Mesh is not BoxMesh bounds)
            return;

        blockout.Visible = false;
        var transforms = new List<Transform3D>();
        var rows = Mathf.RoundToInt(bounds.Size.X);
        for (var row = 0; row < rows; row++)
        for (var segment = -6; segment <= 6; segment++)
        {
            // Kenney's sidewalk section is 1.31 m long and centered 15.5 cm
            // behind its origin; these offsets keep thirteen modules inside
            // the existing 16 m sidewalk footprint without stretching them.
            var local = new Vector3(
                row - (rows - 1) * 0.5f,
                bounds.Size.Y * 0.5f + 0.02f,
                segment * 1.23f + 0.155f);
            transforms.Add(new Transform3D(Basis.Identity, local));
        }

        AddInstancedMesh(sidewalk, "KenneySidewalk", sidewalkMesh, transforms);
    }

    private Mesh? LoadModelMesh(string assetPath, string meshName)
    {
        var packed = GD.Load<PackedScene>(assetPath);
        var prototype = packed?.Instantiate<Node3D>();
        if (prototype is null)
        {
            GD.PushError($"{Name}: asset urbano não carregou: {assetPath}");
            return null;
        }

        var mesh = GetMeshInstance(prototype)?.Mesh;
        prototype.Free();
        if (mesh is null)
            GD.PushError($"{Name}: malha '{meshName}' não encontrada em {assetPath}");
        return mesh;
    }

    private static void AddInstancedMesh(Node3D parent, string nodeName, Mesh mesh,
        List<Transform3D> transforms)
    {
        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = transforms.Count
        };
        for (var i = 0; i < transforms.Count; i++)
            multimesh.SetInstanceTransform(i, transforms[i]);

        parent.AddChild(new MultiMeshInstance3D
        {
            Name = nodeName,
            Multimesh = multimesh
        });
    }

    private void AddParkedCars()
    {
        var region = GetSceneNode<Node3D>("NavigationRegion3D");
        if (region is null)
            return;

        AddParkedCar(region, "ParkedSedan", Cars + "sedan.glb", ParkedSedanPosition, ParkedCarScale);
        AddParkedCar(region, "ParkedSuv", Cars + "suv.glb", ParkedSuvPosition, ParkedCarScale);
    }

    private void AddParkedCar(Node3D region, string nodeName, string assetPath, Vector3 position, Vector3 scale)
    {
        var car = new StaticBody3D
        {
            Name = nodeName,
            Position = position,
            CollisionLayer = PhysicsLayers.World,
            CollisionMask = 0
        };
        var collider = new CollisionShape3D
        {
            Name = "Col",
            Position = new Vector3(0f, 0.8f, 0f),
            Shape = new BoxShape3D { Size = new Vector3(2.1f, 1.6f, 3f) }
        };
        car.AddChild(collider);
        region.AddChild(car);
        AddModel(car, new ModelPlacement(assetPath, "Visual", scale, new Vector3(0f, 0.36f, 0f)));
    }

    private void AddModel(Node3D parent, ModelPlacement placement)
    {
        var packed = GD.Load<PackedScene>(placement.AssetPath);
        if (packed is null)
        {
            GD.PushError($"{Name}: asset urbano não carregou: {placement.AssetPath}");
            return;
        }

        var model = packed.Instantiate<Node3D>();
        model.Name = placement.NodeName;
        model.Position = placement.Position;
        model.Rotation = new Vector3(0f, placement.Yaw, 0f);
        model.Scale = placement.Scale;
        parent.AddChild(model);
    }

    private static Vector3 FacadeScale(string assetPath, Vector3 standardScale, Vector3 lowDetailScale) =>
        assetPath.Contains("low-detail", System.StringComparison.Ordinal) ? lowDetailScale : standardScale;

    private static void BuildScaffold(Node3D scaffold)
    {
        var steel = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.73f, 0.49f, 0.16f),
            Metallic = 0.48f,
            Roughness = 0.62f
        };
        var timber = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.48f, 0.31f, 0.16f),
            Roughness = 0.88f
        };

        var posts = new List<Transform3D>(4);
        for (var x = -1; x <= 1; x += 2)
        for (var z = -1; z <= 1; z += 2)
            posts.Add(new Transform3D(Basis.Identity, new Vector3(x * 1.4f, -0.05f, z * 1.4f)));
        AddInstancedMesh(scaffold, "ScaffoldPosts", CreateBoxMesh(new Vector3(0.12f, 1.4f, 0.12f), steel), posts);

        var rails = new List<Transform3D>(8);
        for (var side = -1; side <= 1; side += 2)
        for (var level = 0; level < 2; level++)
        {
            var height = level == 0 ? -0.47f : 0.52f;
            rails.Add(new Transform3D(Basis.Identity, new Vector3(0f, height, side * 1.4f)));
            rails.Add(new Transform3D(Basis.FromEuler(new Vector3(0f, Mathf.Pi / 2f, 0f)),
                new Vector3(side * 1.4f, height, 0f)));
        }
        AddInstancedMesh(scaffold, "ScaffoldRails", CreateBoxMesh(new Vector3(2.8f, 0.08f, 0.08f), steel), rails);

        var braces = new List<Transform3D>(4);
        var diagonal = Basis.FromEuler(new Vector3(0f, 0f, Mathf.Pi / 4f));
        var reverseDiagonal = Basis.FromEuler(new Vector3(0f, 0f, -Mathf.Pi / 4f));
        braces.Add(new Transform3D(diagonal, new Vector3(0f, -0.02f, -1.41f)));
        braces.Add(new Transform3D(reverseDiagonal, new Vector3(0f, -0.02f, -1.41f)));
        braces.Add(new Transform3D(diagonal, new Vector3(0f, -0.02f, 1.41f)));
        braces.Add(new Transform3D(reverseDiagonal, new Vector3(0f, -0.02f, 1.41f)));
        AddInstancedMesh(scaffold, "ScaffoldCrossBraces", CreateBoxMesh(new Vector3(0.07f, 1.95f, 0.07f), steel), braces);

        var deck = new List<Transform3D>(7);
        for (var index = 0; index < 7; index++)
            deck.Add(new Transform3D(Basis.Identity, new Vector3(0f, 0.7f, -1.29f + index * 0.43f)));
        AddInstancedMesh(scaffold, "ScaffoldDeck", CreateBoxMesh(new Vector3(2.8f, 0.1f, 0.4f), timber), deck);
    }

    private static BoxMesh CreateBoxMesh(Vector3 size, Material material) => new()
    {
        Size = size,
        Material = material
    };

    private static void HideBlockoutMesh(StaticBody3D body)
    {
        if (GetBlockoutMesh(body) is { } blockout)
            blockout.Visible = false;
    }

    private static MeshInstance3D? GetBlockoutMesh(StaticBody3D? body)
    {
        if (body is null)
            return null;

        for (var index = 0; index < body.GetChildCount(); index++)
        {
            if (body.GetChild(index) is MeshInstance3D mesh)
                return mesh;
        }

        return null;
    }

    private T? GetSceneNode<T>(string name) where T : Node =>
        _sceneNodes.TryGetValue(name, out var node) ? node as T : null;

    private static MeshInstance3D? GetMeshInstance(Node root)
    {
        // Os GLBs modulares da Kenney importam uma malha como único filho direto.
        // Não percorra a árvore: se o formato do asset mudar, falhe explicitamente.
        if (root is MeshInstance3D rootMesh)
            return rootMesh;

        return root.GetChildCount() == 1 ? root.GetChild(0) as MeshInstance3D : null;
    }
}
