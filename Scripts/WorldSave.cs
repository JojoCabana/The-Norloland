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
        public List<Drop> Drops { get; set; } = new();
        public Dictionary<string, int> Health { get; set; } = new();
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
                loaded.Slots ??= new List<string>();
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
