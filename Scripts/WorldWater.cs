using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// World rain and water stay active independently of the terrain's visual chunk cache.
public partial class WorldWater : Node3D
{
    public Main OwnerWorld { get; set; }
    private WorldWaterGrid _grid;
    private VolumeTerrain _terrain;
    private readonly Dictionary<Vector2I, MeshInstance3D> _meshes = new();
    private readonly Dictionary<Vector2I, CpuParticles3D> _rain = new();
    private readonly Dictionary<Vector2I, CpuParticles3D> _snowflakes = new();
    private readonly Dictionary<Vector2I, ImageTexture> _snowMaps = new();
    private StandardMaterial3D _crystalMaterial;
    private readonly HashSet<Vector2I> _snowOnTerrain = new();
    private readonly Dictionary<(int, int), float> _groundVertices = new();
    private readonly Dictionary<(int, int), float> _levels = new();
    private ShaderMaterial _material;
    private Node3D _outline;
    private Label _label;
    private Vector2I? _target;
    private double _accumulator, _refreshTimer;
    private int _vessel;
    private string _message = "Auf Boden zielen · R Regen umschalten";
    private const double Tick = 0.05;
    // Deliberately accelerated rainfall for testing filling and runoff in game.
    private const double RainRate = 0.01;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        _terrain = OwnerWorld.WaterTerrainSnapshot();
        _grid = new WorldWaterGrid(OwnerWorld.ChunkSize, (x, z) => SurfaceHeight(x + .5f, z + .5f));
        foreach (var entry in WorldSave.Current.WorldWater)
        {
            if (ParseCell(entry.Key, out int x, out int z)) _grid.Put(x, z, entry.Value);
        }
        foreach (var entry in WorldSave.Current.WorldSnow)
            if (ParseCell(entry.Key, out int x, out int z)) _grid.PutSnow(x, z, entry.Value);
        _crystalMaterial = CreateCrystalMaterial();
        foreach (string entry in WorldSave.Current.RainChunks)
            if (ParseCell(entry, out int x, out int z)) _grid.SetRain(x, z, true);
        if (Array.IndexOf(OS.GetCmdlineUserArgs(), "--snow-capture") >= 0) _grid.SetRain(0, 0, true);
        _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/world_water.gdshader") };
        _outline = new Node3D { Name = "RainTargetChunk", Visible = false }; AddChild(_outline);
        var border = new StandardMaterial3D { AlbedoColor = new Color(1, .75f, .15f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, NoDepthTest = true };
        for (int i = 0; i < 4; i++)
        {
            bool horizontal = i < 2;
            _outline.AddChild(new MeshInstance3D { MaterialOverride = border,
                Mesh = new BoxMesh { Size = new Vector3(horizontal ? OwnerWorld.ChunkSize : .07f, .07f, horizontal ? .07f : OwnerWorld.ChunkSize) },
                Position = new Vector3(horizontal ? OwnerWorld.ChunkSize * .5f : (i - 2) * OwnerWorld.ChunkSize, 0,
                    horizontal ? i * OwnerWorld.ChunkSize : OwnerWorld.ChunkSize * .5f) });
        }
        var layer = new CanvasLayer(); AddChild(layer);
        _label = new Label { Position = new Vector2(16, 92), MouseFilter = Control.MouseFilterEnum.Ignore };
        _label.AddThemeFontSizeOverride("font_size", 15);
        _label.AddThemeColorOverride("font_shadow_color", Colors.Black);
        _label.AddThemeConstantOverride("shadow_offset_x", 1); _label.AddThemeConstantOverride("shadow_offset_y", 1);
        layer.AddChild(_label);
    }
    private static bool ParseCell(string key, out int x, out int z)
    {
        x = z = 0; string[] parts = key.Split(',');
        return parts.Length == 2 && int.TryParse(parts[0], out x) && int.TryParse(parts[1], out z);
    }
    public void TerrainChanged()
    {
        _terrain = OwnerWorld.WaterTerrainSnapshot();
        _grid?.InvalidateTerrain(); _groundVertices.Clear(); _refreshTimer = 0;
    }
    private float SurfaceHeight(float x, float z)
    {
        float top = _terrain.Height(x, z);
        // Highest solid surface, including open excavations. This is a surface
        // solver: stacked cave pools and pressurised underground flow are not modelled.
        for (float y = top + .25f; y >= top - 64; y -= .125f)
        {
            if (_terrain.Density(new System.Numerics.Vector3(x, y, z)) < 0) continue;
            float low = y, high = y + .125f;
            for (int i = 0; i < 7; i++)
            {
                float middle = (low + high) * .5f;
                if (_terrain.Density(new System.Numerics.Vector3(x, middle, z)) >= 0) low = middle; else high = middle;
            }
            return (low + high) * .5f;
        }
        return top - 64;
    }
    private bool Aim(out Vector3 point)
    {
        point = default;
        var camera = GetViewport().GetCamera3D();
        if (camera == null) return false;
        var query = PhysicsRayQueryParameters3D.Create(camera.GlobalPosition, camera.GlobalPosition - camera.GlobalBasis.Z * 80, 1);
        query.Exclude = new Godot.Collections.Array<Rid> { OwnerWorld.WaterPlayerRid };
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0 || hit["collider"].AsGodotObject() is not StaticBody3D body || body.Name != "TerrainCollision") return false;
        point = (Vector3)hit["position"]; return true;
    }
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo || GetTree().Paused ||
            Input.MouseMode != Input.MouseModeEnum.Captured) return;
        Key code = key.PhysicalKeycode == Key.None ? key.Keycode : key.PhysicalKeycode;
        if (code == Key.V) { _vessel = (_vessel + 1) % WorldSave.Current.Vessels.Count; }
        else if (code == Key.R || code == Key.B || code == Key.P)
        {
            if (!Aim(out Vector3 point)) { _message = "Erst auf erreichbaren Boden zielen"; return; }
            int x = Mathf.FloorToInt(point.X), z = Mathf.FloorToInt(point.Z);
            int cx = WorldWaterGrid.ChunkOf(x, OwnerWorld.ChunkSize), cz = WorldWaterGrid.ChunkOf(z, OwnerWorld.ChunkSize);
            WaterVessel vessel = WorldSave.Current.Vessels[_vessel];
            if (code == Key.R)
            {
                bool on = !_grid.IsRaining(cx, cz); _grid.SetRain(cx, cz, on);
                _message = $"Niederschlag in Chunk {cx},{cz}: {(on ? "AN" : "AUS")}";
            }
            else if (code == Key.B)
            {
                double litres = _grid.Take(x, z, Math.Max(0, vessel.CapacityLitres - vessel.Litres) / 1000) * 1000;
                vessel.Litres += litres; _message = $"{litres:F3} L aufgenommen";
            }
            else
            {
                double litres = vessel.Litres; _grid.Put(x, z, litres / 1000); vessel.Litres = 0;
                _message = $"{litres:F3} L ausgegossen";
            }
            OwnerWorld.SaveWorldChanges(); _refreshTimer = 0;
        }
        else return;
        GetViewport().SetInputAsHandled();
    }
    public override void _Process(double delta)
    {
        bool playing = Input.MouseMode == Input.MouseModeEnum.Captured;
        _outline.Visible = playing && _target.HasValue; _label.Visible = playing;
        _accumulator = Math.Min(_accumulator + delta, Tick * 4);
        while (_accumulator >= Tick) { _grid.Step(Tick, RainRate, OwnerWorld.WaterEvaporationMetresPerSecond, OwnerWorld.WaterTemperatureCelsius, OwnerWorld.WaterSunlight, WorldClimate.ClockScale); _accumulator -= Tick; }
        _refreshTimer -= delta; if (_refreshTimer > 0) return; _refreshTimer = .1;
        _target = null;
        if (playing && Aim(out Vector3 point))
        {
            _target = new Vector2I(WorldWaterGrid.ChunkOf(Mathf.FloorToInt(point.X), OwnerWorld.ChunkSize),
                WorldWaterGrid.ChunkOf(Mathf.FloorToInt(point.Z), OwnerWorld.ChunkSize));
            _outline.Position = new Vector3(_target.Value.X * OwnerWorld.ChunkSize, point.Y + .08f, _target.Value.Y * OwnerWorld.ChunkSize);
        }
        string target = _target.HasValue ? $"Chunk {_target.Value.X},{_target.Value.Y} · Niederschlag {(_grid.IsRaining(_target.Value.X, _target.Value.Y) ? "AN" : "AUS")}" : "Auf Boden zielen";
        WaterVessel vessel = WorldSave.Current.Vessels[_vessel];
        _label.Text = $"R · {target} (Niederschlag beschleunigt: 10 mm/s Wasseräquivalent)\nV · {vessel.Name} {vessel.Litres:F2}/{vessel.CapacityLitres:F1} L · B füllen · P ausgießen\n{OwnerWorld.ClimateDescription}\nVerdunstung aktuell: {OwnerWorld.WaterEvaporationMetresPerSecond / WorldClimate.ClockScale * 3600000:F2} mm/Spielstunde (bei Regen ×0,35) · {_message}";
        RefreshVisuals();
    }
    private void RefreshVisuals()
    {
        var loaded = OwnerWorld.WaterVisibleChunks;
        var groups = new Dictionary<Vector2I, List<(int X, int Z)>>();
        foreach (var cell in _grid.Cells)
        {
            if (cell.Value <= WorldWaterGrid.VisibleDepth) continue;
            var chunk = new Vector2I(WorldWaterGrid.ChunkOf(cell.Key.X, OwnerWorld.ChunkSize), WorldWaterGrid.ChunkOf(cell.Key.Z, OwnerWorld.ChunkSize));
            if (!loaded.Contains(chunk)) continue;
            if (!groups.TryGetValue(chunk, out var cells)) groups[chunk] = cells = new(); cells.Add(cell.Key);
        }
        foreach (var old in _meshes.Keys.ToArray())
            if (!groups.ContainsKey(old)) { _meshes[old].QueueFree(); _meshes.Remove(old); }
        _levels.Clear();
        foreach (var group in groups)
        {
            if (!_meshes.TryGetValue(group.Key, out var visual))
            {
                visual = new MeshInstance3D { MaterialOverride = _material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    Position = new Vector3(group.Key.X * OwnerWorld.ChunkSize, 0, group.Key.Y * OwnerWorld.ChunkSize) };
                AddChild(visual); _meshes[group.Key] = visual;
            }
            BuildSurface(visual, group.Value);
        }
        RefreshSnowCover(loaded);
        RefreshPrecipitation(loaded);
    }
    private void RefreshSnowCover(HashSet<Vector2I> loaded)
    {
        int size = OwnerWorld.ChunkSize;
        var snowy = new HashSet<Vector2I>();
        foreach (var cell in _grid.SnowCells)
        {
            var chunk = new Vector2I(WorldWaterGrid.ChunkOf(cell.Key.X, size), WorldWaterGrid.ChunkOf(cell.Key.Z, size));
            // Border vertices sample neighboring cells too. Include touching neighbors
            // so shader filtering and cover are identical on both sides of a seam.
            for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
            {
                var neighbor = chunk + new Vector2I(dx, dz);
                if (loaded.Contains(neighbor)) snowy.Add(neighbor);
            }
        }
        foreach (var old in _snowOnTerrain.ToArray())
            if (!snowy.Contains(old))
            {
                OwnerWorld.SetTerrainSnow(old, null); _snowOnTerrain.Remove(old);
                if (_snowMaps.Remove(old, out var texture)) texture.Dispose();
            }
        foreach (var chunk in snowy)
        {
            using var image = Image.CreateEmpty(size + 1, size + 1, false, Image.Format.Rf);
            for (int z = 0; z <= size; z++) for (int x = 0; x <= size; x++)
                image.SetPixel(x, z, new Color((float)_grid.SnowCornerDepth(chunk.X * size + x, chunk.Y * size + z), 0, 0));
            if (!_snowMaps.TryGetValue(chunk, out var texture))
                _snowMaps[chunk] = texture = ImageTexture.CreateFromImage(image);
            else texture.Update(image);
            OwnerWorld.SetTerrainSnow(chunk, texture); _snowOnTerrain.Add(chunk);
        }
    }
    private void RefreshPrecipitation(HashSet<Vector2I> loaded)
    {
        var raining = new HashSet<Vector2I>(_grid.RainChunks.Select(c => new Vector2I(c.X, c.Z)));
        void RemoveUnused(Dictionary<Vector2I, CpuParticles3D> particles)
        {
            foreach (var old in particles.Keys.ToArray())
                if (!raining.Contains(old) || !loaded.Contains(old)) { particles[old].QueueFree(); particles.Remove(old); }
        }
        RemoveUnused(_rain); RemoveUnused(_snowflakes);
        float snowFraction = (float)SnowClimate.SnowFraction(OwnerWorld.WaterTemperatureCelsius);
        foreach (Vector2I chunk in raining)
        {
            if (!loaded.Contains(chunk)) continue;
            float top = float.NegativeInfinity;
            int size = OwnerWorld.ChunkSize;
            for (int z = 0; z <= size; z += 2) for (int x = 0; x <= size; x += 2)
                top = Math.Max(top, _terrain.Height(chunk.X * size + x, chunk.Y * size + z));
            if (!_rain.TryGetValue(chunk, out var drops))
            {
                drops = new CpuParticles3D { Amount = 512, Lifetime = 1.5, Emitting = true,
                    EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(size * .5f, .1f, size * .5f),
                    Direction = Vector3.Down, Spread = 0, InitialVelocityMin = 12, InitialVelocityMax = 14,
                    Gravity = new Vector3(0, -6, 0), Color = new Color(.6f, .8f, 1, .65f),
                    Mesh = new BoxMesh { Size = new Vector3(.015f, .2f, .015f) } };
                AddChild(drops); _rain[chunk] = drops;
            }
            drops.Emitting = snowFraction < 1;
            int dropCount = Math.Max(1, (int)(512 * (1 - snowFraction)));
            if (drops.Amount != dropCount) drops.Amount = dropCount;
            drops.Position = new Vector3((chunk.X + .5f) * size, top + 8, (chunk.Y + .5f) * size);
            if (!_snowflakes.TryGetValue(chunk, out var flakes))
            {
                flakes = new CpuParticles3D { Amount = 320, Lifetime = 12, Emitting = false, Preprocess = 6,
                    EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(size * .5f, 2, size * .5f),
                    Direction = new Vector3(.12f, -1, .06f), Spread = 15,
                    InitialVelocityMin = .6f, InitialVelocityMax = 1.1f, Gravity = new Vector3(0, -.04f, 0),
                    AngleMin = 0, AngleMax = 360, AngularVelocityMin = -25, AngularVelocityMax = 25,
                    ScaleAmountMin = .6f, ScaleAmountMax = 1.4f,
                    Mesh = new QuadMesh { Size = new Vector2(.09f, .09f), Material = _crystalMaterial } };
                AddChild(flakes); _snowflakes[chunk] = flakes;
            }
            flakes.Emitting = snowFraction > 0;
            int flakeCount = Math.Max(1, (int)(320 * snowFraction));
            if (flakes.Amount != flakeCount) flakes.Amount = flakeCount;
            flakes.Position = new Vector3((chunk.X + .5f) * size, top + 7, (chunk.Y + .5f) * size);
        }
    }
    private static StandardMaterial3D CreateCrystalMaterial()
    {
        const int size = 64;
        using var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(new Color(1, 1, 1, 0));
        void Line(Vector2 a, Vector2 b, float width)
        {
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                Vector2 p = new(x + .5f, y + .5f), ab = b - a;
                float t = Mathf.Clamp((p - a).Dot(ab) / Math.Max(ab.LengthSquared(), .001f), 0, 1);
                float opacity = Mathf.Clamp(width + .7f - p.DistanceTo(a + ab * t), 0, 1);
                if (opacity > image.GetPixel(x, y).A) image.SetPixel(x, y, new Color(.93f, .97f, 1, opacity));
            }
        }
        Vector2 center = new(size * .5f, size * .5f);
        for (int arm = 0; arm < 6; arm++)
        {
            float angle = arm * Mathf.Tau / 6;
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            Line(center, center + direction * 28, 1.1f);
            foreach (float distance in new[] { 12f, 20f })
            foreach (float side in new[] { -1f, 1f })
            {
                float branchAngle = angle + side * Mathf.Pi / 3;
                Vector2 start = center + direction * distance;
                Line(start, start + new Vector2(Mathf.Cos(branchAngle), Mathf.Sin(branchAngle)) * 8, .8f);
            }
        }
        return new StandardMaterial3D { AlbedoTexture = ImageTexture.CreateFromImage(image),
            Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor, AlphaScissorThreshold = .2f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled };
    }
    private void BuildSurface(MeshInstance3D visual, List<(int X, int Z)> cells)
    {
        using var surface = new SurfaceTool(); surface.Begin(Mesh.PrimitiveType.Triangles); bool any = false;
        (Vector3 P, float D) Corner(int x, int z)
        {
            if (!_levels.TryGetValue((x, z), out float level)) _levels[(x, z)] = level = (float)_grid.CornerLevel(x, z);
            if (!_groundVertices.TryGetValue((x, z), out float ground)) _groundVertices[(x, z)] = ground = SurfaceHeight(x, z);
            return (new Vector3(x - visual.Position.X, level, z - visual.Position.Z), level - ground);
        }
        void Triangle((Vector3 P, float D) a, (Vector3 P, float D) b, (Vector3 P, float D) c)
        {
            Span<(Vector3 P, float D)> input = stackalloc (Vector3, float)[3];
            input[0] = a; input[1] = b; input[2] = c;
            Span<(Vector3 P, float D)> clipped = stackalloc (Vector3, float)[4]; int count = 0;
            for (int i = 0; i < 3; i++)
            {
                var start = input[i]; var end = input[(i + 1) % 3];
                if (start.D > 0) clipped[count++] = start;
                if ((start.D > 0) != (end.D > 0)) clipped[count++] = (start.P.Lerp(end.P, start.D / (start.D - end.D)), 0);
            }
            void Vertex((Vector3 P, float D) v)
            {
                surface.SetNormal(Vector3.Up); surface.SetColor(new Color(Mathf.Clamp(v.D, 0, 1), 0, 0, 1)); surface.AddVertex(v.P);
            }
            for (int i = 1; i + 1 < count; i++) { Vertex(clipped[0]); Vertex(clipped[i]); Vertex(clipped[i + 1]); any = true; }
        }
        foreach (var cell in cells)
        {
            var a = Corner(cell.X, cell.Z); var b = Corner(cell.X, cell.Z + 1);
            var c = Corner(cell.X + 1, cell.Z + 1); var d = Corner(cell.X + 1, cell.Z);
            Triangle(a, b, c); Triangle(a, c, d);
        }
        Mesh old = visual.Mesh; visual.Mesh = any ? surface.Commit() : null; old?.Dispose();
    }
    public override void _ExitTree()
    {
        foreach (var pair in _snowMaps) { OwnerWorld.SetTerrainSnow(pair.Key, null); pair.Value.Dispose(); }
    }
    public void SaveTo(WorldSave.Data data)
    {
        data.WorldSnow.Clear(); foreach (var cell in _grid.SnowCells) data.WorldSnow[$"{cell.Key.X},{cell.Key.Z}"] = cell.Value;
        data.WorldWater.Clear(); foreach (var cell in _grid.Cells) data.WorldWater[$"{cell.Key.X},{cell.Key.Z}"] = cell.Value;
        data.RainChunks.Clear(); foreach (var chunk in _grid.RainChunks) data.RainChunks.Add($"{chunk.X},{chunk.Z}");
    }
}

public partial class Main
{
    public VolumeTerrain WaterTerrainSnapshot() => MakeField();
    public Rid WaterPlayerRid => _player.GetRid();
    public HashSet<Vector2I> WaterVisibleChunks => new(_chunks.Keys);
}
