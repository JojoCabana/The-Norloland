using System;

// Gameplay climate, not a meteorological forecast. Clock and season survive saves.
public sealed class WorldClimate
{
    public enum Season { Spring, Summer, Autumn, Winter }
    public const int DaysPerSeason = 28;
    public const double SecondsPerDay = 1200;
    public const double ClockScale = 86400 / SecondsPerDay;
    public double Day { get; private set; }
    public WorldClimate(double day = 56.5)
    {
        if (!double.IsFinite(day) || day < 0) throw new ArgumentOutOfRangeException(nameof(day));
        Day = day;
    }
    public Season CurrentSeason => (Season)((int)(Math.Floor(Day) / DaysPerSeason) % 4);
    public int DayInSeason => (int)Math.Floor(Day) % DaysPerSeason + 1;
    public double Hour => (Day - Math.Floor(Day)) * 24;
    public string Name => CurrentSeason switch { Season.Spring => "Frühling", Season.Summer => "Sommer", Season.Autumn => "Herbst", _ => "Winter" };
    public double DaylightHours => CurrentSeason switch { Season.Spring => 12, Season.Summer => 16, Season.Autumn => 11, _ => 8 };
    public double MaxSunElevation => CurrentSeason switch { Season.Spring => 55, Season.Summer => 70, Season.Autumn => 40, _ => 20 };
    public double Sunlight
    {
        get
        {
            double progress = (Hour - (12 - DaylightHours / 2)) / DaylightHours;
            return progress <= 0 || progress >= 1 ? 0 : Math.Sin(progress * Math.PI);
        }
    }
    public double TemperatureCelsius => (CurrentSeason switch { Season.Spring => 12, Season.Summer => 24, Season.Autumn => 10, _ => -2 })
        + 4 * Math.Cos((Hour - 14) / 24 * Math.PI * 2) + 3 * Math.Sin(Day * .41);
    // A configurable reference evaporation is multiplied by warmth and sun.
    // Cold nights reduce evaporation; freezing, ice and sublimation are separate systems.
    public double EvaporationFactor => Math.Clamp((TemperatureCelsius + 5) / 25, 0, 1.8) * (.2 + .8 * Sunlight);
    public void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        Day += seconds / SecondsPerDay;
    }
    public void SelectSeason(Season season)
    {
        if ((int)season < 0 || (int)season > 3) throw new ArgumentOutOfRangeException(nameof(season));
        double yearStart = Math.Floor(Day / (DaysPerSeason * 4)) * DaysPerSeason * 4;
        Day = yearStart + (int)season * DaysPerSeason + (Day - Math.Floor(Day));
    }
}
