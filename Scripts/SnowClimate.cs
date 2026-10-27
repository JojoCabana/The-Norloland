using System;

// Snow mass is measured as liquid-water equivalent. Visual depth assumes fresh
// snow at 100 kg/m³ (10:1 snow/water); no compaction or ice physics yet.
public static class SnowClimate
{
    public const double DepthRatio = 10;
    public static double SnowFraction(double temperatureCelsius)
    {
        if (!double.IsFinite(temperatureCelsius)) throw new ArgumentOutOfRangeException(nameof(temperatureCelsius));
        return Math.Clamp((2 - temperatureCelsius) / 2, 0, 1);
    }
    public static double MeltDepth(double temperatureCelsius, double sunlight, double elapsedGameSeconds)
    {
        if (!double.IsFinite(temperatureCelsius) || !double.IsFinite(sunlight) || !double.IsFinite(elapsedGameSeconds) || elapsedGameSeconds < 0)
            throw new ArgumentOutOfRangeException();
        // Simplified degree-day melt: 3 mm water / positive degree / game day,
        // reduced without sunlight. All melted mass is returned to surface water.
        return Math.Max(0, temperatureCelsius) * (.3 + .7 * Math.Clamp(sunlight, 0, 1)) * .003 / 86400 * elapsedGameSeconds;
    }
}
