using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Main : Node3D
{
    [Export] public int WorldSeed { get; set; } = 0;
    [Export(PropertyHint.Range, "1,5,1")] public int ViewDistance { get; set; } = 2;
    [Export(PropertyHint.Range, "8,32,1")] public int ChunkSize { get; set; } = 16;

    private readonly Dictionary<Vector2I, Node3D> _chunks = new();
    private readonly HashSet<Vector2I> _wantedChunks = new();
    private Queue<Vector2I> _pendingChunks = new();
    private FastNoiseLite _terrainNoise = null!;
    private FastNoiseLite _biomeNoise = null!;
    private PlayerController _player = null!;
    private Label _hud = null!;
    private Label _debugHud = null!;
    private Vector2I _lastCenter = new(int.MinValue, int.MinValue);
    private StandardMaterial3D _terrainMaterial = null!;
    private StandardMaterial3D _trunkMaterial = null!;
    private StandardMaterial3D _leavesMaterial = null!;

    public override void _Ready()
    {
        AddInputAction("move_forward", Key.W);
        AddInputAction("move_back", Key.S);
        AddInputAction("move_left", Key.A);
        AddInputAction("move_right", Key.D);
        AddInputAction("jump", Key.Space);

        if (WorldSeed == 0)
            WorldSeed = unchecked((int)GD.Randi());

        _terrainNoise = new FastNoiseLite
        {
            Seed = WorldSeed,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = 0.008f,
            FractalOctaves = 4,
            FractalGain = 0.48f,
            FractalLacunarity = 2.0f
        };

        _biomeNoise = new FastNoiseLite
        {
            Seed = WorldSeed ^ 0x5f3759df,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = 0.0025f,
            FractalOctaves = 2
        };

        _terrainMaterial = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 1.0f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };

        _trunkMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.28f, 0.16f, 0.08f),
            Roughness = 1.0f
        };

        _leavesMaterial = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.15f, 0.30f, 0.12f),
            Roughness = 1.0f
        };

        CreateLighting();
        CreatePlayer();
        CreateHud();
        UpdateChunkTargets(true);
    }

    public override void _Process(double delta)
    {
        if (_player == null)
            return;

        Vector2I center = GetPlayerChunk();
        if (center != _lastCenter)
            UpdateChunkTargets(false);

        // Build no more than one chunk per frame to keep generation manageable.
        while (_pendingChunks.Count > 0)
        {
            Vector2I next = _pendingChunks.Dequeue();
            if (!_wantedChunks.Contains(next) || _chunks.ContainsKey(next))
                continue;

            Node3D chunk = BuildChunk(next);
            AddChild(chunk);
            _chunks[next] = chunk;
            break;
        }

        if (_hud != null)
        {
            _hud.Text = "THE NORLOLAND  |  Seed: " + WorldSeed +
                        "  |  Chunks: " + _chunks.Count +
                        "  |  WASD move  |  Space jump  |  F3 debug  |  Esc release mouse";
        }

        if (_debugHud != null && _debugHud.Visible)
        {
            Vector3 position = _player.GlobalPosition;
            _debugHud.Text = "DEBUG (F3)" +
                "\nFPS: " + Engine.GetFramesPerSecond() +
                "\nPosition: " + position.X.ToString("F1") + ", " + position.Y.ToString("F1") + ", " + position.Z.ToString("F1") +
                "\nChunk: " + center.X + ", " + center.Y +
                "\nChunks loaded: " + _chunks.Count + " / " + _wantedChunks.Count +
                "  |  Queued: " + _pendingChunks.Count +
                "\nView radius: " + ViewDistance + " chunks";
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F3)
        {
            _debugHud.Visible = !_debugHud.Visible;
            GetViewport().SetInputAsHandled();
        }
    }

    private static void AddInputAction(string action, Key key)
    {
        if (!InputMap.HasAction(action))
            InputMap.AddAction(action);

        var keyEvent = new InputEventKey { PhysicalKeycode = key };
        InputMap.ActionAddEvent(action, keyEvent);
    }

    private void CreateLighting()
    {
        var worldEnvironment = new WorldEnvironment();
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.48f, 0.66f, 0.78f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.72f, 0.77f, 0.82f),
            AmbientLightEnergy = 0.7f
        };
        worldEnvironment.Environment = environment;
        AddChild(worldEnvironment);

        var sun = new DirectionalLight3D
        {
            Name = "Sun",
            RotationDegrees = new Vector3(-48, -32, 0),
            LightEnergy = 1.15f,
            ShadowEnabled = true
        };
        AddChild(sun);
    }

    private void CreatePlayer()
    {
        _player = new PlayerController
        {
            Position = new Vector3(0, 55, 0)
        };
        _player.GetNode<Camera3D>("Camera3D").Far = ChunkSize * (ViewDistance + 1.0f);
        AddChild(_player);
    }

    private void CreateHud()
    {
        var layer = new CanvasLayer();
        _hud = new Label
        {
            Position = new Vector2(16, 16),
            Text = "Generating world...",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _hud.AddThemeFontSizeOverride("font_size", 16);
        _hud.AddThemeColorOverride("font_color", Colors.White);
        _hud.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
        _hud.AddThemeConstantOverride("shadow_offset_x", 1);
        _hud.AddThemeConstantOverride("shadow_offset_y", 1);
        layer.AddChild(_hud);

        _debugHud = new Label
        {
            Position = new Vector2(16, 78),
            Text = "DEBUG",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _debugHud.AddThemeFontSizeOverride("font_size", 16);
        _debugHud.AddThemeColorOverride("font_color", Colors.White);
        _debugHud.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        _debugHud.AddThemeConstantOverride("shadow_offset_x", 1);
        _debugHud.AddThemeConstantOverride("shadow_offset_y", 1);
        layer.AddChild(_debugHud);
        AddChild(layer);
    }

    private Vector2I GetPlayerChunk()
    {
        int x = Mathf.FloorToInt(_player.GlobalPosition.X / ChunkSize);
        int z = Mathf.FloorToInt(_player.GlobalPosition.Z / ChunkSize);
        return new Vector2I(x, z);
    }

    private void UpdateChunkTargets(bool force)
    {
        Vector2I center = GetPlayerChunk();
        if (!force && center == _lastCenter)
            return;

        _lastCenter = center;
        _wantedChunks.Clear();

        for (int x = center.X - ViewDistance; x <= center.X + ViewDistance; x++)
        {
            for (int z = center.Y - ViewDistance; z <= center.Y + ViewDistance; z++)
            {
                Vector2I offset = new(x - center.X, z - center.Y);
                if (offset.LengthSquared() <= ViewDistance * ViewDistance)
                    _wantedChunks.Add(new Vector2I(x, z));
            }
        }

        foreach (Vector2I key in _chunks.Keys.ToArray())
        {
            if (_wantedChunks.Contains(key))
                continue;

            _chunks[key].QueueFree();
            _chunks.Remove(key);
        }

        _pendingChunks = new Queue<Vector2I>(
            _wantedChunks
                .Where(key => !_chunks.ContainsKey(key))
                .OrderBy(key => key.DistanceSquaredTo(center)));
    }

    private Node3D BuildChunk(Vector2I coordinate)
    {
        var chunk = new Node3D
        {
            Name = "Chunk_" + coordinate.X + "_" + coordinate.Y,
            Position = new Vector3(coordinate.X * ChunkSize, 0, coordinate.Y * ChunkSize)
        };

        int[,] heights = new int[ChunkSize, ChunkSize];
        for (int x = 0; x < ChunkSize; x++)
        for (int z = 0; z < ChunkSize; z++)
            heights[x, z] = GetHeight(coordinate.X * ChunkSize + x, coordinate.Y * ChunkSize + z);

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);

        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                int worldX = coordinate.X * ChunkSize + x;
                int worldZ = coordinate.Y * ChunkSize + z;
                int h = heights[x, z];

                Color topColor = GetGrassColor(worldX, worldZ);
                AddQuad(surface,
                    new Vector3(x, h, z),
                    new Vector3(x, h, z + 1),
                    new Vector3(x + 1, h, z + 1),
                    new Vector3(x + 1, h, z),
                    Vector3.Up, topColor);

                AddWall(surface, x, z, h, GetHeight(worldX - 1, worldZ), WallDirection.NegativeX);
                AddWall(surface, x, z, h, GetHeight(worldX + 1, worldZ), WallDirection.PositiveX);
                AddWall(surface, x, z, h, GetHeight(worldX, worldZ - 1), WallDirection.NegativeZ);
                AddWall(surface, x, z, h, GetHeight(worldX, worldZ + 1), WallDirection.PositiveZ);
            }
        }

        ArrayMesh mesh = surface.Commit() as ArrayMesh;
        var terrain = new MeshInstance3D
        {
            Name = "TerrainMesh",
            Mesh = mesh,
            MaterialOverride = _terrainMaterial
        };
        chunk.AddChild(terrain);

        // A single heightmap collider per chunk avoids building a triangle mesh collider
        // from every rendered terrain face. The height samples use the same grid as the mesh.
        var heightMap = new HeightMapShape3D
        {
            MapWidth = ChunkSize,
            MapDepth = ChunkSize,
            MapData = CreateHeightMapData(heights)
        };
        var collision = new StaticBody3D { Name = "TerrainCollision" };
        collision.AddChild(new CollisionShape3D
        {
            Shape = heightMap,
            Position = new Vector3(ChunkSize * 0.5f, 0, ChunkSize * 0.5f)
        });
        chunk.AddChild(collision);

        AddTrees(chunk, coordinate, heights);
        return chunk;
    }

    private float[] CreateHeightMapData(int[,] heights)
    {
        var data = new float[ChunkSize * ChunkSize];
        for (int z = 0; z < ChunkSize; z++)
        for (int x = 0; x < ChunkSize; x++)
            data[z * ChunkSize + x] = heights[x, z];
        return data;
    }

    private enum WallDirection { NegativeX, PositiveX, NegativeZ, PositiveZ }

    private void AddWall(SurfaceTool surface, int x, int z, int height, int neighbourHeight, WallDirection direction)
    {
        if (height <= neighbourHeight)
            return;

        for (int y = neighbourHeight; y < height; y++)
        {
            Color color = y >= height - 3
                ? new Color(0.34f, 0.23f, 0.13f)
                : new Color(0.36f, 0.37f, 0.36f);

            Vector3 a, b, c, d, normal;
            switch (direction)
            {
                case WallDirection.NegativeX:
                    a = new Vector3(x, y, z + 1);
                    b = new Vector3(x, y + 1, z + 1);
                    c = new Vector3(x, y + 1, z);
                    d = new Vector3(x, y, z);
                    normal = Vector3.Left;
                    break;
                case WallDirection.PositiveX:
                    a = new Vector3(x + 1, y, z);
                    b = new Vector3(x + 1, y + 1, z);
                    c = new Vector3(x + 1, y + 1, z + 1);
                    d = new Vector3(x + 1, y, z + 1);
                    normal = Vector3.Right;
                    break;
                case WallDirection.NegativeZ:
                    a = new Vector3(x, y, z);
                    b = new Vector3(x, y + 1, z);
                    c = new Vector3(x + 1, y + 1, z);
                    d = new Vector3(x + 1, y, z);
                    normal = Vector3.Back;
                    break;
                default:
                    a = new Vector3(x + 1, y, z + 1);
                    b = new Vector3(x + 1, y + 1, z + 1);
                    c = new Vector3(x, y + 1, z + 1);
                    d = new Vector3(x, y, z + 1);
                    normal = Vector3.Forward;
                    break;
            }

            AddQuad(surface, a, b, c, d, normal, color);
        }
    }

    private static void AddQuad(
        SurfaceTool surface,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        Vector3 normal, Color color)
    {
        surface.SetColor(color);
        surface.SetNormal(normal);
        surface.AddVertex(a);
        surface.SetColor(color);
        surface.SetNormal(normal);
        surface.AddVertex(b);
        surface.SetColor(color);
        surface.SetNormal(normal);
        surface.AddVertex(c);

        surface.SetColor(color);
        surface.SetNormal(normal);
        surface.AddVertex(a);
        surface.SetColor(color);
        surface.SetNormal(normal);
        surface.AddVertex(c);
        surface.SetColor(color);
        surface.SetNormal(normal);
        surface.AddVertex(d);
    }

    private int GetHeight(int worldX, int worldZ)
    {
        float noise = _terrainNoise.GetNoise2D(worldX, worldZ);
        return 14 + Mathf.RoundToInt((noise + 1.0f) * 12.0f);
    }

    private Color GetGrassColor(int worldX, int worldZ)
    {
        float biome = _biomeNoise.GetNoise2D(worldX, worldZ);
        if (biome < -0.25f)
            return new Color(0.43f, 0.39f, 0.23f); // dry grass
        if (biome > 0.35f)
            return new Color(0.20f, 0.34f, 0.16f); // deep green
        return new Color(0.29f, 0.43f, 0.20f);
    }

    private void AddTrees(Node3D chunk, Vector2I coordinate, int[,] heights)
    {
        int treeSeed = unchecked(WorldSeed ^ (coordinate.X * 73856093) ^ (coordinate.Y * 19349663));
        var random = new Random(treeSeed);
        var trunkTransforms = new List<Transform3D>();
        var leafTransforms = new List<Transform3D>();

        for (int attempt = 0; attempt < 18; attempt++)
        {
            int x = random.Next(1, ChunkSize - 1);
            int z = random.Next(1, ChunkSize - 1);
            int h = heights[x, z];

            if (random.NextDouble() > 0.58)
                continue;

            if (Math.Abs(h - heights[x - 1, z]) > 2 ||
                Math.Abs(h - heights[x + 1, z]) > 2 ||
                Math.Abs(h - heights[x, z - 1]) > 2 ||
                Math.Abs(h - heights[x, z + 1]) > 2)
                continue;

            float px = x + 0.5f;
            float pz = z + 0.5f;
            trunkTransforms.Add(new Transform3D(Basis.Identity, new Vector3(px, h + 1.6f, pz)));
            Basis leafBasis = Basis.FromScale(new Vector3(1.15f, 1.45f, 1.15f));
            leafTransforms.Add(new Transform3D(leafBasis, new Vector3(px, h + 3.4f, pz)));
        }

        if (trunkTransforms.Count == 0)
            return;

        var trunkMesh = new CylinderMesh
        {
            TopRadius = 0.13f,
            BottomRadius = 0.20f,
            Height = 3.2f,
            RadialSegments = 6,
            Rings = 1
        };

        var trunkMultiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = trunkMesh,
            InstanceCount = trunkTransforms.Count
        };
        for (int i = 0; i < trunkTransforms.Count; i++)
            trunkMultiMesh.SetInstanceTransform(i, trunkTransforms[i]);

        var trunks = new MultiMeshInstance3D
        {
            Name = "TreeTrunks",
            Multimesh = trunkMultiMesh,
            MaterialOverride = _trunkMaterial
        };
        chunk.AddChild(trunks);

        var leafMesh = new SphereMesh
        {
            Radius = 1.0f,
            Height = 2.0f,
            RadialSegments = 7,
            Rings = 4
        };
        var leafMultiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = leafMesh,
            InstanceCount = leafTransforms.Count
        };
        for (int i = 0; i < leafTransforms.Count; i++)
            leafMultiMesh.SetInstanceTransform(i, leafTransforms[i]);

        var leaves = new MultiMeshInstance3D
        {
            Name = "TreeCanopies",
            Multimesh = leafMultiMesh,
            MaterialOverride = _leavesMaterial
        };
        chunk.AddChild(leaves);
    }
}
