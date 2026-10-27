using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Main : Node3D
{
    public const string AppVersion = "0.1.11";

    private sealed class ChunkBuildJob
    {
        public Vector2I Coordinate;
        public Node3D Chunk;
        public float[,] Heights;
        public int Stage;
    }

    private ChunkBuildJob _chunkBuildJob;

    [Export] public int WorldSeed { get; set; } = 0;
    [Export(PropertyHint.Range, "1,4,1")] public int ViewDistance { get; set; } = 2;
    [Export] public bool AutoScaleViewDistance { get; set; } = true;
    [Export(PropertyHint.Range, "30,120,1")] public int TargetFps { get; set; } = 60;
    [Export(PropertyHint.Range, "8,32,1")] public int ChunkSize { get; set; } = 16;

    private readonly Dictionary<Vector2I, Node3D> _chunks = new();
    private readonly Dictionary<Vector2I, MeshInstance3D> _terrainMeshes = new();
    private readonly Dictionary<Vector2I, CollisionShape3D> _terrainColliders = new();
    private readonly Dictionary<Vector2I, float> _excavationDepth = new();
    private readonly Dictionary<Vector2I, float> _grassCover = new();
    private readonly HashSet<Vector2I> _wantedChunks = new();
    private Queue<Vector2I> _pendingChunks = new();
    private static readonly Color[] FlowerColors =
    {
        new Color(0.94f, 0.91f, 0.79f),
        new Color(0.96f, 0.78f, 0.18f),
        new Color(0.77f, 0.52f, 0.80f)
    };
    private FastNoiseLite _terrainNoise = null!;
    private FastNoiseLite _detailNoise = null!;
    private FastNoiseLite _surfaceNoise = null!;
    private FastNoiseLite _biomeNoise = null!;
    private FastNoiseLite _mountainNoise = null!;
    private PlayerController _player = null!;
    private Label _hud = null!;
    private Label _debugHud = null!;
    private Control _menuRoot = null!;
    private PanelContainer _pauseMenu = null!;
    private PanelContainer _settingsMenu = null!;
    private Label _viewDistanceLabel = null!;
    private SurvivalGameplay _survival = null!;
    private Vector2I _lastCenter = new(int.MinValue, int.MinValue);
    private StandardMaterial3D _terrainMaterial = null!;
    private ImageTexture _soilTexture = null!;
    private StandardMaterial3D[] _trunkMaterials = null!;
    private StandardMaterial3D[] _leavesMaterials = null!;
    private StandardMaterial3D _grassMaterial = null!;
    private StandardMaterial3D _shrubMaterial = null!;
    private StandardMaterial3D _shrubTwigMaterial = null!;
    private StandardMaterial3D _flowerStemMaterial = null!;
    private StandardMaterial3D _flowerCenterMaterial = null!;
    private StandardMaterial3D[] _flowerMaterials = null!;
    private Mesh _grassMesh = null!;
    private Mesh _shortGrassMesh = null!;
    private Mesh _shrubLeafMesh = null!;
    private Mesh _flowerPetalMesh = null!;
    // Mesh geometry is immutable; each chunk retains its own instance transforms.
    private readonly Mesh[] _treeTrunkMeshes = new Mesh[4];
    private Mesh _treeLeafMesh;
    private double _hudRefreshTimer;
    private readonly Dictionary<Vector2I, float> _waterLabGround = new();
    private readonly HashSet<Vector2I> _waterLabChunks = new();
    private WaterSandbox _waterLab;
    private bool _debugBeforeWaterLab;
    private bool _escapeWasPressed;
    private float _grassRegrowthTimer;
    private double _saveTimer;
    private float _adaptiveViewTimer;
    private int _adaptiveGoodFrames;
    private int _adaptiveBadFrames;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        AddInputAction("move_forward", Key.W);
        AddInputAction("move_back", Key.S);
        AddInputAction("move_left", Key.A);
        AddInputAction("move_right", Key.D);
        AddInputAction("jump", Key.Space);
        AddInputAction("pause_menu", Key.Escape);

        WorldSeed = WorldSave.Load(WorldSeed);
        RestoreTerrainChanges();

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

        _mountainNoise = new FastNoiseLite
        {
            Seed = WorldSeed ^ 0x1b873593,
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = 0.0045f,
            FractalType = FastNoiseLite.FractalTypeEnum.Ridged,
            FractalOctaves = 3,
            FractalGain = 0.5f,
            FractalLacunarity = 2.0f
        };

        _terrainMaterial = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            Roughness = 1.0f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        _terrainMaterial.AlbedoTexture = GD.Load<Texture2D>("res://Assets/Textures/forest_floor.png") ?? CreateGroundTexture();
        _terrainMaterial.Uv1Scale = new Vector3(0.16f, 1.0f, 0.16f);
        _soilTexture = CreateSoilTexture();

        Texture2D barkTexture = GD.Load<Texture2D>("res://Assets/Textures/tree_bark.png") ?? CreateBarkTexture();
        Texture2D foliageTexture = GD.Load<Texture2D>("res://Assets/Textures/forest_leaves.png");

        _trunkMaterials = new[]
        {
            CreateMaterial(new Color(0.58f, 0.43f, 0.28f), barkTexture),
            CreateMaterial(new Color(0.42f, 0.30f, 0.19f), barkTexture),
            CreateMaterial(new Color(0.66f, 0.51f, 0.32f), barkTexture),
            CreateMaterial(new Color(0.50f, 0.36f, 0.21f), barkTexture)
        };
        _leavesMaterials = new[]
        {
            CreateMaterial(new Color(0.42f, 0.58f, 0.30f), foliageTexture),
            CreateMaterial(new Color(0.30f, 0.47f, 0.22f), foliageTexture),
            CreateMaterial(new Color(0.50f, 0.64f, 0.31f), foliageTexture),
            CreateMaterial(new Color(0.36f, 0.50f, 0.20f), foliageTexture)
        };
        foreach (StandardMaterial3D material in _leavesMaterials)
            material.VertexColorUseAsAlbedo = true;

        _grassMaterial = CreateMaterial(Colors.White);
        _grassMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        _grassMaterial.VertexColorUseAsAlbedo = true;
        _shrubMaterial = CreateMaterial(Colors.White);
        _shrubMaterial.AlbedoTexture = foliageTexture;
        _shrubMaterial.Uv1Scale = new Vector3(0.45f, 0.45f, 1.0f);
        _shrubMaterial.VertexColorUseAsAlbedo = true;
        _shrubMaterial.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        _shrubTwigMaterial = CreateMaterial(new Color(0.27f, 0.22f, 0.13f));
        _flowerStemMaterial = CreateMaterial(new Color(0.18f, 0.38f, 0.12f));
        _flowerCenterMaterial = CreateMaterial(new Color(0.95f, 0.68f, 0.13f));
        _flowerMaterials = new[]
        {
            CreateMaterial(FlowerColors[0]),
            CreateMaterial(FlowerColors[1]),
            CreateMaterial(FlowerColors[2])
        };
        _flowerMaterials[0].CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        _flowerMaterials[1].CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        _flowerMaterials[2].CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        _grassMesh = CreateGrassMesh();
        _shortGrassMesh = CreateGrassMesh(0.56f, 1.15f);
        _shrubLeafMesh = CreateShrubLeafMesh();
        _flowerPetalMesh = CreateFlowerPetalMesh();

        CreateLighting();
        CreatePlayer();
        CreateHud();
        _survival = new SurvivalGameplay { Name = "SurvivalGameplay" };
        AddChild(_survival);
        _survival.Initialize(this, _player);
        CreateMenus();
        UpdateChunkTargets(true);
        if (OS.GetCmdlineUserArgs().Contains("--water-preview"))
        {
            // CI captures a fixed-size window, independent of the desktop mode.
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetSize(new Vector2I(1280, 720));
            ToggleWaterLabor();
        }
    }

    public override void _Process(double delta)
    {
        if (_player == null)
            return;

        bool escapePressed = Input.IsKeyPressed(Key.Escape) || Input.IsActionPressed("pause_menu");
        if (escapePressed && !_escapeWasPressed)
            TogglePauseMenu();
        _escapeWasPressed = escapePressed;
        Vector2I center = GetPlayerChunk();
        if (!GetTree().Paused)
        {
            if (AutoScaleViewDistance)
            {
                _adaptiveViewTimer += (float)delta;
                if (_adaptiveViewTimer >= 3.0f)
                {
                    AdaptViewDistance();
                    _adaptiveViewTimer = 0.0f;
                }
            }
            _saveTimer += delta;
            if (_saveTimer >= 5.0)
            {
                SaveWorldChanges();
                _saveTimer = 0.0;
            }
            _grassRegrowthTimer += (float)delta;
            if (_grassRegrowthTimer >= 5.0f)
            {
                RegrowGroundCover(_grassRegrowthTimer);
                _grassRegrowthTimer = 0.0f;
            }
            if (center != _lastCenter)
                UpdateChunkTargets(false);

            AdvanceChunkBuild();
        }

        // Text layout and terrain diagnostics need not run at render frequency.
        _hudRefreshTimer -= delta;
        if (_hudRefreshTimer > 0.0)
            return;
        _hudRefreshTimer = 0.2;

        if (_hud != null)
        {
            _hud.Text = "THE NORLOLAND v" + AppVersion + "  |  Seed: " + WorldSeed +
                        "  |  Chunks: " + _chunks.Count +
                        (WorldSave.Failed || WorldSave.WriteFailed ? "  |  SPEICHERFEHLER" : "  |  Autosave") +
                        "  |  WASD/Space  |  E Sammeln  |  Linksklick Abbau  |  I Inventar  |  1–4 Werkzeuge  |  F3 Debug  |  F7 Wasserlabor  |  Esc Menü";
        }

        if (_debugHud != null && _debugHud.Visible)
        {
            Vector3 position = _player.GlobalPosition;
            _debugHud.Text = "DEBUG (F3)" +
                "\nFPS: " + Engine.GetFramesPerSecond() +
                "\nPosition: " + position.X.ToString("F1") + ", " + position.Y.ToString("F1") + ", " + position.Z.ToString("F1") +
                "\nBiome: " + GetBiomeName(Mathf.FloorToInt(position.X), Mathf.FloorToInt(position.Z)) +
                "\nTerrain form: " + GetTerrainForm(position.X, position.Z) +
                "\nChunk: " + center.X + ", " + center.Y +
                "\nChunks loaded: " + _chunks.Count + " / " + _wantedChunks.Count +
                "  |  Queued: " + _pendingChunks.Count +
                "  |  Build stage: " + (_chunkBuildJob == null ? "idle" : (_chunkBuildJob.Stage + 1).ToString() + "/5") +
                "\nView radius: " + ViewDistance + " chunks";
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            return;

        if (key.PhysicalKeycode == Key.F11 || key.Keycode == Key.F11)
        {
            var mode = DisplayServer.WindowGetMode();
            bool fullscreen = mode == DisplayServer.WindowMode.Fullscreen || mode == DisplayServer.WindowMode.ExclusiveFullscreen;
            DisplayServer.WindowSetMode(fullscreen ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.ExclusiveFullscreen);
            GetViewport().SetInputAsHandled();
            return;
        }

        if ((key.Keycode == Key.Escape || key.PhysicalKeycode == Key.Escape) && _survival?.CraftingVisible == true)
        {
            _survival.HideCraftingMenu();
            _escapeWasPressed = true;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode == Key.F7)
        {
            ToggleWaterLabor();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (_waterLab != null)
            return;

        if (key.Keycode == Key.F3)
        {
            _debugHud.Visible = !_debugHud.Visible;
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Keycode == Key.I && _survival != null)
        {
            _survival.ToggleCraftingMenu();
            GetViewport().SetInputAsHandled();
            return;
        }

        if ((key.Keycode == Key.Escape || key.PhysicalKeycode == Key.Escape) && _menuRoot != null)
        {
            _escapeWasPressed = true;
            TogglePauseMenu();
            GetViewport().SetInputAsHandled();
        }
        else if (key.Keycode == Key.F10 || key.PhysicalKeycode == Key.F10)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
            GetViewport().SetInputAsHandled();
        }
    }

    public void ToggleWaterLabor()
    {
        if (_waterLab != null)
        {
            _waterLab.QueueFree();
            _waterLab = null;
            _waterLabGround.Clear();
            RebuildWaterLabChunks();
            _waterLabChunks.Clear();
            _player.GetNode<Camera3D>("Camera3D").Current = true;
            _player.SetPhysicsProcess(true);
            _player.SetProcessUnhandledInput(true);
            _survival.SetProcessInput(true);
            _survival.SetProcess(true);
            _survival.SetWorldInterfaceVisible(true);
            _hud.Visible = true;
            _debugHud.Visible = _debugBeforeWaterLab;
            Input.MouseMode = Input.MouseModeEnum.Captured;
            return;
        }
        _survival.HideCraftingMenu();
        _menuRoot.Visible = false;
        GetTree().Paused = false;
        _player.SetPhysicsProcess(false);
        _player.SetProcessUnhandledInput(false);
        _survival.SetProcessInput(false);
        _survival.SetProcess(false);
        _survival.SetWorldInterfaceVisible(false);
        _debugBeforeWaterLab = _debugHud.Visible;
        _hud.Visible = false;
        _debugHud.Visible = false;
        int centerX = Mathf.RoundToInt(_player.Position.X) + 12;
        int centerZ = Mathf.RoundToInt(_player.Position.Z);
        float baseHeight = GetHeight(centerX, centerZ);
        // Modify the existing terrain height samples, fading to the original rim.
        for (int z = -10; z <= 10; z++)
        for (int x = -10; x <= 10; x++)
        {
            int wx = centerX + x, wz = centerZ + z;
            float original = GetHeight(wx, wz);
            float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            float blend = 1.0f - Mathf.SmoothStep(7.0f, 10.0f, edge);
            // A bowl rises in both axes: the source first fills the center, then the shores.
            float valley = baseHeight - 2.5f + x * x * 0.035f + z * z * 0.045f;
            _waterLabGround[new Vector2I(wx, wz)] = Mathf.Lerp(original, valley, blend);
            // Include both chunks touching a vertex, including negative coordinates.
            for (int dz = -1; dz <= 0; dz++)
            for (int dx = -1; dx <= 0; dx++)
                _waterLabChunks.Add(new Vector2I(Mathf.FloorToInt((float)(wx + dx) / ChunkSize), Mathf.FloorToInt((float)(wz + dz) / ChunkSize)));
        }
        RebuildWaterLabChunks();
        _waterLab = new WaterSandbox { Name = "WaterLabor", OwnerWorld = this,
            Position = new Vector3(centerX, baseHeight, centerZ) };
        AddChild(_waterLab);
    }

    private void RebuildWaterLabChunks()
    {
        // Keep original terrain/vegetation outside the affected chunks. Rebuilding
        // removes floating plants from the modified valley, then restores them on exit.
        if (_chunkBuildJob != null && _waterLabChunks.Contains(_chunkBuildJob.Coordinate))
            _chunkBuildJob = null;
        foreach (Vector2I coordinate in _waterLabChunks)
        {
            if (_chunks.TryGetValue(coordinate, out Node3D old))
            {
                old.QueueFree();
                _chunks.Remove(coordinate);
                _terrainMeshes.Remove(coordinate);
                _terrainColliders.Remove(coordinate);
            }
        }
        UpdateChunkTargets(true);
        if (_waterLabGround.Count == 0) return;
        foreach (Vector2I coordinate in _waterLabChunks)
        {
            if (!_wantedChunks.Contains(coordinate)) continue;
            Node3D chunk = BuildChunk(coordinate);
            AddChild(chunk);
            _chunks[coordinate] = chunk;
        }
    }

    private void TogglePauseMenu()
    {
        if (!_menuRoot.Visible)
        {
            ShowPauseMenu();
            return;
        }

        if (_settingsMenu.Visible)
        {
            ShowPauseMenu();
            return;
        }

        SetMenuOpen(false);
    }

    public void SetCraftingOverlayOpen(bool isOpen)
    {
        if (isOpen)
        {
            GetTree().Paused = true;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
        else if (_menuRoot == null || !_menuRoot.Visible)
        {
            GetTree().Paused = false;
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    private static StandardMaterial3D CreateMaterial(Color color, Texture2D texture = null)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            AlbedoTexture = texture,
            Roughness = 1.0f,
            TextureRepeat = true,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps
        };
    }

    private ImageTexture CreateBarkTexture()
    {
        const int width = 64;
        const int height = 128;
        var random = new Random(WorldSeed ^ 0x4b41524b);
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float wave = Mathf.Sin((x + Mathf.Sin(y * 0.09f) * 3.0f) * 0.52f) * 0.10f;
            float streak = (float)random.NextDouble() * 0.10f;
            float value = Mathf.Clamp(0.72f + wave + streak, 0.42f, 1.0f);
            image.SetPixel(x, y, new Color(value, value, value, 1.0f));
        }
        var texture = ImageTexture.CreateFromImage(image);
        return texture;
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
            BackgroundColor = new Color(0.16f, 0.22f, 0.20f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.52f, 0.58f, 0.54f),
            AmbientLightEnergy = 0.38f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            FogEnabled = true,
            FogLightColor = new Color(0.43f, 0.47f, 0.44f),
            FogDensity = 0.006f,
            FogHeight = 24.0f,
            FogHeightDensity = 0.018f
        };
        worldEnvironment.Environment = environment;
        AddChild(worldEnvironment);

        var sun = new DirectionalLight3D
        {
            Name = "Sun",
            RotationDegrees = new Vector3(-48, -32, 0),
            LightEnergy = 0.72f,
            LightColor = new Color(1.0f, 0.86f, 0.66f),
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
            Position = new Vector2(16, 156),
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
            Layer = 100,
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
        _menuRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

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
        _pauseMenu.AddThemeStyleboxOverride("panel", CreateMenuPanelStyle());
        center.AddChild(_pauseMenu);
        var pauseContent = new VBoxContainer();
        pauseContent.AddThemeConstantOverride("separation", 14);
        _pauseMenu.AddChild(pauseContent);
        pauseContent.AddChild(CreateMenuTitle("PAUSE"));
        pauseContent.AddChild(CreateMenuButton("Weiter", () => SetMenuOpen(false)));
        pauseContent.AddChild(CreateMenuButton("Einstellungen", ShowSettingsMenu));
        pauseContent.AddChild(CreateMenuButton("Rucksack & Herstellen", _survival.ShowCraftingMenu));

        _settingsMenu = new PanelContainer
        {
            CustomMinimumSize = new Vector2(460, 0),
            Visible = false
        };
        _settingsMenu.AddThemeStyleboxOverride("panel", CreateMenuPanelStyle());
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
            MaxValue = 4,
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
        title.AddThemeColorOverride("font_color", Colors.White);
        return title;
    }

    private static StyleBoxFlat CreateMenuPanelStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.055f, 0.075f, 0.085f, 0.98f),
            BorderColor = new Color(0.42f, 0.52f, 0.43f, 1.0f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10
        };
        style.ContentMarginLeft = 20;
        style.ContentMarginTop = 18;
        style.ContentMarginRight = 20;
        style.ContentMarginBottom = 18;
        return style;
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
        AutoScaleViewDistance = false;
        ApplyViewDistance(Mathf.RoundToInt((float)value));
    }

    private void ApplyViewDistance(int distance)
    {
        ViewDistance = Mathf.Clamp(distance, 1, 4);
        _player.GetNode<Camera3D>("Camera3D").Far = ChunkSize * (ViewDistance + 1.0f);
        UpdateViewDistanceLabel();
        UpdateChunkTargets(true);
    }

    private void AdaptViewDistance()
    {
        int fps = Mathf.RoundToInt((float)Engine.GetFramesPerSecond());
        if (fps <= 0) return;
        if (fps < TargetFps - 8)
        {
            _adaptiveBadFrames++;
            _adaptiveGoodFrames = 0;
        }
        else if (fps > TargetFps + 12)
        {
            _adaptiveGoodFrames++;
            _adaptiveBadFrames = 0;
        }
        else
        {
            _adaptiveGoodFrames = 0;
            _adaptiveBadFrames = 0;
        }

        if (_adaptiveBadFrames >= 2 && ViewDistance > 1)
        {
            ApplyViewDistance(ViewDistance - 1);
            _adaptiveBadFrames = 0;
        }
        else if (_adaptiveGoodFrames >= 4 && ViewDistance < 4)
        {
            ApplyViewDistance(ViewDistance + 1);
            _adaptiveGoodFrames = 0;
        }
    }

    private void UpdateViewDistanceLabel()
    {
        _viewDistanceLabel.Text = $"{ViewDistance} Chunkringe · ca. {ViewDistance * ChunkSize} m" + (AutoScaleViewDistance ? " · automatisch" : " · manuell");
    }

    private Vector2I GetPlayerChunk()
    {
        if (_player == null || !_player.IsInsideTree())
            return Vector2I.Zero;
        Vector3 position = _player.GlobalPosition;
        int x = Mathf.FloorToInt(position.X / ChunkSize);
        int z = Mathf.FloorToInt(position.Z / ChunkSize);
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

        if (_waterLabGround.Count > 0)
            foreach (Vector2I coordinate in _waterLabChunks) _wantedChunks.Add(coordinate);

        foreach (Vector2I key in _chunks.Keys.ToArray())
        {
            if (_wantedChunks.Contains(key))
                continue;

            _chunks[key].QueueFree();
            _chunks.Remove(key);
            _terrainMeshes.Remove(key);
            _terrainColliders.Remove(key);
        }

        _pendingChunks = new Queue<Vector2I>(
            _wantedChunks
                .Where(key => !_chunks.ContainsKey(key))
                .OrderBy(key => key.DistanceSquaredTo(center)));
    }

    private void AdvanceChunkBuild()
    {
        // Scene-tree and rendering operations stay on the main thread. Only one
        // construction stage runs per frame; no partially built detached nodes linger.
        if (_chunkBuildJob != null)
        {
            ChunkBuildJob job = _chunkBuildJob;
            if (!_wantedChunks.Contains(job.Coordinate) ||
                !_chunks.TryGetValue(job.Coordinate, out Node3D current) ||
                current != job.Chunk || current.IsQueuedForDeletion())
            {
                _chunkBuildJob = null;
            }
            else
            {
                switch (job.Stage)
                {
                    case 0: AddTrees(job.Chunk, job.Coordinate, job.Heights); break;
                    case 1: AddGrass(job.Chunk, job.Coordinate, job.Heights); break;
                    case 2: AddFlowers(job.Chunk, job.Coordinate, job.Heights); break;
                    case 3: AddShrubs(job.Chunk, job.Coordinate, job.Heights); break;
                    case 4: AddLooseStones(job.Chunk, job.Coordinate, job.Heights); break;
                }
                if (++job.Stage >= 5)
                    _chunkBuildJob = null;
                return;
            }
        }

        while (_pendingChunks.Count > 0)
        {
            Vector2I next = _pendingChunks.Dequeue();
            if (!_wantedChunks.Contains(next) || _chunks.ContainsKey(next))
                continue;
            Node3D chunk = BuildChunk(next);
            AddChild(chunk);
            _chunks[next] = chunk;
            return;
        }
    }

    private Node3D BuildChunk(Vector2I coordinate)
    {
        var chunk = new Node3D
        {
            Name = "Chunk_" + coordinate.X + "_" + coordinate.Y,
            Position = new Vector3(coordinate.X * ChunkSize, 0, coordinate.Y * ChunkSize)
        };

        float[,] heights = CreateTerrainHeightGrid(coordinate);
        ArrayMesh mesh = CreateTerrainMesh(coordinate, heights);
        var terrain = new MeshInstance3D
        {
            Name = "TerrainMesh",
            Mesh = mesh,
            MaterialOverride = _terrainMaterial
        };
        chunk.AddChild(terrain);
        _terrainMeshes[coordinate] = terrain;

        // A single heightmap collider per chunk avoids building a triangle mesh collider
        // from every rendered terrain face. The height samples use the same grid as the mesh.
        var heightMap = new HeightMapShape3D
        {
            MapWidth = ChunkSize + 1,
            MapDepth = ChunkSize + 1,
            MapData = CreateHeightMapData(heights)
        };
        var collision = new StaticBody3D { Name = "TerrainCollision" };
        var terrainCollisionShape = new CollisionShape3D
        {
            Shape = heightMap,
            Position = new Vector3(ChunkSize * 0.5f, 0, ChunkSize * 0.5f)
        };
        collision.AddChild(terrainCollisionShape);
        chunk.AddChild(collision);
        _terrainColliders[coordinate] = terrainCollisionShape;

        if (_waterLabGround.Count > 0 && _waterLabChunks.Contains(coordinate))
            return chunk;

        // Terrain and collision become available first. Decoration follows over
        // five frames, preserving each generator's seed and random-call order.
        _chunkBuildJob = new ChunkBuildJob
        {
            Coordinate = coordinate,
            Chunk = chunk,
            Heights = heights
        };
        return chunk;
    }

    private float[,] CreateTerrainHeightGrid(Vector2I coordinate)
    {
        int sampleCount = ChunkSize + 1;
        var heights = new float[sampleCount, sampleCount];
        for (int x = 0; x < sampleCount; x++)
        for (int z = 0; z < sampleCount; z++)
            heights[x, z] = GetHeight(coordinate.X * ChunkSize + x, coordinate.Y * ChunkSize + z);
        return heights;
    }

    private ArrayMesh CreateTerrainMesh(Vector2I coordinate, float[,] heights)
    {
        int sampleCount = ChunkSize + 1;
        Color[,] terrainColors = new Color[sampleCount, sampleCount];
        for (int x = 0; x < sampleCount; x++)
        for (int z = 0; z < sampleCount; z++)
            terrainColors[x, z] = GetTerrainColor(heights, x, z, coordinate.X * ChunkSize + x, coordinate.Y * ChunkSize + z);

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int x = 0; x < ChunkSize; x++)
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
        surface.Index();
        surface.GenerateNormals();
        return surface.Commit() as ArrayMesh;
    }

    public float GetGroundHeight(float worldX, float worldZ)
        => GetHeight(Mathf.RoundToInt(worldX), Mathf.RoundToInt(worldZ));

    public void DigGroundAt(Vector3 worldPosition, float radius, float depth)
    {
        int centerX = Mathf.RoundToInt(worldPosition.X);
        int centerZ = Mathf.RoundToInt(worldPosition.Z);
        int extent = Mathf.CeilToInt(radius);
        for (int x = centerX - extent; x <= centerX + extent; x++)
        for (int z = centerZ - extent; z <= centerZ + extent; z++)
        {
            float distance = (new Vector2(x, z) - new Vector2(worldPosition.X, worldPosition.Z)).Length();
            if (distance > radius) continue;
            float falloff = 1.0f - distance / radius;
            falloff = falloff * falloff * (3.0f - 2.0f * falloff);
            var key = new Vector2I(x, z);
            _excavationDepth.TryGetValue(key, out float existingDepth);
            _excavationDepth[key] = Mathf.Min(existingDepth + depth * falloff, 2.2f);
            _grassCover[key] = 0.0f;
        }

        SaveWorldChanges();
        foreach (Vector2I coordinate in _chunks.Keys.ToArray())
        {
            float minX = coordinate.X * ChunkSize - radius - 1.0f;
            float maxX = (coordinate.X + 1) * ChunkSize + radius + 1.0f;
            float minZ = coordinate.Y * ChunkSize - radius - 1.0f;
            float maxZ = (coordinate.Y + 1) * ChunkSize + radius + 1.0f;
            if (worldPosition.X < minX || worldPosition.X > maxX || worldPosition.Z < minZ || worldPosition.Z > maxZ)
                continue;
            RefreshTerrainChunk(coordinate);
        }
    }

    private void RestoreTerrainChanges()
    {
        _excavationDepth.Clear();
        _grassCover.Clear();
        foreach (var entry in WorldSave.Current.Depth)
        {
            string[] parts = entry.Key.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int z))
                _excavationDepth[new Vector2I(x, z)] = entry.Value;
        }
        foreach (var entry in WorldSave.Current.Cover)
        {
            string[] parts = entry.Key.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int z))
                _grassCover[new Vector2I(x, z)] = entry.Value;
        }
    }

    public void SaveWorldChanges()
    {
        WorldSave.Current.Depth.Clear();
        WorldSave.Current.Cover.Clear();
        foreach (var entry in _excavationDepth)
            WorldSave.Current.Depth[$"{entry.Key.X},{entry.Key.Y}"] = entry.Value;
        foreach (var entry in _grassCover)
            WorldSave.Current.Cover[$"{entry.Key.X},{entry.Key.Y}"] = entry.Value;
        WorldSave.Save();
    }

    public override void _ExitTree() => SaveWorldChanges();

    public void RemoveTreeAt(Vector3 worldPosition, string persistentId)
    {
        if (!string.IsNullOrEmpty(persistentId))
            SurvivalGameplay.MarkWorldResourceConsumed(persistentId);
        Vector2I coordinate = new(Mathf.FloorToInt(worldPosition.X / ChunkSize), Mathf.FloorToInt(worldPosition.Z / ChunkSize));
        if (!_chunks.TryGetValue(coordinate, out Node3D chunk)) return;
        chunk.QueueFree();
        _chunks.Remove(coordinate);
        _terrainMeshes.Remove(coordinate);
        _terrainColliders.Remove(coordinate);
        if (!_pendingChunks.Contains(coordinate)) _pendingChunks.Enqueue(coordinate);
    }

    public bool IsRockLayerAt(float worldX, float worldZ)
    {
        var key = new Vector2I(Mathf.RoundToInt(worldX), Mathf.RoundToInt(worldZ));
        return _excavationDepth.TryGetValue(key, out float depth) && depth >= 1.2f;
    }

    private void RegrowGroundCover(float elapsed)
    {
        var touchedChunks = new HashSet<Vector2I>();
        foreach (Vector2I key in _grassCover.Keys.ToArray())
        {
            if (!_excavationDepth.TryGetValue(key, out float depth) || depth >= 1.2f)
                continue;
            float cover = Mathf.Min(1.0f, _grassCover[key] + elapsed / 180.0f);
            if (cover >= 1.0f)
                _grassCover.Remove(key);
            else
                _grassCover[key] = cover;
            Vector2I chunk = new(Mathf.FloorToInt((float)key.X / ChunkSize), Mathf.FloorToInt((float)key.Y / ChunkSize));
            if (_chunks.ContainsKey(chunk)) touchedChunks.Add(chunk);
        }
        foreach (Vector2I coordinate in touchedChunks)
            RefreshTerrainChunk(coordinate, false);
    }

    private void RefreshTerrainChunk(Vector2I coordinate, bool refreshCollision = true)
    {
        if (!_terrainMeshes.TryGetValue(coordinate, out MeshInstance3D terrain) ||
            (refreshCollision && !_terrainColliders.ContainsKey(coordinate)))
            return;

        float[,] heights = CreateTerrainHeightGrid(coordinate);
        if (_chunkBuildJob != null && _chunkBuildJob.Coordinate == coordinate)
            _chunkBuildJob.Heights = heights;
        terrain.Mesh = CreateTerrainMesh(coordinate, heights);
        if (refreshCollision && _terrainColliders.TryGetValue(coordinate, out CollisionShape3D collider))
        {
            collider.Shape = new HeightMapShape3D
            {
                MapWidth = ChunkSize + 1,
                MapDepth = ChunkSize + 1,
                MapData = CreateHeightMapData(heights)
            };
        }
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

    private ImageTexture CreateSoilTexture()
    {
        const int size = 128;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        var random = new Random(WorldSeed ^ 0x13579BDF);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float grain = (float)random.NextDouble() * 0.10f;
            float damp = Mathf.Sin(x * 0.17f + y * 0.11f) * 0.035f;
            image.SetPixel(x, y, new Color(0.25f + grain + damp, 0.145f + grain * 0.55f, 0.075f + grain * 0.28f, 1));
        }
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

            Color grass = GetGrassColor(worldX, worldZ) * 0.78f;
        float groundVariation = _surfaceNoise.GetNoise2D(worldX, worldZ);
        Color soil = new(0.30f, 0.19f, 0.105f);
        grass = grass.Lerp(soil, Mathf.Clamp(0.08f + groundVariation * 0.12f, 0.0f, 0.22f));
        grass *= 0.94f + _detailNoise.GetNoise2D(worldX, worldZ) * 0.08f;
        Color rock = new(0.38f, 0.37f, 0.34f);
        float rockAmount = Mathf.Clamp((slope - 1.4f) * 0.42f, 0.0f, 0.72f);
        Color terrain = grass.Lerp(rock, rockAmount);
        Color snow = new(0.78f, 0.80f, 0.76f);
        float snowAmount = Mathf.Clamp((heights[x, z] - 39.0f) * 0.34f, 0.0f, 0.62f);
        Color naturalSurface = terrain.Lerp(snow, snowAmount);
        Vector2I key = new(worldX, worldZ);
        if (_excavationDepth.TryGetValue(key, out float dugDepth))
        {
            Color exposed = soil.Lerp(rock, Smooth01((dugDepth - 0.95f) / 0.38f));
            _grassCover.TryGetValue(key, out float cover);
            return exposed.Lerp(naturalSurface, cover);
        }
        return naturalSurface;
    }

    private static float Smooth01(float value)
    {
        float t = Mathf.Clamp(value, 0.0f, 1.0f);
        return t * t * (3.0f - 2.0f * t);
    }

    private string GetTerrainName(int worldX, int worldZ)
    {
        float height = GetHeight(worldX, worldZ);
        float slope = Mathf.Max(
            Mathf.Abs(GetHeight(worldX + 1, worldZ) - GetHeight(worldX - 1, worldZ)),
            Mathf.Abs(GetHeight(worldX, worldZ + 1) - GetHeight(worldX, worldZ - 1))) * 0.5f;
        if (height >= 43.0f)
            return "Schneegebirge";
        if (height >= 39.0f || slope >= 2.5f)
            return "Hügeliges Hochland";

        return GetBiomeName(worldX, worldZ);
    }

    private string GetBiomeName(int worldX, int worldZ)
    {
        float biome = _biomeNoise.GetNoise2D(worldX, worldZ);
        if (biome < -0.42f)
            return "Trockenes Buschland";
        if (biome < -0.18f)
            return "Steppe";
        if (biome > 0.48f)
            return "Dichter Wald";
        if (biome > 0.24f)
            return "Blumenwiese";
        return "Grasland";
    }

    private string GetTerrainForm(float worldX, float worldZ)
    {
        int x = Mathf.RoundToInt(worldX);
        int z = Mathf.RoundToInt(worldZ);
        float height = GetHeight(x, z);
        float slope = Mathf.Max(
            Mathf.Abs(GetHeight(x + 1, z) - GetHeight(x - 1, z)),
            Mathf.Abs(GetHeight(x, z + 1) - GetHeight(x, z - 1))) * 0.5f;
        if (height >= 43.0f) return "Hochgebirge";
        if (height >= 39.0f || slope >= 2.5f) return "Hochland/Hang";
        if (slope >= 1.3f) return "sanfte Hügel";
        return "Ebene";
    }

    private float GetHeight(int worldX, int worldZ)
    {
        if (_waterLabGround.TryGetValue(new Vector2I(worldX, worldZ), out float laboratoryHeight))
            return laboratoryHeight;
        float noise = _terrainNoise.GetNoise2D(worldX, worldZ);
        float detail = _detailNoise.GetNoise2D(worldX, worldZ);
        float ridgeField = _mountainNoise.GetNoise2D(worldX, worldZ);
        float mountainMask = Mathf.Clamp((ridgeField + 0.18f) * 1.2f, 0.0f, 1.0f);
        float ridges = Mathf.Pow(mountainMask, 1.7f) * 4.5f;
        float height = 27.0f + noise * 7.0f + detail * 0.9f + ridges;
        if (_excavationDepth.TryGetValue(new Vector2I(worldX, worldZ), out float dugDepth))
            height -= dugDepth;
        return height;
    }

    private Color GetGrassColor(int worldX, int worldZ)
    {
        float biome = _biomeNoise.GetNoise2D(worldX, worldZ);
        if (biome < -0.42f)
            return new Color(0.92f, 0.80f, 0.58f); // dry scrub
        if (biome < -0.18f)
            return new Color(0.84f, 0.83f, 0.59f); // steppe
        if (biome > 0.48f)
            return new Color(0.74f, 0.84f, 0.68f); // dense forest
        if (biome > 0.24f)
            return new Color(0.83f, 0.92f, 0.71f); // meadow
        return new Color(0.89f, 0.94f, 0.77f); // grassland
    }

    private void AddTrees(Node3D chunk, Vector2I coordinate, float[,] heights)
    {
        int treeSeed = unchecked(WorldSeed ^ (coordinate.X * 73856093) ^ (coordinate.Y * 19349663));
        var random = new Random(treeSeed);
        var trunkTransforms = new List<Transform3D>[4];
        var leafTransforms = new List<Transform3D>[4];
        var leafColors = new List<Color>[4];
        var treeCollisionShapes = new List<CollisionShape3D>();
        for (int type = 0; type < 4; type++)
        {
            trunkTransforms[type] = new List<Transform3D>();
            leafTransforms[type] = new List<Transform3D>();
            leafColors[type] = new List<Color>();
        }

        for (int attempt = 0; attempt < 24; attempt++)
        {
            int x = random.Next(1, ChunkSize - 1);
            int z = random.Next(1, ChunkSize - 1);
            float h = (heights[x, z] + heights[x + 1, z] + heights[x, z + 1] + heights[x + 1, z + 1]) * 0.25f;

            if (Math.Abs(h - heights[x - 1, z]) > 1.5f ||
                Math.Abs(h - heights[x + 1, z]) > 1.5f ||
                Math.Abs(h - heights[x, z - 1]) > 1.5f ||
                Math.Abs(h - heights[x, z + 1]) > 1.5f)
                continue;

            float px = x + 0.5f;
            float pz = z + 0.5f;
            int worldX = coordinate.X * ChunkSize + x;
            int worldZ = coordinate.Y * ChunkSize + z;
            string treeId = $"{WorldSeed}:tree:{worldX}:{worldZ}:{attempt}";
            if (SurvivalGameplay.IsResourceConsumed(treeId))
                continue;
            float biome = _biomeNoise.GetNoise2D(worldX, worldZ);
            double density = biome < -0.42f ? 0.08 : biome < -0.18f ? 0.14 :
                biome > 0.48f ? 0.44 : biome > 0.24f ? 0.31 : 0.25;
            float patch = _detailNoise.GetNoise2D(worldX * 0.18f, worldZ * 0.18f);
            density *= Mathf.Lerp(0.45f, 1.35f, patch * 0.5f + 0.5f);
            if (random.NextDouble() > density)
                continue;

            int treeType = ChooseTreeType(biome, worldX, worldZ, random);
            float trunkHeight = treeType switch { 1 => 5.0f, 2 => 4.4f, 3 => 4.3f, _ => 3.4f };
            float matureScale = treeType == 3
                ? 1.28f + (float)random.NextDouble() * 0.32f
                : 1.65f + (float)random.NextDouble() * 0.45f;
            float heightScale = 0.90f + (float)random.NextDouble() * 0.20f;
            float actualTrunkHeight = trunkHeight * matureScale * heightScale;
            float trunkThickness = 0.92f + matureScale * 0.26f;
            float leanX = ((float)random.NextDouble() - 0.5f) * 0.08f;
            float leanZ = ((float)random.NextDouble() - 0.5f) * 0.08f;
            Vector3 trunkPosition = new(px, h + actualTrunkHeight * 0.5f, pz);
            trunkTransforms[treeType].Add(new Transform3D(
                Basis.FromEuler(new Vector3(leanX, 0, leanZ)).Scaled(new Vector3(trunkThickness, matureScale * heightScale, trunkThickness)),
                trunkPosition));
            treeCollisionShapes.Add(new CollisionShape3D
            {
                Name = "TreeTrunkCollider",
                Shape = new CylinderShape3D { Radius = 0.23f * trunkThickness, Height = actualTrunkHeight },
                Position = trunkPosition
            });
            AddTreeCanopy(leafTransforms[treeType], leafColors[treeType], treeType, new Vector3(px, h, pz), matureScale, random);
            var treeResource = CreateTreeResourceNode(treeId, actualTrunkHeight);
            treeResource.Position = new Vector3(px, h + actualTrunkHeight * 0.42f, pz);
            chunk.AddChild(treeResource);
        }

        if (treeCollisionShapes.Count > 0)
        {
            var treeCollision = new StaticBody3D { Name = "TreeCollisions", CollisionLayer = 1, CollisionMask = 1 };
            foreach (CollisionShape3D shape in treeCollisionShapes)
                treeCollision.AddChild(shape);
            chunk.AddChild(treeCollision);
        }

        for (int type = 0; type < 4; type++)
            AddTreeInstances(chunk, type, trunkTransforms[type], leafTransforms[type], leafColors[type]);
    }

    private int ChooseTreeType(float biome, int worldX, int worldZ, Random random)
    {
        // The low-frequency field creates broad, coherent woodland patches.
        float patch = _detailNoise.GetNoise2D(worldX * 0.18f, worldZ * 0.18f);
        double roll = random.NextDouble();
        int dominant;
        int accent;
        if (biome < -0.25f)
        {
            dominant = 3; accent = 0;
        }
        else if (biome > 0.30f)
        {
            dominant = patch > 0.05f ? 1 : 2; accent = dominant == 1 ? 2 : 1;
        }
        else
        {
            dominant = patch > 0.18f ? 0 : 1; accent = dominant == 0 ? 1 : 0;
        }

        return roll < 0.88 ? dominant : roll < 0.97 ? accent : (dominant + 1 + random.Next(0, 3)) % 4;
    }

    private static float SampleTerrainHeight(float[,] heights, float x, float z)
    {
        int ix = Mathf.Clamp(Mathf.FloorToInt(x), 0, heights.GetLength(0) - 2);
        int iz = Mathf.Clamp(Mathf.FloorToInt(z), 0, heights.GetLength(1) - 2);
        float tx = Mathf.Clamp(x - ix, 0.0f, 1.0f);
        float tz = Mathf.Clamp(z - iz, 0.0f, 1.0f);
        float p00 = heights[ix, iz];
        float p01 = heights[ix, iz + 1];
        float p11 = heights[ix + 1, iz + 1];
        float p10 = heights[ix + 1, iz];
        return tz >= tx
            ? p00 * (1.0f - tz) + p01 * (tz - tx) + p11 * tx
            : p00 * (1.0f - tx) + p11 * tz + p10 * (tx - tz);
    }

    private static bool IsPlantable(float[,] heights, int x, int z)
    {
        float h = heights[x, z];
        return h < 40.0f &&
            Mathf.Abs(h - heights[x - 1, z]) < 1.1f &&
            Mathf.Abs(h - heights[x + 1, z]) < 1.1f &&
            Mathf.Abs(h - heights[x, z - 1]) < 1.1f &&
            Mathf.Abs(h - heights[x, z + 1]) < 1.1f;
    }

    private void AddGrass(Node3D chunk, Vector2I coordinate, float[,] heights)
    {
        var random = new Random(unchecked(WorldSeed ^ coordinate.X * 83492791 ^ coordinate.Y * 297121507));
        float biome = _biomeNoise.GetNoise2D(coordinate.X * ChunkSize + ChunkSize / 2, coordinate.Y * ChunkSize + ChunkSize / 2);
        int shortCount = biome < -0.42f ? 140 : biome < -0.18f ? 240 : biome > 0.48f ? 300 : biome > 0.25f ? 470 : 410;
        int tallCount = biome < -0.42f ? 24 : biome < -0.18f ? 42 : biome > 0.48f ? 58 : biome > 0.25f ? 110 : 90;
        var shortTransforms = new List<Transform3D>(shortCount);
        var shortColors = new List<Color>(shortCount);
        var tallTransforms = new List<Transform3D>(tallCount);
        var tallColors = new List<Color>(tallCount);
        AddGrassPatches(heights, random, shortCount, 0.72f, 1.22f, shortTransforms, shortColors);
        AddGrassPatches(heights, random, tallCount, 0.72f, 1.18f, tallTransforms, tallColors);
        AddColoredMultiMesh(chunk, "ShortGrass", _shortGrassMesh, _grassMaterial, shortTransforms, shortColors);
        AddColoredMultiMesh(chunk, "TallGrass", _grassMesh, _grassMaterial, tallTransforms, tallColors);
    }

    private void AddGrassPatches(float[,] heights, Random random, int desired, float minScale, float maxScale,
        List<Transform3D> transforms, List<Color> colors)
    {
        int patchCount = (desired + 6) / 7;
        for (int patch = 0; patch < patchCount && transforms.Count < desired; patch++)
        {
            int centerX = random.Next(2, ChunkSize - 2);
            int centerZ = random.Next(2, ChunkSize - 2);
            for (int blade = 0; blade < 16 && transforms.Count < desired; blade++)
            {
                int x = Mathf.Clamp(centerX + random.Next(-2, 3), 1, ChunkSize - 2);
                int z = Mathf.Clamp(centerZ + random.Next(-2, 3), 1, ChunkSize - 2);
                if (!IsPlantable(heights, x, z)) continue;
                float px = x + (float)random.NextDouble();
                float pz = z + (float)random.NextDouble();
                float y = SampleTerrainHeight(heights, px, pz) + 0.015f;
                float scale = minScale + (float)random.NextDouble() * (maxScale - minScale);
                float angle = (float)random.NextDouble() * Mathf.Tau;
                transforms.Add(new Transform3D(Basis.FromEuler(new Vector3(0, angle, 0)).Scaled(new Vector3(scale, scale, scale)), new Vector3(px, y, pz)));
                float shade = (float)random.NextDouble() * 0.10f;
                colors.Add(new Color(0.90f + shade, 0.93f + shade * 0.60f, 0.85f + shade));
            }
        }
    }

    private void AddFlowers(Node3D chunk, Vector2I coordinate, float[,] heights)
    {
        var random = new Random(unchecked(WorldSeed ^ coordinate.X * 1640531535 ^ coordinate.Y * 265443576));
        float biome = _biomeNoise.GetNoise2D(coordinate.X * ChunkSize + ChunkSize / 2, coordinate.Y * ChunkSize + ChunkSize / 2);
        int desired = biome < -0.42f ? 2 : biome < -0.18f ? 5 : biome > 0.24f && biome <= 0.48f ? 18 : biome > 0.48f ? 7 : 11;
        var stems = new List<Transform3D>();
        var petals = new List<Transform3D>[] { new(), new(), new() };
        var centers = new List<Transform3D>();
        int patchCount = (desired + 3) / 4;
        for (int patch = 0; patch < patchCount && stems.Count < desired; patch++)
        {
            int centerX = random.Next(2, ChunkSize - 2);
            int centerZ = random.Next(2, ChunkSize - 2);
            int patchColor = random.Next(0, petals.Length);
            for (int flower = 0; flower < 8 && stems.Count < desired; flower++)
            {
                int x = Mathf.Clamp(centerX + random.Next(-2, 3), 1, ChunkSize - 2);
                int z = Mathf.Clamp(centerZ + random.Next(-2, 3), 1, ChunkSize - 2);
                if (!IsPlantable(heights, x, z) || random.NextDouble() < 0.20) continue;
                float px = x + (float)random.NextDouble();
                float pz = z + (float)random.NextDouble();
                float y = SampleTerrainHeight(heights, px, pz);
                float height = 0.32f + (float)random.NextDouble() * 0.30f;
                stems.Add(new Transform3D(Basis.FromScale(new Vector3(0.035f, height, 0.035f)), new Vector3(px, y + height * 0.5f, pz)));
                int color = random.NextDouble() < 0.82 ? patchColor : random.Next(0, petals.Length);
                float size = 0.10f + (float)random.NextDouble() * 0.07f;
                petals[color].Add(new Transform3D(Basis.FromScale(new Vector3(size, size, size)), new Vector3(px, y + height, pz)));
                centers.Add(new Transform3D(Basis.FromScale(new Vector3(size * 0.36f, size * 0.36f, size * 0.36f)), new Vector3(px, y + height + 0.015f, pz)));
            }
        }
        AddMultiMesh(chunk, "FlowerStems", new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 1.0f, RadialSegments = 5 }, _flowerStemMaterial, stems);
        for (int c = 0; c < petals.Length; c++) AddMultiMesh(chunk, "FlowerPetals_" + c, _flowerPetalMesh, _flowerMaterials[c], petals[c]);
        AddMultiMesh(chunk, "FlowerCenters", new SphereMesh { Radius = 1.0f, Height = 2.0f, RadialSegments = 6, Rings = 4 }, _flowerCenterMaterial, centers);
    }

    private void AddShrubs(Node3D chunk, Vector2I coordinate, float[,] heights)
    {
        var random = new Random(unchecked(WorldSeed ^ coordinate.X * 1000003 ^ coordinate.Y * 9176));
        int worldCenterX = coordinate.X * ChunkSize + ChunkSize / 2;
        int worldCenterZ = coordinate.Y * ChunkSize + ChunkSize / 2;
        float biome = _biomeNoise.GetNoise2D(worldCenterX, worldCenterZ);
        int bushCount = biome < -0.42f ? 2 : biome < -0.18f ? 3 : biome > 0.48f ? 7 : biome > 0.24f ? 6 : 5;
        for (int i = 0; i < bushCount; i++)
        {
            int x = random.Next(1, ChunkSize - 1);
            int z = random.Next(1, ChunkSize - 1);
            if (!IsPlantable(heights, x, z)) continue;
            float px = x + (float)random.NextDouble();
            float pz = z + (float)random.NextDouble();
            float y = SampleTerrainHeight(heights, px, pz);
            var bush = new GrowingBush { Name = $"GrowingBush_{i}" };
            int worldX = coordinate.X * ChunkSize + x;
            int worldZ = coordinate.Y * ChunkSize + z;
            bush.Configure(
                unchecked(WorldSeed ^ coordinate.X * 73856093 ^ coordinate.Y * 19349663 ^ i * 83492791),
                biome,
                $"{WorldSeed}:bush:{worldX}:{worldZ}:{i}",
                (float)random.NextDouble() * 0.78f + 0.18f);
            if (SurvivalGameplay.IsResourceConsumed(bush.PersistentId))
            {
                bush.Free();
                continue;
            }
            bush.Position = new Vector3(px, y, pz);
            chunk.AddChild(bush);
        }
    }

    private void AddLooseStones(Node3D chunk, Vector2I coordinate, float[,] heights)
    {
        var random = new Random(unchecked(WorldSeed ^ coordinate.X * 15485863 ^ coordinate.Y * 32452843 ^ 0x51A7));
        int looseStoneCount = random.Next(0, 3);
        for (int i = 0; i < looseStoneCount; i++)
        {
            int x = random.Next(1, ChunkSize - 1);
            int z = random.Next(1, ChunkSize - 1);
            if (!IsPlantable(heights, x, z)) continue;
            int roll = random.Next(100);
            string kind = roll < 82 ? "small_stone" : "large_stone";
            string persistentId = $"{WorldSeed}:loose:{coordinate.X}:{coordinate.Y}:{i}";
            if (SurvivalGameplay.IsResourceConsumed(persistentId))
            {
                random.NextDouble(); // Match the per-stone visual variation draw to preserve later chunk randomness.
                continue;
            }
            float size = kind switch { "small_stone" => 0.055f, _ => 0.16f };
            float px = x + (float)random.NextDouble();
            float pz = z + (float)random.NextDouble();
            float y = SampleTerrainHeight(heights, px, pz);
            var stone = CreateResourceNode(kind, size, 1, random);
            stone.Name = "LooseStone_" + i;
            stone.PersistentId = persistentId;
            stone.Position = new Vector3(px, y + size * 0.35f, pz);
            chunk.AddChild(stone);
        }

        if (random.NextDouble() < 0.16)
        {
            string persistentId = $"{WorldSeed}:deposit:{coordinate.X}:{coordinate.Y}";
            if (SurvivalGameplay.IsResourceConsumed(persistentId)) return;
            int x = random.Next(2, ChunkSize - 2);
            int z = random.Next(2, ChunkSize - 2);
            if (IsPlantable(heights, x, z))
            {
                float px = x + 0.5f;
                float pz = z + 0.5f;
                float y = SampleTerrainHeight(heights, px, pz);
                var deposit = CreateResourceNode("stone_deposit", 0.40f, 5, random);
                deposit.Name = "StoneDeposit";
                deposit.PersistentId = persistentId;
                deposit.HitPoints = WorldSave.Current.Health.GetValueOrDefault(persistentId, 5);
                deposit.Position = new Vector3(px, y + 0.25f, pz);
                chunk.AddChild(deposit);
            }
        }
    }

    private static ResourceNode CreateResourceNode(string kind, float size, int hitPoints, Random random)
    {
        var node = new ResourceNode
        {
            ResourceKind = kind,
            HitPoints = hitPoints,
            MaxHitPoints = hitPoints,
            CollisionLayer = 1,
            CollisionMask = 0
        };
        node.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = size * (kind == "stone_deposit" ? 1.2f : 1.7f) } });
        var mesh = new SphereMesh
        {
            Radius = 1.0f,
            Height = 1.55f,
            RadialSegments = kind == "stone_deposit" ? 9 : 7,
            Rings = kind == "stone_deposit" ? 5 : 4
        };
        var material = new StandardMaterial3D
        {
            AlbedoColor = kind == "stone_deposit" ? new Color(0.35f, 0.36f, 0.34f) : new Color(0.48f, 0.49f, 0.46f),
            Roughness = 1.0f
        };
        node.AddChild(new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = material,
            Scale = new Vector3(size * 1.25f, size, size * (0.78f + (float)random.NextDouble() * 0.35f))
        });
        return node;
    }

    private static ResourceNode CreateTreeResourceNode(string persistentId, float trunkHeight)
    {
        var node = new ResourceNode
        {
            Name = "HarvestableTree",
            ResourceKind = "tree",
            PersistentId = persistentId,
            HitPoints = 4,
            CollisionLayer = 1,
            CollisionMask = 1
        };
        node.AddChild(new CollisionShape3D
        {
            Shape = new CylinderShape3D { Radius = 0.65f, Height = Mathf.Max(1.4f, trunkHeight * 0.82f) }
        });
        return node;
    }

    private static ArrayMesh CreateShrubLeafMesh()
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        Vector3 basePoint = Vector3.Zero;
        Vector3 left = new(-0.48f, 0.40f, 0.015f);
        Vector3 center = new(0, 0.46f, 0.075f);
        Vector3 tip = new(0, 1.0f, 0.0f);
        Vector3 right = new(0.48f, 0.40f, 0.015f);
        Color baseColor = new(0.78f, 0.87f, 0.68f);
        Color edgeColor = new(0.87f, 0.94f, 0.76f);
        Color tipColor = new(0.93f, 0.98f, 0.83f);
        AddLeafTriangle(surface, basePoint, left, center, baseColor, edgeColor, baseColor);
        AddLeafTriangle(surface, left, tip, center, edgeColor, tipColor, baseColor);
        AddLeafTriangle(surface, tip, right, center, tipColor, edgeColor, baseColor);
        AddLeafTriangle(surface, right, basePoint, center, edgeColor, baseColor, baseColor);
        AddLeafTriangle(surface, center, left, basePoint, baseColor, edgeColor, baseColor);
        AddLeafTriangle(surface, center, tip, left, baseColor, tipColor, edgeColor);
        AddLeafTriangle(surface, center, right, tip, baseColor, edgeColor, tipColor);
        AddLeafTriangle(surface, center, basePoint, right, baseColor, baseColor, edgeColor);
        surface.GenerateNormals();
        return surface.Commit() as ArrayMesh;
    }

    private static void AddLeafTriangle(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Color colorA, Color colorB, Color colorC)
    {
        AddLeafVertex(surface, a, colorA);
        AddLeafVertex(surface, b, colorB);
        AddLeafVertex(surface, c, colorC);
    }

    private static void AddLeafVertex(SurfaceTool surface, Vector3 position, Color color)
    {
        surface.SetUV(new Vector2(position.X + 0.5f, position.Y));
        surface.SetColor(color);
        surface.AddVertex(position);
    }

    private static ArrayMesh CreateGrassMesh(float heightScale = 1.0f, float widthScale = 1.0f)
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        AddGrassBlade(surface, 0.05f, 0.48f * heightScale, 0.070f * widthScale, 0.13f * heightScale);
        AddGrassBlade(surface, 2.12f, 0.39f * heightScale, 0.064f * widthScale, 0.10f * heightScale);
        AddGrassBlade(surface, 4.20f, 0.44f * heightScale, 0.068f * widthScale, 0.12f * heightScale);
        surface.GenerateNormals();
        return surface.Commit() as ArrayMesh;
    }

    private static void AddGrassBlade(SurfaceTool surface, float angle, float height, float width, float lean)
    {
        Vector3 forward = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
        Vector3 side = new Vector3(-forward.Z, 0, forward.X) * (width * 0.5f);
        Vector3 baseLeft = -side;
        Vector3 baseRight = side;
        Vector3 middle = forward * (lean * 0.45f) + Vector3.Up * (height * 0.56f);
        Vector3 middleLeft = middle - side * 0.58f;
        Vector3 middleRight = middle + side * 0.58f;
        Vector3 tip = forward * lean + Vector3.Up * height;
        Color baseColor = new(0.25f, 0.39f, 0.17f);
        Color midColor = new(0.38f, 0.55f, 0.23f);
        Color tipColor = new(0.53f, 0.66f, 0.32f);
        AddGrassTriangle(surface, baseLeft, baseRight, middleLeft, baseColor, baseColor, midColor);
        AddGrassTriangle(surface, baseRight, middleRight, middleLeft, baseColor, midColor, midColor);
        AddGrassTriangle(surface, middleLeft, middleRight, tip, midColor, midColor, tipColor);
    }

    private static void AddGrassTriangle(SurfaceTool surface, Vector3 a, Vector3 b, Vector3 c, Color colorA, Color colorB, Color colorC)
    {
        surface.SetColor(colorA); surface.AddVertex(a);
        surface.SetColor(colorB); surface.AddVertex(b);
        surface.SetColor(colorC); surface.AddVertex(c);
    }

    private static ArrayMesh CreateFlowerPetalMesh()
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.Tau / 5.0f;
            float next = (i + 1) * Mathf.Tau / 5.0f;
            surface.SetColor(Colors.White); surface.AddVertex(Vector3.Zero);
            surface.SetColor(Colors.White); surface.AddVertex(new Vector3(Mathf.Cos(angle) * 0.55f, 0, Mathf.Sin(angle) * 0.55f));
            surface.SetColor(Colors.White); surface.AddVertex(new Vector3(Mathf.Cos(next) * 0.55f, 0, Mathf.Sin(next) * 0.55f));
        }
        surface.GenerateNormals();
        return surface.Commit() as ArrayMesh;
    }

    private static void AddMultiMesh(Node3D chunk, string name, Mesh mesh, Material material, List<Transform3D> transforms)
    {
        if (transforms.Count == 0) return;
        var multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = transforms.Count };
        for (int i = 0; i < transforms.Count; i++) multimesh.SetInstanceTransform(i, transforms[i]);
        chunk.AddChild(new MultiMeshInstance3D { Name = name, Multimesh = multimesh, MaterialOverride = material });
    }

    private static void AddColoredMultiMesh(Node3D chunk, string name, Mesh mesh, Material material, List<Transform3D> transforms, List<Color> colors)
    {
        if (transforms.Count == 0) return;
        var multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = mesh, InstanceCount = transforms.Count };
        for (int i = 0; i < transforms.Count; i++) { multimesh.SetInstanceTransform(i, transforms[i]); multimesh.SetInstanceColor(i, colors[i]); }
        chunk.AddChild(new MultiMeshInstance3D { Name = name, Multimesh = multimesh, MaterialOverride = material });
    }

    private static void AddTreeCanopy(List<Transform3D> transforms, List<Color> colors, int type, Vector3 origin, float matureScale, Random random)
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
            Vector3 scale = scales[i] * matureScale * (0.88f + (float)random.NextDouble() * 0.24f);
            Vector3 jitter = new(
                ((float)random.NextDouble() - 0.5f) * 0.24f * matureScale,
                ((float)random.NextDouble() - 0.5f) * 0.16f * matureScale,
                ((float)random.NextDouble() - 0.5f) * 0.24f * matureScale);
            transforms.Add(new Transform3D(
                Basis.FromScale(scale),
                origin + offsets[i] * matureScale + jitter));
            float shade = 0.48f + (float)random.NextDouble() * 0.25f;
            colors.Add(new Color(shade * 0.78f, shade, shade * 0.62f));
        }
    }

    private void AddTreeInstances(Node3D chunk, int type, List<Transform3D> trunks, List<Transform3D> leaves, List<Color> leafColors)
    {
        if (trunks.Count == 0)
            return;

        float trunkHeight = type switch { 1 => 5.0f, 2 => 4.4f, 3 => 4.3f, _ => 3.4f };
        var trunkMesh = _treeTrunkMeshes[type] ??= new CylinderMesh
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

        var leafMesh = _treeLeafMesh ??= new SphereMesh
        {
            Radius = 1.0f,
            Height = 2.0f,
            RadialSegments = 18,
            Rings = 12
        };
        AddColoredMultiMesh(chunk, $"TreeCanopies_{type}", leafMesh, _leavesMaterials[type], leaves, leafColors);
    }
}
