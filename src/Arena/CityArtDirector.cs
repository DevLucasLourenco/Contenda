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
    private const string Commercial = "res://assets/city/kenney/commercial/Models/";
    private const string Industrial = "res://assets/city/kenney/industrial/Models/";
    private const string Roads = "res://assets/city/kenney/roads/Models/";
    private const string Cars = "res://assets/city/kenney/cars/Models/";

    /// <summary>Referências exportadas às peças do bloqueio visual que recebem os módulos da cidade.</summary>
    [Export] public Godot.Collections.Array<NodePath> SceneNodePaths { get; set; } = new()
    {
        new("../NavigationRegion3D"),
        new("../NavigationRegion3D/Conteiner"), new("../NavigationRegion3D/Onibus"),
        new("../NavigationRegion3D/Cacamba"), new("../NavigationRegion3D/Andaime"),
        new("../PredioNorte"), new("../PredioSul"), new("../PredioOeste"), new("../PredioLeste"),
        new("../NavigationRegion3D/RuaNorteOeste"), new("../NavigationRegion3D/RuaNorteLeste"),
        new("../NavigationRegion3D/RuaNorteNotch"), new("../NavigationRegion3D/RuaSulOeste"),
        new("../NavigationRegion3D/RuaSulLeste"), new("../NavigationRegion3D/RuaSulNotch"),
        new("../NavigationRegion3D/RuaOesteSul"), new("../NavigationRegion3D/RuaOesteNorte"),
        new("../NavigationRegion3D/RuaOesteNotch"), new("../NavigationRegion3D/RuaLesteSul"),
        new("../NavigationRegion3D/RuaLesteNorte"), new("../NavigationRegion3D/RuaLesteNotch"),
        new("../NavigationRegion3D/CalcadaOeste"), new("../NavigationRegion3D/CalcadaLeste")
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

        DecorateObstacle("Conteiner", Industrial + "shipping-container-a.glb",
            new Vector3(3.623f, 1.163f, 0.821f), new Vector3(0f, -0.75f, 0f));
        DecorateObstacle("Onibus", Cars + "van.glb",
            new Vector3(1.5f, 1.1f, 1.1f), new Vector3(0f, -0.32f, 0f), Mathf.Pi / 2f);
        DecorateObstacle("Cacamba", Roads + "dumpster.glb",
            new Vector3(10.9f, 5.8f, 5.4f), new Vector3(0f, -0.6f, 0f));

        DecorateScaffold();
        DecorateConstructionSite();
        DecorateBuildingRows();
        DecorateStreetsAndSidewalks();
        AddCrosswalks();
        AddParkedCars();
    }

    private void DecorateObstacle(string nodeName, string assetPath, Vector3 scale, Vector3 offset, float yaw = 0f)
    {
        var body = GetSceneNode<StaticBody3D>(nodeName);
        if (body is null)
        {
            GD.PushError($"{Name}: StaticBody3D da peça urbana '{nodeName}' não resolveu.");
            return;
        }

        HideBlockoutMesh(body);
        AddModel(body, assetPath, "Visual", scale, offset, yaw);
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
        AddModel(navRegion, Roads + "road-sign-warning.glb", "SinalObra",
            new Vector3(3f, 3f, 3f), new Vector3(13.8f, 0.015f, -14.8f));
        AddModel(navRegion, Roads + "construction-cone.glb", "ConeObra1",
            new Vector3(10f, 10f, 10f), new Vector3(13.2f, 0.015f, -14.2f));
        AddModel(navRegion, Roads + "construction-cone.glb", "ConeObra2",
            new Vector3(10f, 10f, 10f), new Vector3(14.1f, 0.015f, -14.2f));
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
            var x = -25.8f + i * 8.6f;
            var facadeScale = facades[i].Contains("low-detail", System.StringComparison.Ordinal)
                ? new Vector3(17.2f, 3.25f, 15.2f)
                : new Vector3(9.2f, 5f, 8.2f);
            AddModel(north, facades[i], $"FacadeNorth{i}", facadeScale,
                new Vector3(x, -5f, 3.3f), Mathf.Pi);
            AddModel(south, facades[(i + 3) % facades.Length], $"FacadeSouth{i}", facadeScale,
                new Vector3(x, -5f, -3.3f));
        }

        for (var i = 0; i < 5; i++)
        {
            var z = -12f + i * 6f;
            var facadePath = facades[(i + 1) % facades.Length];
            var facadeScale = facadePath.Contains("low-detail", System.StringComparison.Ordinal)
                ? new Vector3(15.2f, 3.25f, 12f)
                : new Vector3(8.2f, 5f, 8.2f);
            AddModel(west, facadePath, $"FacadeWest{i}", facadeScale,
                new Vector3(2.9f, -5f, z), -Mathf.Pi / 2f);
            AddModel(east, facades[(i + 4) % facades.Length], $"FacadeEast{i}", facadeScale,
                new Vector3(-2.9f, -5f, z), Mathf.Pi / 2f);
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

        var meshInstance = FindMeshDescendant(prototype);
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

        var mesh = FindMeshDescendant(prototype)?.Mesh;
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

        AddParkedCar(region, "ParkedSedan", Cars + "sedan.glb",
            new Vector3(-13f, 0f, -16f), new Vector3(1.4f, 1.2f, 1.1f));
        AddParkedCar(region, "ParkedSuv", Cars + "suv.glb",
            new Vector3(13f, 0f, 15.2f), new Vector3(1.4f, 1.2f, 1.1f));
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
        AddModel(car, assetPath, "Visual", scale, new Vector3(0f, 0.36f, 0f));
    }

    private void AddModel(Node3D parent, string assetPath, string nodeName, Vector3 scale,
        Vector3 position, float yaw = 0f)
    {
        var packed = GD.Load<PackedScene>(assetPath);
        if (packed is null)
        {
            GD.PushError($"{Name}: asset urbano não carregou: {assetPath}");
            return;
        }

        var model = packed.Instantiate<Node3D>();
        model.Name = nodeName;
        model.Position = position;
        model.Rotation = new Vector3(0f, yaw, 0f);
        model.Scale = scale;
        parent.AddChild(model);
    }

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

    private static MeshInstance3D? FindMeshDescendant(Node root)
    {
        if (root is MeshInstance3D mesh)
            return mesh;

        for (var index = 0; index < root.GetChildCount(); index++)
        {
            if (FindMeshDescendant(root.GetChild(index)) is { } found)
                return found;
        }

        return null;
    }
}
