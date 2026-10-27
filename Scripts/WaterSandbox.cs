using Godot;
using System;

public partial class WaterSandbox : Node3D
{
    public Main OwnerWorld { get; set; }
    private const int Size = 20;
    private const double Tick = 0.05;
    private WaterGrid _grid;
    private MultiMesh _water;
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
        _grid = new WaterGrid(Size, Size);
        for (int z = 0; z < Size; z++)
        for (int x = 0; x < Size; x++)
        {
            float cross = (z - 9.5f) / 9.5f;
            _grid.Ground[z * Size + x] = 0.15 + cross * cross * 2.5 + (Size - 1 - x) * 0.035;
        }
        var ground = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true, Mesh = new BoxMesh(), InstanceCount = Size * Size };
        for (int z = 0; z < Size; z++)
        for (int x = 0; x < Size; x++)
        {
            int i = z * Size + x;
            float h = (float)_grid.Ground[i] + 0.8f;
            ground.SetInstanceTransform(i, new Transform3D(Basis.FromScale(new Vector3(1, h, 1)), new Vector3(x - 9.5f, h * 0.5f - 0.8f, z - 9.5f)));
            ground.SetInstanceColor(i, new Color(0.26f + h * 0.035f, 0.34f + h * 0.025f, 0.18f));
        }
        AddChild(new MultiMeshInstance3D { Multimesh = ground,
            MaterialOverride = new StandardMaterial3D { VertexColorUseAsAlbedo = true, Roughness = 1 } });
        _water = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new PlaneMesh { Size = new Vector2(1.01f, 1.01f), SubdivideWidth = 2, SubdivideDepth = 2 }, InstanceCount = Size * Size };
        AddChild(new MultiMeshInstance3D { Multimesh = _water,
            MaterialOverride = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/sandbox_water.gdshader") } });
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
        var light = new DirectionalLight3D { LightColor = new Color(1, 0.88f, 0.7f), LightEnergy = 1.3f, ShadowEnabled = true };
        AddChild(light); light.Position = _sun; light.LookAt(ToGlobal(Vector3.Zero));
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
        UpdateWater();
    }
    private void UpdateCamera()
    {
        _camera.Position = new Vector3(Mathf.Cos(_angle) * 24, 20, Mathf.Sin(_angle) * 24);
        _camera.LookAt(ToGlobal(new Vector3(0, 0.8f, 0)));
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if (input is not InputEventKey key || !key.Pressed || key.Echo) return;
        switch (key.PhysicalKeycode)
        {
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
        var reset = new WaterGrid(Size, Size) { DamClosed = _grid.DamClosed };
        Array.Copy(_grid.Ground, reset.Ground, _grid.Ground.Length);
        return reset;
    }
    public override void _Process(double delta)
    {
        _rain.Emitting = _raining && !_paused;
        if (!_paused)
        {
            _accumulator = Math.Min(_accumulator + delta, Tick * 4);
            while (_accumulator >= Tick)
            {
                _grid.Step(Tick, _raining ? 0.04 : 0);
                _accumulator -= Tick;
            }
            UpdateWater();
        }
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
                $"Durchfluss am Damm: {_grid.LastDamFlow:F2} m³/s   Bilanzfehler: {_grid.Volume - _grid.RainAdded:E1}\n" +
                "Geschlossene Ränder · beschleunigter Regen · kein Wasserrad / kein Speichern";
        }
    }
    private void UpdateWater()
    {
        for (int z = 0; z < Size; z++)
        for (int x = 0; x < Size; x++)
        {
            int i = z * Size + x;
            bool wet = _grid.Water[i] > 0.008;
            _water.SetInstanceTransform(i, new Transform3D(Basis.FromScale(wet ? Vector3.One : Vector3.One * 0.0001f),
                new Vector3(x - 9.5f, wet ? (float)(_grid.Ground[i] + _grid.Water[i]) : -10, z - 9.5f)));
        }
    }
}
