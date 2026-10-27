using Godot;
using System;

public partial class Main
{
    private WorldClimate _climate;
    private DirectionalLight3D _climateSun;
    private Godot.Environment _climateEnvironment;
    private double _climateRefresh;
    public double WaterEvaporationMetresPerSecond => EvaporationMillimetresPerHour / 3600000.0 * WorldClimate.ClockScale * _climate.EvaporationFactor;
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
