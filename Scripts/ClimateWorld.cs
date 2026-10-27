using Godot;
using System;
using System.Linq;

public partial class Main
{
    private WorldClimate _climate;
    private DirectionalLight3D _climateSun;
    private Godot.Environment _climateEnvironment;
    private double _climateRefresh;
    public double WaterEvaporationMetresPerSecond => EvaporationMillimetresPerHour / 3600000.0 * WorldClimate.ClockScale * _climate.EvaporationFactor;
    public double WaterTemperatureCelsius => _climate.TemperatureCelsius;
    public double WaterSunlight => _climate.Sunlight;
    public string ClimateDescription => $"{_climate.Name} · Tag {_climate.DayInSeason}/28 · {((int)_climate.Hour):00}:{((int)((_climate.Hour % 1) * 60)):00} · {_climate.TemperatureCelsius:F1} °C";
    private void TickClimate(double delta)
    {
        _climate.Advance(delta);
        _climateRefresh -= delta; if (_climateRefresh > 0) return; _climateRefresh = .2;
        ApplyClimateLighting();
    }
    private void ApplyClimateLighting()
    {
        if (_climateSun == null || _climateEnvironment == null) return;
        float sunlight = (float)_climate.Sunlight;
        _climateSun.RotationDegrees = new Vector3(-(float)Math.Max(2, _climate.MaxSunElevation * sunlight), (float)(_climate.Hour * 15 - 180), 0);
        _climateSun.LightEnergy = sunlight * (_climate.CurrentSeason == WorldClimate.Season.Winter ? .6f : .95f);
        _climateSun.LightColor = new Color(1, Mathf.Lerp(.65f, .94f, sunlight), Mathf.Lerp(.45f, .85f, sunlight));
        _climateEnvironment.AmbientLightEnergy = Mathf.Lerp(.13f, .48f, sunlight);
        _climateEnvironment.FogLightColor = new Color(.25f, .32f, .40f).Lerp(new Color(.62f, .68f, .72f), sunlight);
    }
}

public partial class Main
{
    private readonly System.Collections.Generic.Dictionary<Vector2I, ShaderMaterial> _snowTerrainMaterials = new();
    public void SetTerrainSnow(Vector2I coordinate, Texture2D snowMap)
    {
        if (!_terrainMeshes.TryGetValue(coordinate, out var visual))
        { if (_snowTerrainMaterials.Remove(coordinate, out var old)) old.Dispose(); return; }
        if (snowMap == null)
        {
            if (_snowTerrainMaterials.Remove(coordinate, out var old))
            {
                if (visual.MaterialOverride == old) visual.MaterialOverride = _volumeMaterial;
                old.Dispose();
            }
            return;
        }
        if (!_snowTerrainMaterials.TryGetValue(coordinate, out var material))
        {
            material = (ShaderMaterial)_volumeMaterial.Duplicate();
            _snowTerrainMaterials[coordinate] = material;
        }
        material.SetShaderParameter("snow_map", snowMap);
        material.SetShaderParameter("snow_origin", new Vector2(coordinate.X * ChunkSize, coordinate.Y * ChunkSize));
        material.SetShaderParameter("snow_size", (float)ChunkSize);
        material.SetShaderParameter("snow_enabled", true);
        visual.MaterialOverride = material;
    }
}

public partial class Main
{
    private readonly System.Collections.Generic.Dictionary<ulong, (MeshInstance3D Visual, Material Original, ShaderMaterial Snow)> _snowStones = new();
    private Shader _snowStoneShader;
    public void UpdateStoneSnow(WorldWaterGrid grid)
    {
        foreach (var id in _snowStones.Keys.ToArray())
            if (!GodotObject.IsInstanceValid(_snowStones[id].Visual) || !_snowStones[id].Visual.IsInsideTree()) _snowStones.Remove(id);
        if (grid.SnowCells.Count == 0 && _snowStones.Count == 0) return;
        foreach (var chunk in _chunks.Values)
        foreach (Node node in chunk.GetChildren())
        {
            if (node is not ResourceNode resource || resource.ResourceKind is not ("small_stone" or "medium_stone" or "large_stone" or "stone_deposit")) continue;
            foreach (Node child in resource.GetChildren())
            {
                if (child is not MeshInstance3D visual) continue;
                ulong id = visual.GetInstanceId();
                float depth = (float)grid.SnowWaterEquivalent(Mathf.FloorToInt(resource.GlobalPosition.X), Mathf.FloorToInt(resource.GlobalPosition.Z)) * (float)SnowClimate.DepthRatio;
                float cover = Mathf.SmoothStep(0, .025f, depth);
                if (cover <= 0)
                {
                    if (_snowStones.Remove(id, out var old)) { visual.MaterialOverride = old.Original; old.Snow.Dispose(); }
                    continue;
                }
                if (!_snowStones.TryGetValue(id, out var entry))
                {
                    if (visual.MaterialOverride is not StandardMaterial3D original) continue;
                    _snowStoneShader ??= GD.Load<Shader>("res://Shaders/snow_stone.gdshader");
                    var material = new ShaderMaterial { Shader = _snowStoneShader };
                    material.SetShaderParameter("stone_color", original.AlbedoColor);
                    entry = (visual, original, material); _snowStones[id] = entry;
                    visual.MaterialOverride = material;
                }
                entry.Snow.SetShaderParameter("snow_cover", cover);
            }
        }
    }
}
