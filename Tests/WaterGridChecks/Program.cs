using System;
static class Checks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Main()
    {
        var g = new WaterGrid(20, 20);
        var rng = new Random(732);
        for (int i = 0; i < g.Water.Length; i++) { g.Ground[i] = rng.NextDouble() * 5; g.Water[i] = rng.NextDouble(); }
        double initial = g.Volume;
        for (int t = 0; t < 2000; t++)
        {
            g.DamClosed = t % 3 == 0;
            g.Step(0.05, t % 5 == 0 ? 0.04 : 0);
            foreach (double v in g.Water) Check(double.IsFinite(v) && v >= -1e-12, "Negative/nonfinite water");
            Check(Math.Abs(g.Volume - initial - g.RainAdded) < 1e-8, "Water volume not conserved");
        }
        var dam = new WaterGrid(2, 1) { DamCrest = 2 };
        dam.Water[0] = 1;
        for (int t = 0; t < 100; t++) dam.Step(0.05, 0);
        Check(dam.Water[1] == 0, "Closed dam leaks below crest");
        dam.DamClosed = false;
        dam.Step(0.05, 0);
        Check(dam.Water[1] > 0 && Math.Abs(dam.Volume - 1) < 1e-12, "Open dam fails to flow");
        var overflow = new WaterGrid(2, 1) { DamCrest = 2 };
        overflow.Water[0] = 3; overflow.Step(0.05, 0);
        Check(overflow.Water[1] > 0, "Dam cannot overflow");
        var pool = new WaterGrid(2, 1);
        pool.Put(0, 0.001); // one litre
        var bowl = new WaterVessel { CapacityLitres = 2 };
        Check(Math.Abs(bowl.Fill(pool, 0) - 1) < 1e-10 && pool.Volume == 0, "Partial fill creates water");
        Check(bowl.Fill(pool, 0) == 0, "Dry pool fills vessel");
        pool.Put(0, 0.010);
        Check(Math.Abs(bowl.Fill(pool, 0) - 1) < 1e-10 && bowl.Litres == 2, "Capacity exceeded");
        Check(bowl.Fill(pool, 0) == 0, "Full vessel takes more water");
        double before = pool.Volume + bowl.Litres / 1000;
        bowl.Pour(pool, 1);
        Check(bowl.Litres == 0 && Math.Abs(pool.Volume - before) < 1e-10, "Pour loses water");
        var dry = new WaterGrid(4, 4); dry.Step(0.05, 0);
        Check(dry.Volume == 0, "Dry grid generates water");
        Console.WriteLine("PASS: conservation, nonnegative volumes, rain, closed/open dam, overflow and dry grid.");
    }
}
