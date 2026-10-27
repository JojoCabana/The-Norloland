using System;

// Conservative surface-water toy model. One cell has area 1 m²; no Godot dependency.
public sealed class WaterGrid
{
    public int Width { get; }
    public int Depth { get; }
    public double[] Ground { get; }
    public double[] Water { get; }
    public bool DamClosed { get; set; } = true;
    public double DamCrest { get; set; } = 2.2;
    // -1 distributes rainfall; a cell index concentrates catchment runoff at a spring.
    public int RainSourceCell { get; set; } = -1;
    public double RainAdded { get; private set; }
    public double LastDamFlow { get; private set; }
    public double Taken { get; private set; }
    public double Returned { get; private set; }
    public double Take(int cell, double amount)
    {
        if (cell < 0 || cell >= Water.Length || !double.IsFinite(amount) || amount < 0) throw new ArgumentOutOfRangeException();
        double taken = Math.Min(Water[cell], amount);
        Water[cell] -= taken; Taken += taken;
        return taken;
    }
    public void Put(int cell, double amount)
    {
        if (cell < 0 || cell >= Water.Length || !double.IsFinite(amount) || amount < 0) throw new ArgumentOutOfRangeException();
        Water[cell] += amount; Returned += amount;
    }
    private readonly double[] _delta;
    public WaterGrid(int width, int depth)
    {
        if (width < 2 || depth < 1) throw new ArgumentOutOfRangeException();
        Width = width; Depth = depth;
        Ground = new double[width * depth]; Water = new double[width * depth];
        _delta = new double[width * depth];
    }
    public double Volume { get { double total = 0; foreach (double v in Water) total += v; return total; } }
    public void Step(double dt, double rainRate)
    {
        if (!double.IsFinite(dt) || dt <= 0 || dt > 0.1 || !double.IsFinite(rainRate) || rainRate < 0)
            throw new ArgumentOutOfRangeException();
        Array.Clear(_delta);
        LastDamFlow = 0;
        if (RainSourceCell < -1 || RainSourceCell >= Water.Length) throw new ArgumentOutOfRangeException(nameof(RainSourceCell));
        if (RainSourceCell >= 0) Water[RainSourceCell] += rainRate * dt * Water.Length;
        else for (int i = 0; i < Water.Length; i++) Water[i] += rainRate * dt;
        RainAdded += rainRate * dt * Water.Length;
        // Only positive outgoing flows are calculated. Each donor distributes at
        // most 1/4 of its old volume to each of four neighbors, so it cannot go negative.
        for (int z = 0; z < Depth; z++)
        for (int x = 0; x < Width; x++)
        {
            int source = z * Width + x;
            Transfer(source, x, z, x - 1, z, dt);
            Transfer(source, x, z, x + 1, z, dt);
            Transfer(source, x, z, x, z - 1, dt);
            Transfer(source, x, z, x, z + 1, dt);
        }
        for (int i = 0; i < Water.Length; i++) Water[i] += _delta[i];
    }
    // Shared mesh corners must not average upstream and downstream levels
    // across a closed dam. cellX identifies which bank is being rendered.
    public double CornerLevel(int vertexX, int vertexZ, int cellX)
    {
        double sum = 0; int count = 0;
        for (int dz = -1; dz <= 0; dz++)
        for (int dx = -1; dx <= 0; dx++)
        {
            int x = vertexX + dx, z = vertexZ + dz;
            if (x < 0 || x >= Width || z < 0 || z >= Depth) continue;
            if (DamClosed && (x < Width / 2) != (cellX < Width / 2)) continue;
            int i = z * Width + x;
            if (Water[i] <= 0.008) continue;
            sum += Ground[i] + Water[i]; count++;
        }
        return count > 0 ? sum / count : -10;
    }
    private void Transfer(int source, int x, int z, int nx, int nz, double dt)
    {
        if (nx < 0 || nx >= Width || nz < 0 || nz >= Depth) return; // sealed boundary
        int target = nz * Width + nx;
        double level = Ground[source] + Water[source];
        double receiver = Ground[target] + Water[target];
        bool crossesDam = z == nz && Math.Min(x, nx) == Width / 2 - 1;
        if (crossesDam && DamClosed) receiver = Math.Max(receiver, DamCrest);
        double head = level - receiver;
        if (head <= 0 || Water[source] <= 0) return;
        double amount = Math.Min(Water[source] * 0.249, head * dt * 1.8);
        // Only the volume above the crest may cross a closed dam.
        if (crossesDam && DamClosed) amount = Math.Min(amount, Math.Max(0, level - DamCrest));
        _delta[source] -= amount;
        _delta[target] += amount;
        if (crossesDam) LastDamFlow += (nx > x ? amount : -amount) / dt;
    }
}
