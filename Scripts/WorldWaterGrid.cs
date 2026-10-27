using System;
using System.Collections.Generic;

// Sparse 1 m² columns in world coordinates. Render chunks never bound the solver.
// Volumes and dry terrain survive visual chunk unloading; no Godot dependency.
public sealed class WorldWaterGrid
{
    public const double VisibleDepth = 0.0001;
    private const double FlowDepth = 0.000001;
    private readonly Func<int, int, double> _sampleGround;
    private readonly Dictionary<(int X, int Z), double> _ground = new();
    private readonly Dictionary<(int X, int Z), double> _water = new();
    private readonly Dictionary<(int X, int Z), double> _delta = new();
    private readonly List<(int X, int Z)> _sources = new();
    private readonly HashSet<(int X, int Z)> _rainChunks = new();
    public IReadOnlyDictionary<(int X, int Z), double> Cells => _water;
    public IEnumerable<(int X, int Z)> RainChunks => _rainChunks;
    public int ChunkSize { get; }
    public double RainAdded { get; private set; }
    public double Evaporated { get; private set; }
    public WorldWaterGrid(int chunkSize, Func<int, int, double> sampleGround)
    {
        if (chunkSize < 1) throw new ArgumentOutOfRangeException(nameof(chunkSize));
        ChunkSize = chunkSize; _sampleGround = sampleGround ?? throw new ArgumentNullException(nameof(sampleGround));
    }
    public static int ChunkOf(int cell, int size) => (int)Math.Floor((double)cell / size);
    public double Depth(int x, int z) => _water.TryGetValue((x, z), out double value) ? value : 0;
    public double Ground(int x, int z)
    {
        if (!_ground.TryGetValue((x, z), out double value))
        {
            value = _sampleGround(x, z);
            if (!double.IsFinite(value)) throw new InvalidOperationException("Invalid water terrain height.");
            _ground[(x, z)] = value;
        }
        return value;
    }
    public void InvalidateTerrain() => _ground.Clear();
    public bool IsRaining(int chunkX, int chunkZ) => _rainChunks.Contains((chunkX, chunkZ));
    public void SetRain(int chunkX, int chunkZ, bool enabled)
    {
        if (enabled) _rainChunks.Add((chunkX, chunkZ)); else _rainChunks.Remove((chunkX, chunkZ));
    }
    public void Put(int x, int z, double cubicMetres)
    {
        if (!double.IsFinite(cubicMetres) || cubicMetres < 0) throw new ArgumentOutOfRangeException(nameof(cubicMetres));
        if (cubicMetres > 0) _water[(x, z)] = Depth(x, z) + cubicMetres;
    }
    public double Take(int x, int z, double cubicMetres)
    {
        if (!double.IsFinite(cubicMetres) || cubicMetres < 0) throw new ArgumentOutOfRangeException(nameof(cubicMetres));
        double taken = Math.Min(Depth(x, z), cubicMetres);
        double remaining = Depth(x, z) - taken;
        if (remaining == 0) _water.Remove((x, z)); else _water[(x, z)] = remaining;
        return taken;
    }
    public double Volume { get { double sum = 0; foreach (double value in _water.Values) sum += value; return sum; } }
    public void Step(double dt, double rainRate, double evaporationMetresPerSecond = 0)
    {
        if (!double.IsFinite(dt) || dt <= 0 || dt > 0.1 || !double.IsFinite(rainRate) || rainRate < 0 || !double.IsFinite(evaporationMetresPerSecond) || evaporationMetresPerSecond < 0)
            throw new ArgumentOutOfRangeException();
        foreach (var chunk in _rainChunks)
        for (int z = 0; z < ChunkSize; z++)
        for (int x = 0; x < ChunkSize; x++)
            Put(chunk.X * ChunkSize + x, chunk.Z * ChunkSize + z, rainRate * dt);
        RainAdded += _rainChunks.Count * (double)ChunkSize * ChunkSize * rainRate * dt;
        _sources.Clear();
        foreach (var cell in _water) if (cell.Value > FlowDepth) _sources.Add(cell.Key);
        _delta.Clear();
        foreach (var source in _sources)
        {
            double volume = Depth(source.X, source.Z);
            double level = Ground(source.X, source.Z) + volume;
            Transfer(source, (source.X - 1, source.Z), level, volume, dt);
            Transfer(source, (source.X + 1, source.Z), level, volume, dt);
            Transfer(source, (source.X, source.Z - 1), level, volume, dt);
            Transfer(source, (source.X, source.Z + 1), level, volume, dt);
        }
        foreach (var change in _delta)
        {
            double remaining = Depth(change.Key.X, change.Key.Z) + change.Value;
            if (remaining == 0) _water.Remove(change.Key); else _water[change.Key] = remaining;
        }
        Evaporate(evaporationMetresPerSecond * dt);
    }
    private void Evaporate(double depth)
    {
        if (depth <= 0) return;
        _sources.Clear(); _sources.AddRange(_water.Keys);
        foreach (var cell in _sources)
        {
            double weather = IsRaining(ChunkOf(cell.X, ChunkSize), ChunkOf(cell.Z, ChunkSize)) ? .35 : 1;
            double before = _water[cell], removed = Math.Min(before, depth * weather);
            double remaining = before - removed;
            if (remaining == 0) _water.Remove(cell); else _water[cell] = remaining;
            Evaporated += removed; // Evaporation is the only intentional atmospheric sink.
        }
    }
    private void Transfer((int X, int Z) source, (int X, int Z) target, double level, double volume, double dt)
    {
        double head = level - Ground(target.X, target.Z) - Depth(target.X, target.Z);
        if (head <= 0) return;
        // At most 4 * .249 of the donor's pre-step volume can leave. All edges
        // use the same snapshot; traversal order cannot create or duplicate water.
        double amount = Math.Min(volume * 0.249, head * dt * 1.8);
        if (amount < 1e-10) return; // Retain sub-droplet amounts, never delete them.
        _delta.TryGetValue(source, out double outgoing); _delta[source] = outgoing - amount;
        _delta.TryGetValue(target, out double incoming); _delta[target] = incoming + amount;
    }
    public double CornerLevel(int x, int z)
    {
        double sum = 0; int count = 0;
        for (int dz = -1; dz <= 0; dz++)
        for (int dx = -1; dx <= 0; dx++)
        {
            double depth = Depth(x + dx, z + dz);
            if (depth <= VisibleDepth) continue;
            sum += Ground(x + dx, z + dz) + depth; count++;
        }
        return count > 0 ? sum / count : Ground(x, z);
    }
}
