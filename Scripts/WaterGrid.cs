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
    public double RainAdded { get; private set; }
    public double LastDamFlow { get; private set; }
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
        for (int i = 0; i < Water.Length; i++) Water[i] += rainRate * dt;
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
        _delta[source] -= amount;
        _delta[target] += amount;
        if (crossesDam) LastDamFlow += (nx > x ? amount : -amount) / dt;
    }
}
