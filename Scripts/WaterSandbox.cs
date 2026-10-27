using Godot;
using System;

public partial class WaterSandbox : Node3D
{
    public Main OwnerWorld { get; set; }
    private bool _capturePreview;
    private int _previewFrames;
    private const int Size = 20;
    private const double Tick = 0.05;
    private WaterGrid _grid;
    private MeshInstance3D _waterSurface;
    private int _selectedCell = 10 * Size + 8;
    private int _selectedVessel;
    private MeshInstance3D _selection;
    private string _waterMessage = "Gefäß mit V wählen, Zelle anklicken, B füllen / P ausgießen";
    private Node3D _dam;
    private Camera3D _camera;
    private Label _hud;
    private CpuParticles3D _rain;
    private ColorRect _flare;
    private ShaderMaterial _flareMaterial;
    private bool _freeLook;
    private bool _raining = true;
    private bool _paused;
    private bool _flareEnabled = true;
    private double _accumulator;
    private double _hudTimer;
    private float _angle = -0.8f;
    private readonly Vector3 _sun = new(13, 14, -14);

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _grid = new WaterGrid(Size, Size) { DamCrest = -0.7, RainSourceCell = 10 * Size + 9 };
        for (int z = 0; z < Size; z++)
        for (int x = 0; x < Size; x++)
        {
            float wx = GlobalPosition.X + x - 9.5f;
            float wz = GlobalPosition.Z + z - 9.5f;
            _grid.Ground[z * Size + x] = (OwnerWorld.GetGroundHeight(wx - 0.5f, wz - 0.5f)
                + OwnerWorld.GetGroundHeight(wx + 0.5f, wz - 0.5f)
                + OwnerWorld.GetGroundHeight(wx - 0.5f, wz + 0.5f)
                + OwnerWorld.GetGroundHeight(wx + 0.5f, wz + 0.5f)) * 0.25 - GlobalPosition.Y;
        }
        _waterSurface = new MeshInstance3D {
            MaterialOverride = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/sandbox_water.gdshader") } };
        AddChild(_waterSurface);
        _selection = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.30f, OuterRadius = 0.40f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(1, 0.8f, 0.1f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } };
        AddChild(_selection);
        _dam = new Node3D(); AddChild(_dam);
        for (int z = 0; z < Size; z++)
        {
            float groundY = Math.Min((float)_grid.Ground[z * Size + 9], (float)_grid.Ground[z * Size + 10]);
            float height = (float)_grid.DamCrest - groundY;
            if (height <= 0) continue;
            _dam.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.16f, height, 1) },
                Position = new Vector3(0, groundY + height * 0.5f, z - 9.5f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.48f, 0.34f, 0.19f), Roughness = 0.9f } });
        }
        _camera = new Camera3D { Current = true, Far = 1000, Fov = 55 }; AddChild(_camera); UpdateCamera();
        _rain = new CpuParticles3D {
            Amount = 500, Lifetime = 1.5, Emitting = true,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(10, 0.2f, 10),
            Direction = Vector3.Down, Spread = 3, InitialVelocityMin = 8, InitialVelocityMax = 11,
            Gravity = new Vector3(0, -3, 0), Position = new Vector3(0, 12, 0),
            Mesh = new BoxMesh { Size = new Vector3(0.015f, 0.23f, 0.015f) },
            Color = new Color(0.65f, 0.80f, 0.95f, 0.65f)
        }; AddChild(_rain);
        var layer = new CanvasLayer(); AddChild(layer);
        _flareMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/sandbox_flare.gdshader") };
        _flare = new ColorRect { Material = _flareMaterial, MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(_flare); _flare.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var panel = new PanelContainer { Position = new Vector2(14, 14), MouseFilter = Control.MouseFilterEnum.Ignore };
        _hud = new Label { MouseFilter = Control.MouseFilterEnum.Ignore }; panel.AddChild(_hud); layer.AddChild(panel);
        _capturePreview = Array.IndexOf(OS.GetCmdlineUserArgs(), "--water-preview") >= 0;
        if (_capturePreview)
        {
            for (int i = 0; i < 500; i++) _grid.Step(Tick, 0.004);
            _raining = false;
        }
        UpdateWater();
    }
    private void UpdateCamera()
    {
        _camera.Position = new Vector3(Mathf.Cos(_angle) * 24, 20, Mathf.Sin(_angle) * 24);
        _camera.LookAt(ToGlobal(new Vector3(0, 0.8f, 0)));
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if (input is InputEventMouseButton lookButton && lookButton.ButtonIndex == MouseButton.Right)
        {
            _freeLook = lookButton.Pressed;
            Input.MouseMode = _freeLook ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (input is InputEventMouseMotion motion && _freeLook)
        {
            Vector3 rotation = _camera.Rotation;
            rotation.Y -= motion.Relative.X * 0.003f;
            rotation.X = Mathf.Clamp(rotation.X - motion.Relative.Y * 0.003f, -1.5f, 1.5f);
            _camera.Rotation = rotation;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (input is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
        {
            Vector3 origin = _camera.ProjectRayOrigin(mouse.Position);
            Vector3 direction = _camera.ProjectRayNormal(mouse.Position);
            if (Mathf.Abs(direction.Y) > 0.001f)
            {
                float t = (GlobalPosition.Y - 1 - origin.Y) / direction.Y;
                Vector3 local = ToLocal(origin + direction * t);
                int x = Mathf.FloorToInt(local.X + 10), z = Mathf.FloorToInt(local.Z + 10);
                if (t > 0 && x >= 0 && x < Size && z >= 0 && z < Size) _selectedCell = z * Size + x;
            }
        }
        if (input is not InputEventKey key || !key.Pressed || key.Echo) return;
        switch (key.PhysicalKeycode)
        {
            case Key.V: _selectedVessel = (_selectedVessel + 1) % WorldSave.Current.Vessels.Count; break;
            case Key.B:
                _waterMessage = $"Entnommen: {WorldSave.Current.Vessels[_selectedVessel].Fill(_grid, _selectedCell):F3} Liter";
                WorldSave.Save(); UpdateWater(); break;
            case Key.P:
                _waterMessage = $"Ausgegossen: {WorldSave.Current.Vessels[_selectedVessel].Pour(_grid, _selectedCell):F3} Liter";
                WorldSave.Save(); UpdateWater(); break;
            case Key.R: _raining = !_raining; break;
            case Key.G: _grid.DamClosed = !_grid.DamClosed; _dam.Visible = _grid.DamClosed; UpdateWater(); break;
            case Key.Space: _paused = !_paused; break;
            case Key.F: _flareEnabled = !_flareEnabled; break;
            case Key.Left: _angle -= 0.2f; UpdateCamera(); break;
            case Key.Right: _angle += 0.2f; UpdateCamera(); break;
            case Key.Backspace: Array.Clear(_grid.Water); _grid = ResetGrid(); break;
            case Key.Escape: OwnerWorld?.ToggleWaterLabor(); break;
        }
    }
    private WaterGrid ResetGrid()
    {
        var reset = new WaterGrid(Size, Size) { DamClosed = _grid.DamClosed, DamCrest = _grid.DamCrest, RainSourceCell = _grid.RainSourceCell };
        Array.Copy(_grid.Ground, reset.Ground, _grid.Ground.Length);
        return reset;
    }
    public override void _Process(double delta)
    {
        if (_capturePreview && ++_previewFrames == 30)
        {
            string path = ProjectSettings.GlobalizePath("res://build/water-preview.png");
            var error = GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print("Water preview: " + path + " (" + error + ")");
            GetTree().Quit(error == Error.Ok ? 0 : 1);
        }
        // Camera remains usable while the water simulation is paused.
        Vector3 flight = Vector3.Zero;
        if (Input.IsPhysicalKeyPressed(Key.W)) flight -= _camera.Basis.Z;
        if (Input.IsPhysicalKeyPressed(Key.S)) flight += _camera.Basis.Z;
        if (Input.IsPhysicalKeyPressed(Key.A)) flight -= _camera.Basis.X;
        if (Input.IsPhysicalKeyPressed(Key.D)) flight += _camera.Basis.X;
        if (Input.IsPhysicalKeyPressed(Key.E)) flight += Vector3.Up;
        if (Input.IsPhysicalKeyPressed(Key.Q)) flight -= Vector3.Up;
        if (flight.LengthSquared() > 0)
            _camera.Position += flight.Normalized() * (Input.IsPhysicalKeyPressed(Key.Shift) ? 24 : 8) * (float)Math.Min(delta, 0.1);
        _rain.Emitting = _raining && !_paused;
        if (!_paused)
        {
            _accumulator = Math.Min(_accumulator + delta, Tick * 4);
            bool changed = false;
            while (_accumulator >= Tick)
            {
                _grid.Step(Tick, _raining ? 0.004 : 0);
                _accumulator -= Tick;
                changed = true;
            }
            if (changed) UpdateWater();
        }
        int sx = _selectedCell % Size, sz = _selectedCell / Size;
        _selection.Position = new Vector3(sx - 9.5f, (float)(_grid.Ground[_selectedCell] + _grid.Water[_selectedCell]) + 0.06f, sz - 9.5f);
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        Vector2 sunPixel = _camera.UnprojectPosition(ToGlobal(_sun));
        _flare.Visible = _flareEnabled && !_camera.IsPositionBehind(ToGlobal(_sun)) &&
            sunPixel.X >= 0 && sunPixel.X <= viewport.X && sunPixel.Y >= 0 && sunPixel.Y <= viewport.Y;
        _flareMaterial.SetShaderParameter("sun_uv", sunPixel / viewport);
        _flareMaterial.SetShaderParameter("aspect", viewport.X / viewport.Y);
        _flareMaterial.SetShaderParameter("strength", _raining ? 0.16f : 0.45f);
        _hudTimer -= delta;
        if (_hudTimer <= 0)
        {
            _hudTimer = 0.2;
            _hud.Text = $"WASSERLABOR v{Main.AppVersion}  ·  Testbereich in der Spielwelt\n" +
                $"R Regen: {(_raining ? "AN" : "AUS")}   G Damm: {(_grid.DamClosed ? "ZU" : "OFFEN")}   Leertaste: Pause {(_paused ? "AN" : "AUS")}\n" +
                "WASD Flug · Q/E runter/hoch · Shift schneller · rechte Maustaste: umsehen\n" +
                "F11 Vollbild   ← → Übersicht   F Lens Flare   Rücktaste: leeren   F7 / Esc: Spielfigur\n" +
                $"Wasser: {_grid.Volume:F2} m³   Regen gesamt: {_grid.RainAdded:F2} m³\n" +
                $"Dammkrone: {_grid.DamCrest:F2} m · {(_grid.DamClosed ? "Überlauf" : "Durchfluss")}: {_grid.LastDamFlow:F2} m³/s   Bilanzfehler: {_grid.Volume - _grid.RainAdded + _grid.Taken - _grid.Returned:E1}\n" +
                $"V Gefäß: {WorldSave.Current.Vessels[_selectedVessel].Name}  {WorldSave.Current.Vessels[_selectedVessel].Litres:F3}/{WorldSave.Current.Vessels[_selectedVessel].CapacityLitres:F1} L · B füllen · P ausgießen\n" +
                $"Zelle {sx},{sz}: {_grid.Water[_selectedCell] * 1000:F3} L · {_waterMessage}\n" +
                "Regenzufluss in der Mitte · Laborwasser temporär · Gefäßfüllung gespeichert";
        }
    }
    private void UpdateWater()
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        bool any = false;
        // Clip each surface triangle against the actual terrain, rather than
        // exposing whole square cells at the shoreline. Vertex red stores depth.
        void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            float Depth(Vector3 v) => v.Y - (OwnerWorld.GetGroundHeight(
                GlobalPosition.X + v.X, GlobalPosition.Z + v.Z) - GlobalPosition.Y);
            var input = new System.Collections.Generic.List<(Vector3 p, float depth)> {
                (a, Depth(a)), (b, Depth(b)), (c, Depth(c)) };
            var clipped = new System.Collections.Generic.List<(Vector3 p, float depth)>();
            for (int i = 0; i < input.Count; i++)
            {
                var start = input[i]; var end = input[(i + 1) % input.Count];
                bool inside = start.depth > 0, nextInside = end.depth > 0;
                if (inside) clipped.Add(start);
                if (inside != nextInside)
                {
                    float t = start.depth / (start.depth - end.depth);
                    clipped.Add((start.p.Lerp(end.p, t), 0));
                }
            }
            if (clipped.Count < 3) return;
            any = true;
            void Vertex((Vector3 p, float depth) v)
            {
                surface.SetNormal(Vector3.Up);
                surface.SetColor(new Color(Mathf.Clamp(v.depth, 0, 1), 0, 0, 1));
                surface.AddVertex(v.p);
            }
            for (int i = 1; i + 1 < clipped.Count; i++)
            {
                Vertex(clipped[0]); Vertex(clipped[i]); Vertex(clipped[i + 1]);
            }
        }
        for (int z = 0; z < Size; z++)
        for (int x = 0; x < Size; x++)
        {
            if (_grid.Water[z * Size + x] <= 0.008) continue;
            Vector3 Corner(int vx, int vz)
            {
                float level = (float)_grid.CornerLevel(vx, vz, x);
                float px = vx - 10;
                // The simulation barrier has zero width, the visible wall is
                // 16 cm thick. Stop below-crest surfaces at the wall faces.
                if (_grid.DamClosed && vx == Size / 2 && level <= _grid.DamCrest + 0.03)
                    px += x < Size / 2 ? -0.081f : 0.081f;
                return new Vector3(px, level, vz - 10);
            }
            Vector3 a = Corner(x, z);
            Vector3 b = Corner(x, z + 1);
            Vector3 c = Corner(x + 1, z + 1);
            Vector3 d = Corner(x + 1, z);
            Triangle(a, b, c); Triangle(a, c, d);
        }
        _waterSurface.Mesh = any ? surface.Commit() : null;
    }
}
