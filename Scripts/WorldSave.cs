using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

// One local save slot per seed. A zero Inspector seed resumes the last world.
public static class WorldSave
{
    public sealed class Drop
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public sealed class Data
    {
        public List<WaterVessel> Vessels { get; set; } = new()
        {
            new WaterVessel { Name = "Schüssel", CapacityLitres = 2 },
            new WaterVessel { Name = "Krug", CapacityLitres = 5 }
        };
        public double ClimateDay { get; set; } = 56.5;
        public float EvaporationMillimetresPerHour { get; set; } = 0.5f;
        public Dictionary<string, double> WorldSnow { get; set; } = new();
        public Dictionary<string, double> WorldWater { get; set; } = new();
        public HashSet<string> RainChunks { get; set; } = new();
        public List<Drop> Drops { get; set; } = new();
        public Dictionary<string, int> Health { get; set; } = new();
        public List<VolumeTerrain.Cut> Cuts { get; set; } = new();
        public List<VolumeTerrain.Parcel> Parcels { get; set; } = new();
        public Dictionary<string,double> MaterialVolumes { get; set; } = new();
        public int Format { get; set; } = 1;
        public int Seed { get; set; }
        public Dictionary<string, float> Depth { get; set; } = new();
        public Dictionary<string, float> Cover { get; set; } = new();
        public HashSet<string> Consumed { get; set; } = new();
        public Dictionary<string, float> Growth { get; set; } = new();
        public Dictionary<string, int> Inventory { get; set; } = new();
        public List<string> Slots { get; set; } = new();
    }

    public static Data Current { get; private set; } = new();
    public static bool Failed { get; private set; }
    public static bool WriteFailed { get; private set; }
    private static string _path;
    private static string DirectoryPath => ProjectSettings.GlobalizePath("user://worlds");

    public static int Load(int requestedSeed)
    {
        Failed = false;
        WriteFailed = false;
        Current = new Data();
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            string active = Path.Combine(DirectoryPath, "active.txt");
            int seed = requestedSeed;
            if (seed == 0 && File.Exists(active))
                seed = int.Parse(File.ReadAllText(active));
            if (seed == 0)
                do { seed = unchecked((int)GD.Randi()); } while (seed == 0);
            _path = Path.Combine(DirectoryPath, seed + ".json");
            if (File.Exists(_path))
            {
                var loaded = JsonSerializer.Deserialize<Data>(File.ReadAllText(_path));
                if (loaded == null || loaded.Format != 1 || loaded.Seed != seed ||
                    loaded.Depth == null || loaded.Cover == null || loaded.Consumed == null ||
                    loaded.Growth == null || loaded.Inventory == null)
                    throw new InvalidDataException("Invalid world save.");
                if (!double.IsFinite(loaded.ClimateDay) || loaded.ClimateDay < 0 || loaded.ClimateDay > 100000000)
                    throw new InvalidDataException("Invalid climate clock.");
                if (!float.IsFinite(loaded.EvaporationMillimetresPerHour) || loaded.EvaporationMillimetresPerHour < 0 || loaded.EvaporationMillimetresPerHour > 5)
                    throw new InvalidDataException("Invalid evaporation rate.");
                loaded.WorldSnow ??= new();
                loaded.WorldWater ??= new();
                loaded.RainChunks ??= new();
                static bool ValidCell(string key)
                {
                    string[] parts = key.Split(',');
                    return parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int z)
                        && Math.Abs((long)x) < 100000000 && Math.Abs((long)z) < 100000000;
                }
                foreach (var cell in loaded.WorldSnow)
                    if (!ValidCell(cell.Key) || !double.IsFinite(cell.Value) || cell.Value < 0)
                        throw new InvalidDataException("Invalid world snow.");
                foreach (var cell in loaded.WorldWater)
                    if (!ValidCell(cell.Key) || !double.IsFinite(cell.Value) || cell.Value < 0)
                        throw new InvalidDataException("Invalid world water.");
                foreach (string chunk in loaded.RainChunks)
                    if (!ValidCell(chunk)) throw new InvalidDataException("Invalid rain chunk.");
                loaded.Cuts ??= new();
                loaded.Parcels ??= new();
                loaded.MaterialVolumes ??= new();
                foreach(var cut in loaded.Cuts)
                    if(cut==null || !float.IsFinite(cut.X) || !float.IsFinite(cut.Y) || !float.IsFinite(cut.Z) || !float.IsFinite(cut.Radius) || cut.Radius<=0 || cut.Radius>4)
                        throw new InvalidDataException("Invalid terrain cut.");
                loaded.Slots ??= new List<string>();
                loaded.Vessels ??= new Data().Vessels;
                if (loaded.Vessels.Count < 2) loaded.Vessels = new Data().Vessels;
                foreach (WaterVessel vessel in loaded.Vessels)
                    if (vessel == null || !double.IsFinite(vessel.CapacityLitres) || vessel.CapacityLitres <= 0 ||
                        !double.IsFinite(vessel.Litres) || vessel.Litres < 0 || vessel.Litres > vessel.CapacityLitres)
                        throw new InvalidDataException("Invalid vessel contents.");
                loaded.Drops ??= new List<Drop>();
                loaded.Health ??= new Dictionary<string, int>();
                Current = loaded;
            }
            else Current = new Data { Seed = seed };
            File.WriteAllText(active + ".tmp", seed.ToString());
            File.Move(active + ".tmp", active, true);
            return seed;
        }
        catch (Exception error)
        {
            Failed = true; // Never overwrite an unreadable existing save.
            GD.PushError("World save could not be loaded: " + error.Message);
            return requestedSeed == 0 ? 1 : requestedSeed;
        }
    }

    public static void Save()
    {
        if (Failed || string.IsNullOrEmpty(_path)) return;
        try
        {
            // Flush the complete temporary snapshot before replacing the previous one.
            using (var stream = new FileStream(_path + ".tmp", FileMode.Create, System.IO.FileAccess.Write))
            {
                JsonSerializer.Serialize(stream, Current);
                stream.Flush(true);
            }
            File.Move(_path + ".tmp", _path, true);
            WriteFailed = false;
        }
        catch (Exception error)
        {
            WriteFailed = true;
            GD.PushError("World save failed: " + error.Message);
        }
    }
}
