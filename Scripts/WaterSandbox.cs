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
        _grid = new WaterGrid(Size, Size) { DamCrest = -0.7 };
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
        _camera = new Camera3D { Current = true, Far = 100, Fov = 55 }; AddChild(_camera); UpdateCamera();
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
            for (int i = 0; i < 500; i++) _grid.Step(Tick, 0.04);
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
            case Key.D: _grid.DamClosed = !_grid.DamClosed; _dam.Visible = _grid.DamClosed; break;
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
        var reset = new WaterGrid(Size, Size) { DamClosed = _grid.DamClosed, DamCrest = _grid.DamCrest };
        Array.Copy(_grid.Ground, reset.Ground, _grid.Ground.Length);
        return reset;
    }
    public override void _Process(double delta)
    {
        if (_capturePreview && ++_previewFrames == 180)
        {
            string path = ProjectSettings.GlobalizePath("res://build/water-preview.png");
            var error = GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print("Water preview: " + path + " (" + error + ")");
            GetTree().Quit(error == Error.Ok ? 0 : 1);
        }
        _rain.Emitting = _raining && !_paused;
        if (!_paused)
        {
            _accumulator = Math.Min(_accumulator + delta, Tick * 4);
            bool changed = false;
            while (_accumulator >= Tick)
            {
                _grid.Step(Tick, _raining ? 0.04 : 0);
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
                $"R Regen: {(_raining ? "AN" : "AUS")}   D Damm: {(_grid.DamClosed ? "ZU" : "OFFEN")}   Leertaste: Pause {(_paused ? "AN" : "AUS")}\n" +
                "← → Kamera   F Lens Flare   Rücktaste: Wasser leeren   F7 / Esc: Spielfigur\n" +
                $"Wasser: {_grid.Volume:F2} m³   Regen gesamt: {_grid.RainAdded:F2} m³\n" +
                $"Durchfluss am Damm: {_grid.LastDamFlow:F2} m³/s   Bilanzfehler: {_grid.Volume - _grid.RainAdded + _grid.Taken - _grid.Returned:E1}\n" +
                $"V Gefäß: {WorldSave.Current.Vessels[_selectedVessel].Name}  {WorldSave.Current.Vessels[_selectedVessel].Litres:F3}/{WorldSave.Current.Vessels[_selectedVessel].CapacityLitres:F1} L · B füllen · P ausgießen\n" +
                $"Zelle {sx},{sz}: {_grid.Water[_selectedCell] * 1000:F3} L · {_waterMessage}\n" +
                "Beschleunigter Regen · Laborwasser temporär · Gefäßfüllung gespeichert";
        }
    }
    private void UpdateWater()
    {
        var heights = new float[Size + 1, Size + 1];
        for (int z = 0; z <= Size; z++)
        for (int x = 0; x <= Size; x++)
        {
            double sum = 0; int count = 0;
            for (int dz = -1; dz <= 0; dz++)
            for (int dx = -1; dx <= 0; dx++)
            {
                int cx = x + dx, cz = z + dz;
                if (cx < 0 || cx >= Size || cz < 0 || cz >= Size) continue;
                int i = cz * Size + cx;
                if (_grid.Water[i] <= 0.008) continue;
                sum += _grid.Ground[i] + _grid.Water[i]; count++;
            }
            heights[x,z] = count > 0 ? (float)(sum / count) : -10;
        }
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        bool any = false;
        for (int z = 0; z < Size; z++)
        for (int x = 0; x < Size; x++)
        {
            if (_grid.Water[z * Size + x] <= 0.008) continue;
            any = true;
            Vector3 a = new(x - 10, heights[x,z], z - 10);
            Vector3 b = new(x - 10, heights[x,z+1], z - 9);
            Vector3 c = new(x - 9, heights[x+1,z+1], z - 9);
            Vector3 d = new(x - 9, heights[x+1,z], z - 10);
            surface.SetNormal(Vector3.Up);
            surface.AddVertex(a); surface.AddVertex(b); surface.AddVertex(c);
            surface.AddVertex(a); surface.AddVertex(c); surface.AddVertex(d);
        }
        _waterSurface.Mesh = any ? surface.Commit() : null;
    }
}
