using System;

public sealed class WaterVessel
{
    public string Name { get; set; } = "Schüssel";
    public double CapacityLitres { get; set; } = 2;
    public double Litres { get; set; }
    public double Fill(WaterGrid grid, int cell)
    {
        double amount = grid.Take(cell, Math.Max(0, CapacityLitres - Litres) / 1000.0) * 1000.0;
        Litres += amount;
        return amount;
    }
    public double Pour(WaterGrid grid, int cell)
    {
        double amount = Litres;
        grid.Put(cell, amount / 1000.0);
        Litres = 0;
        return amount;
    }
}
