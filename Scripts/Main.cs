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
    private FastNoiseLite _detailNoise = null!;
    private FastNoiseLite _surfaceNoise = null!;
    private FastNoiseLite _biomeNoise = null!;
    private PlayerController _player = null!;
    private Label _hud = null!;
    private Label _debugHud = null!;
    private Control _menuRoot = null!;
    private PanelContainer _pauseMenu = null!;
    private PanelContainer _settingsMenu = null!;
    private Label _viewDistanceLabel = null!;
    private Vector2I _lastCenter = new(int.MinValue, int.MinValue);
    private StandardMaterial3D _terrainMaterial = null!;
    private StandardMaterial3D[] _trunkMaterials = null!;
    private StandardMaterial3D[] _leavesMaterials = null!;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
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

        _detailNoise = new FastNoiseLite
        {
            Seed = WorldSeed ^ 0x2c9277b5,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = 0.035f,
            FractalOctaves = 2,
            FractalGain = 0.45f,
            FractalLacunarity = 2.0f
        };

        _surfaceNoise = new FastNoiseLite
        {
            Seed = WorldSeed ^ 0x6d2b79f5,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = 0.22f,
            FractalOctaves = 2,
            FractalGain = 0.5f,
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
        _terrainMaterial.AlbedoTexture = CreateGroundTexture();

        _trunkMaterials = new[]
        {
            CreateMaterial(new Color(0.28f, 0.16f, 0.08f)),
            CreateMaterial(new Color(0.22f, 0.13f, 0.08f)),
            CreateMaterial(new Color(0.72f, 0.68f, 0.55f)),
            CreateMaterial(new Color(0.39f, 0.24f, 0.12f))
        };
        _leavesMaterials = new[]
        {
            CreateMaterial(new Color(0.16f, 0.34f, 0.13f)),
            CreateMaterial(new Color(0.10f, 0.25f, 0.12f)),
            CreateMaterial(new Color(0.27f, 0.39f, 0.17f)),
            CreateMaterial(new Color(0.34f, 0.39f, 0.15f))
        };

        CreateLighting();
        CreatePlayer();
        CreateHud();
        CreateMenus();
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

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F3)
        {
            _debugHud.Visible = !_debugHud.Visible;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventKey escape && escape.Pressed && !escape.Echo && escape.Keycode == Key.Escape)
        {
            if (_settingsMenu.Visible)
                ShowPauseMenu();
            else if (_pauseMenu.Visible)
                SetMenuOpen(false);
            else
                ShowPauseMenu();
            GetViewport().SetInputAsHandled();
        }
    }

    private static StandardMaterial3D CreateMaterial(Color color)
    {
        return new StandardMaterial3D { AlbedoColor = color, Roughness = 1.0f };
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
        AddChild(_player);
        _player.GetNode<Camera3D>("Camera3D").Far = ChunkSize * (ViewDistance + 1.0f);
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

    private void CreateMenus()
    {
        var menuLayer = new CanvasLayer
        {
            Layer = 10,
            ProcessMode = ProcessModeEnum.Always
        };
        _menuRoot = new Control
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Stop,
            ProcessMode = ProcessModeEnum.Always
        };
        _menuRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        menuLayer.AddChild(_menuRoot);

        var backdrop = new ColorRect
        {
            Color = new Color(0.015f, 0.025f, 0.035f, 0.78f),
            MouseFilter = Control.MouseFilterEnum.Stop
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _menuRoot.AddChild(backdrop);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _menuRoot.AddChild(center);

        _pauseMenu = new PanelContainer { CustomMinimumSize = new Vector2(420, 0) };
        center.AddChild(_pauseMenu);
        var pauseContent = new VBoxContainer();
        pauseContent.AddThemeConstantOverride("separation", 14);
        _pauseMenu.AddChild(pauseContent);
        pauseContent.AddChild(CreateMenuTitle("PAUSE"));
        pauseContent.AddChild(CreateMenuButton("Weiter", () => SetMenuOpen(false)));
        pauseContent.AddChild(CreateMenuButton("Einstellungen", ShowSettingsMenu));

        _settingsMenu = new PanelContainer
        {
            CustomMinimumSize = new Vector2(460, 0),
            Visible = false
        };
        center.AddChild(_settingsMenu);
        var settingsContent = new VBoxContainer();
        settingsContent.AddThemeConstantOverride("separation", 14);
        _settingsMenu.AddChild(settingsContent);
        settingsContent.AddChild(CreateMenuTitle("EINSTELLUNGEN"));

        var distanceTitle = new Label
        {
            Text = "Sichtweite",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        settingsContent.AddChild(distanceTitle);
        _viewDistanceLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        settingsContent.AddChild(_viewDistanceLabel);

        var distanceSlider = new HSlider
        {
            MinValue = 1,
            MaxValue = 5,
            Step = 1,
            Value = ViewDistance,
            CustomMinimumSize = new Vector2(360, 32),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        distanceSlider.ValueChanged += SetViewDistance;
        settingsContent.AddChild(distanceSlider);
        UpdateViewDistanceLabel();

        var buttonRow = new HBoxContainer();
        buttonRow.AddThemeConstantOverride("separation", 12);
        settingsContent.AddChild(buttonRow);
        buttonRow.AddChild(CreateMenuButton("Zurück", ShowPauseMenu));
        buttonRow.AddChild(CreateMenuButton("Weiter", () => SetMenuOpen(false)));

        AddChild(menuLayer);
    }

    private static Label CreateMenuTitle(string text)
    {
        var title = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 28);
        return title;
    }

    private static Button CreateMenuButton(string text, Action onPressed)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 42),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        button.Pressed += onPressed;
        return button;
    }

    private void ShowPauseMenu()
    {
        _pauseMenu.Visible = true;
        _settingsMenu.Visible = false;
        SetMenuOpen(true);
    }

    private void ShowSettingsMenu()
    {
        _pauseMenu.Visible = false;
        _settingsMenu.Visible = true;
    }

    private void SetMenuOpen(bool isOpen)
    {
        _menuRoot.Visible = isOpen;
        GetTree().Paused = isOpen;
        Input.MouseMode = isOpen ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
    }

    private void SetViewDistance(double value)
    {
        ViewDistance = Mathf.RoundToInt((float)value);
        _player.GetNode<Camera3D>("Camera3D").Far = ChunkSize * (ViewDistance + 1.0f);
        UpdateViewDistanceLabel();
        UpdateChunkTargets(true);
    }

    private void UpdateViewDistanceLabel()
    {
        _viewDistanceLabel.Text = $"{ViewDistance} Chunkringe · ca. {ViewDistance * ChunkSize} m";
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

        int sampleCount = ChunkSize + 1;
        float[,] heights = new float[sampleCount, sampleCount];
        for (int x = 0; x < sampleCount; x++)
        for (int z = 0; z < sampleCount; z++)
            heights[x, z] = GetHeight(coordinate.X * ChunkSize + x, coordinate.Y * ChunkSize + z);

        Color[,] terrainColors = new Color[sampleCount, sampleCount];
        for (int x = 0; x < sampleCount; x++)
        for (int z = 0; z < sampleCount; z++)
            terrainColors[x, z] = GetTerrainColor(
                heights, x, z,
                coordinate.X * ChunkSize + x,
                coordinate.Y * ChunkSize + z);

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);

        for (int x = 0; x < ChunkSize; x++)
        {
            for (int z = 0; z < ChunkSize; z++)
            {
                int worldX = coordinate.X * ChunkSize + x;
                int worldZ = coordinate.Y * ChunkSize + z;
                Vector3 p00 = new(x, heights[x, z], z);
                Vector3 p01 = new(x, heights[x, z + 1], z + 1);
                Vector3 p11 = new(x + 1, heights[x + 1, z + 1], z + 1);
                Vector3 p10 = new(x + 1, heights[x + 1, z], z);
                Color c00 = terrainColors[x, z];
                Color c01 = terrainColors[x, z + 1];
                Color c11 = terrainColors[x + 1, z + 1];
                Color c10 = terrainColors[x + 1, z];

                AddTerrainVertex(surface, p00, c00, new Vector2(worldX, worldZ));
                AddTerrainVertex(surface, p01, c01, new Vector2(worldX, worldZ + 1));
                AddTerrainVertex(surface, p11, c11, new Vector2(worldX + 1, worldZ + 1));
                AddTerrainVertex(surface, p00, c00, new Vector2(worldX, worldZ));
                AddTerrainVertex(surface, p11, c11, new Vector2(worldX + 1, worldZ + 1));
                AddTerrainVertex(surface, p10, c10, new Vector2(worldX + 1, worldZ));
            }
        }

        surface.Index();
        surface.GenerateNormals();

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
            MapWidth = ChunkSize + 1,
            MapDepth = ChunkSize + 1,
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

    private float[] CreateHeightMapData(float[,] heights)
    {
        int sampleCount = ChunkSize + 1;
        var data = new float[sampleCount * sampleCount];
        for (int z = 0; z < sampleCount; z++)
        for (int x = 0; x < sampleCount; x++)
            data[z * sampleCount + x] = heights[x, z];
        return data;
    }

    private static void AddTerrainVertex(SurfaceTool surface, Vector3 position, Color color, Vector2 uv)
    {
        surface.SetUV(uv);
        surface.SetColor(color);
        surface.AddVertex(position);
    }

    private ImageTexture CreateGroundTexture()
    {
        const int textureSize = 256;
        var random = new Random(WorldSeed ^ 0x3ab17);
        float[,] broadNoise = CreateNoiseGrid(random, textureSize / 64);
        float[,] patchNoise = CreateNoiseGrid(random, textureSize / 16);
        float[,] detailNoise = CreateNoiseGrid(random, textureSize / 4);
        float[,] grainNoise = CreateNoiseGrid(random, textureSize);
        byte[] pixels = new byte[textureSize * textureSize * 3];

        for (int y = 0; y < textureSize; y++)
        for (int x = 0; x < textureSize; x++)
        {
            float broad = SampleTileNoise(broadNoise, x, y, 64);
            float patch = SampleTileNoise(patchNoise, x, y, 16);
            float detail = SampleTileNoise(detailNoise, x, y, 4);
            float grain = SampleTileNoise(grainNoise, x, y, 1);
            float value = Mathf.Clamp(
                0.82f + (broad - 0.5f) * 0.18f + (patch - 0.5f) * 0.14f +
                (detail - 0.5f) * 0.10f + (grain - 0.5f) * 0.06f,
                0.62f, 1.0f);
            int pixel = (y * textureSize + x) * 3;
            pixels[pixel] = (byte)Mathf.RoundToInt(value * 0.96f * 255.0f);
            pixels[pixel + 1] = (byte)Mathf.RoundToInt(value * 255.0f);
            pixels[pixel + 2] = (byte)Mathf.RoundToInt(value * 0.88f * 255.0f);
        }

        Image image = Image.CreateFromData(textureSize, textureSize, false, Image.Format.Rgb8, pixels);
        return ImageTexture.CreateFromImage(image);
    }

    private static float[,] CreateNoiseGrid(Random random, int size)
    {
        var grid = new float[size, size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
            grid[x, y] = (float)random.NextDouble();
        return grid;
    }

    private static float SampleTileNoise(float[,] grid, int x, int y, int cellSize)
    {
        int gridSize = grid.GetLength(0);
        float gx = (float)x / cellSize;
        float gy = (float)y / cellSize;
        int x0 = Mathf.FloorToInt(gx) % gridSize;
        int y0 = Mathf.FloorToInt(gy) % gridSize;
        int x1 = (x0 + 1) % gridSize;
        int y1 = (y0 + 1) % gridSize;
        float tx = gx - Mathf.Floor(gx);
        float ty = gy - Mathf.Floor(gy);
        tx = tx * tx * (3.0f - 2.0f * tx);
        ty = ty * ty * (3.0f - 2.0f * ty);
        float top = Mathf.Lerp(grid[x0, y0], grid[x1, y0], tx);
        float bottom = Mathf.Lerp(grid[x0, y1], grid[x1, y1], tx);
        return Mathf.Lerp(top, bottom, ty);
    }

    private Color GetTerrainColor(float[,] heights, int x, int z, int worldX, int worldZ)
    {
        float left = x > 0 ? heights[x - 1, z] : GetHeight(worldX - 1, worldZ);
        float right = x < ChunkSize ? heights[x + 1, z] : GetHeight(worldX + 1, worldZ);
        float back = z > 0 ? heights[x, z - 1] : GetHeight(worldX, worldZ - 1);
        float forward = z < ChunkSize ? heights[x, z + 1] : GetHeight(worldX, worldZ + 1);
        float slope = Mathf.Max(Mathf.Abs(right - left), Mathf.Abs(forward - back)) * 0.5f;

        Color grass = GetGrassColor(worldX, worldZ);
        float groundVariation = _surfaceNoise.GetNoise2D(worldX, worldZ);
        Color soil = new(0.39f, 0.34f, 0.24f);
        grass = grass.Lerp(soil, Mathf.Clamp(0.08f + groundVariation * 0.12f, 0.0f, 0.22f));
        grass *= 0.94f + _detailNoise.GetNoise2D(worldX, worldZ) * 0.08f;
        Color rock = new(0.38f, 0.37f, 0.34f);
        float rockAmount = Mathf.Clamp((slope - 1.4f) * 0.42f, 0.0f, 0.72f);
        Color terrain = grass.Lerp(rock, rockAmount);
        Color snow = new(0.78f, 0.80f, 0.76f);
        float snowAmount = Mathf.Clamp((heights[x, z] - 36.0f) * 0.38f, 0.0f, 0.72f);
        return terrain.Lerp(snow, snowAmount);
    }

    private float GetHeight(int worldX, int worldZ)
    {
        float noise = _terrainNoise.GetNoise2D(worldX, worldZ);
        float detail = _detailNoise.GetNoise2D(worldX, worldZ);
        return 26.0f + noise * 12.0f + detail * 1.5f;
    }

    private Color GetGrassColor(int worldX, int worldZ)
    {
        float biome = _biomeNoise.GetNoise2D(worldX, worldZ);
        if (biome < -0.42f)
            return new Color(0.49f, 0.40f, 0.25f); // dry scrub
        if (biome < -0.18f)
            return new Color(0.43f, 0.42f, 0.24f); // steppe
        if (biome > 0.48f)
            return new Color(0.17f, 0.31f, 0.13f); // dense forest
        if (biome > 0.24f)
            return new Color(0.25f, 0.39f, 0.18f); // meadow
        return new Color(0.31f, 0.44f, 0.21f); // grassland
    }

    private void AddTrees(Node3D chunk, Vector2I coordinate, float[,] heights)
    {
        int treeSeed = unchecked(WorldSeed ^ (coordinate.X * 73856093) ^ (coordinate.Y * 19349663));
        var random = new Random(treeSeed);
        var trunkTransforms = new List<Transform3D>[4];
        var leafTransforms = new List<Transform3D>[4];
        for (int type = 0; type < 4; type++)
        {
            trunkTransforms[type] = new List<Transform3D>();
            leafTransforms[type] = new List<Transform3D>();
        }

        for (int attempt = 0; attempt < 24; attempt++)
        {
            int x = random.Next(1, ChunkSize - 1);
            int z = random.Next(1, ChunkSize - 1);
            float h = (heights[x, z] + heights[x + 1, z] + heights[x, z + 1] + heights[x + 1, z + 1]) * 0.25f;

            if (random.NextDouble() > 0.48)
                continue;

            if (Math.Abs(h - heights[x - 1, z]) > 1.5f ||
                Math.Abs(h - heights[x + 1, z]) > 1.5f ||
                Math.Abs(h - heights[x, z - 1]) > 1.5f ||
                Math.Abs(h - heights[x, z + 1]) > 1.5f)
                continue;

            float px = x + 0.5f;
            float pz = z + 0.5f;
            float biome = _biomeNoise.GetNoise2D(coordinate.X * ChunkSize + x, coordinate.Y * ChunkSize + z);
            int treeType = ChooseTreeType(biome, random);
            float trunkHeight = treeType switch { 1 => 5.0f, 2 => 4.4f, 3 => 4.3f, _ => 3.4f };
            trunkTransforms[treeType].Add(new Transform3D(
                Basis.Identity,
                new Vector3(px, h + trunkHeight * 0.5f, pz)));
            AddTreeCanopy(leafTransforms[treeType], treeType, new Vector3(px, h, pz), random);
        }

        for (int type = 0; type < 4; type++)
            AddTreeInstances(chunk, type, trunkTransforms[type], leafTransforms[type]);
    }

    private static int ChooseTreeType(float biome, Random random)
    {
        double variety = random.NextDouble();
        if (biome < -0.25f)
            return variety < 0.55 ? 3 : variety < 0.82 ? 0 : 1;
        if (biome > 0.35f)
            return random.Next(0, 3);
        return random.Next(0, 4);
    }

    private static void AddTreeCanopy(List<Transform3D> transforms, int type, Vector3 origin, Random random)
    {
        Vector3[] offsets;
        Vector3[] scales;
        switch (type)
        {
            case 1: // layered pine
                offsets = new[]
                {
                    new Vector3(0, 5.0f, 0),
                    new Vector3(0.30f, 4.35f, 0), new Vector3(-0.28f, 4.30f, 0.12f),
                    new Vector3(0, 3.72f, 0.34f), new Vector3(0, 3.70f, -0.32f),
                    new Vector3(0.42f, 3.08f, 0), new Vector3(-0.40f, 3.05f, 0),
                    new Vector3(0, 2.55f, 0)
                };
                scales = new[]
                {
                    new Vector3(0.48f, 0.80f, 0.48f),
                    new Vector3(0.63f, 0.75f, 0.62f), new Vector3(0.66f, 0.73f, 0.63f),
                    new Vector3(0.76f, 0.77f, 0.76f), new Vector3(0.74f, 0.75f, 0.72f),
                    new Vector3(0.84f, 0.78f, 0.82f), new Vector3(0.82f, 0.76f, 0.84f),
                    new Vector3(0.88f, 0.78f, 0.86f)
                };
                break;
            case 2: // narrow birch crown
                offsets = new[]
                {
                    new Vector3(0, 5.45f, 0), new Vector3(0.08f, 4.75f, 0),
                    new Vector3(-0.08f, 4.05f, 0.05f), new Vector3(0, 3.42f, -0.04f)
                };
                scales = new[]
                {
                    new Vector3(0.68f, 0.98f, 0.68f), new Vector3(0.72f, 1.0f, 0.72f),
                    new Vector3(0.78f, 0.98f, 0.76f), new Vector3(0.82f, 0.86f, 0.80f)
                };
                break;
            case 3: // flat acacia canopy
                offsets = new[]
                {
                    new Vector3(0, 4.15f, 0), new Vector3(0.68f, 4.05f, 0.10f),
                    new Vector3(-0.65f, 4.10f, -0.12f), new Vector3(0.05f, 4.02f, 0.68f),
                    new Vector3(-0.08f, 4.12f, -0.62f)
                };
                scales = new[]
                {
                    new Vector3(1.05f, 0.46f, 1.02f), new Vector3(0.80f, 0.48f, 0.78f),
                    new Vector3(0.82f, 0.46f, 0.79f), new Vector3(0.78f, 0.45f, 0.82f),
                    new Vector3(0.78f, 0.44f, 0.80f)
                };
                break;
            default: // broad, irregular oak
                offsets = new[]
                {
                    new Vector3(0, 4.0f, 0), new Vector3(0.48f, 3.55f, 0.10f),
                    new Vector3(-0.44f, 3.45f, 0.24f), new Vector3(0.08f, 3.38f, -0.48f),
                    new Vector3(-0.18f, 3.30f, -0.30f), new Vector3(0.18f, 4.02f, 0.50f)
                };
                scales = new[]
                {
                    new Vector3(1.0f, 1.25f, 1.0f), new Vector3(0.85f, 0.95f, 0.86f),
                    new Vector3(0.87f, 0.91f, 0.88f), new Vector3(0.83f, 0.94f, 0.86f),
                    new Vector3(0.92f, 0.90f, 0.90f), new Vector3(0.78f, 0.82f, 0.82f)
                };
                break;
        }

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 scale = scales[i] * (0.88f + (float)random.NextDouble() * 0.24f);
            Vector3 jitter = new(
                ((float)random.NextDouble() - 0.5f) * 0.24f,
                ((float)random.NextDouble() - 0.5f) * 0.16f,
                ((float)random.NextDouble() - 0.5f) * 0.24f);
            transforms.Add(new Transform3D(
                Basis.FromScale(scale),
                origin + offsets[i] + jitter));
        }
    }

    private void AddTreeInstances(Node3D chunk, int type, List<Transform3D> trunks, List<Transform3D> leaves)
    {
        if (trunks.Count == 0)
            return;

        float trunkHeight = type switch { 1 => 5.0f, 2 => 4.4f, 3 => 4.3f, _ => 3.4f };
        var trunkMesh = new CylinderMesh
        {
            TopRadius = type == 2 ? 0.08f : type == 1 ? 0.11f : 0.15f,
            BottomRadius = type == 2 ? 0.13f : type == 1 ? 0.17f : 0.22f,
            Height = trunkHeight,
            RadialSegments = 8,
            Rings = 1
        };
        var trunkMultiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = trunkMesh,
            InstanceCount = trunks.Count
        };
        for (int i = 0; i < trunks.Count; i++)
            trunkMultiMesh.SetInstanceTransform(i, trunks[i]);
        chunk.AddChild(new MultiMeshInstance3D
        {
            Name = $"TreeTrunks_{type}",
            Multimesh = trunkMultiMesh,
            MaterialOverride = _trunkMaterials[type]
        });

        var leafMesh = new SphereMesh
        {
            Radius = 1.0f,
            Height = 2.0f,
            RadialSegments = 12,
            Rings = 8
        };
        var leafMultiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = leafMesh,
            InstanceCount = leaves.Count
        };
        for (int i = 0; i < leaves.Count; i++)
            leafMultiMesh.SetInstanceTransform(i, leaves[i]);
        chunk.AddChild(new MultiMeshInstance3D
        {
            Name = $"TreeCanopies_{type}",
            Multimesh = leafMultiMesh,
            MaterialOverride = _leavesMaterials[type]
        });
    }
}
