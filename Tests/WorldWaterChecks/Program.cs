using System;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;

static class Checks
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Near(double a, double b, double tolerance, string message) => Check(Math.Abs(a - b) <= tolerance, $"{message}: {a} != {b}");
    static void Steps(WorldWaterGrid grid, int count) { for (int i = 0; i < count; i++) grid.Step(.05, 0); }
    static void Main()
    {
        Check(WorldWaterGrid.ChunkOf(-1, 16) == -1 && WorldWaterGrid.ChunkOf(-16, 16) == -1 && WorldWaterGrid.ChunkOf(-17, 16) == -2, "Negative chunk coordinates");
        foreach (int direction in new[] { -1, 1 })
        foreach (bool zAxis in new[] { false, true })
        {
            double Terrain(int x, int z) => ((zAxis ? z : x) == (direction == 1 ? 16 : -1)) ? -2 : 0;
            var grid = new WorldWaterGrid(16, Terrain);
            int origin = direction == 1 ? 15 : 0;
            grid.Put(zAxis ? 8 : origin, zAxis ? origin : 8, 1);
            Steps(grid, 20);
            Check(grid.Depth(zAxis ? 8 : (direction == 1 ? 16 : -1), zAxis ? (direction == 1 ? 16 : -1) : 8) > .1, "Water crosses chunk edge in each axis and direction");
            Near(grid.Volume, 1, 1e-12, "Cross-edge conservation");
            Check(grid.Cells.Values.All(v => v >= 0), "Nonnegative depths");
        }
        Console.WriteLine("PASS: four chunk edges, negative coordinates, conservation and nonnegative depths.");
        var rain = new WorldWaterGrid(4, (x, z) => x >= 4 ? -3 : 0);
        rain.SetRain(0, 0, true); rain.Step(.05, .02);
        Near(rain.RainAdded, 16 * .05 * .02, 1e-14, "Rain over selected chunk only");
        Near(rain.Volume, rain.RainAdded, 1e-14, "Rain balance");
        Check(rain.Depth(4, 1) > 0, "Rain runoff immediately crosses selected chunk edge");
        Check(!rain.IsRaining(1, 0), "Neighbor does not become a rain source");
        rain.SetRain(0, 0, false); double amount = rain.Volume; Steps(rain, 80);
        Near(rain.Volume, amount, 1e-12, "Turning off rain retains water");
        Console.WriteLine("PASS: local rainfall, runoff to dry neighboring chunks, rain off retains water.");
        var downhill = new WorldWaterGrid(4, (x, z) => z == 0 ? -x : 100);
        downhill.Put(3, 0, 10); Steps(downhill, 220);
        Check(downhill.Cells.Any(c => c.Key.X >= 12 && c.Value > .01), "Water flows through multiple unrendered chunks");
        Near(downhill.Volume, 10, 1e-10, "No render-boundary loss");
        var basin = new WorldWaterGrid(4, (x, z) => z != 0 || x < 0 || x > 8 ? 100 : x == 4 ? .5 : 0);
        basin.Put(3, 0, .25); Steps(basin, 150);
        Near(basin.Depth(5, 0), 0, 0, "Low water held by natural ridge");
        basin.Put(3, 0, 8); Steps(basin, 500);
        Check(basin.Depth(5, 0) > .1, "Natural ridge overflows across a chunk edge");
        Near(basin.Volume, 8.25, 1e-10, "Overflow conservation");
        Console.WriteLine("PASS: multi-chunk downhill flow without loaded meshes, basin filling and natural overflow.");
        bool dug = false;
        var dig = new WorldWaterGrid(4, (x, z) => z != 0 || x < 0 || x > 8 ? 100 : x == 4 && !dug ? 2 : 0);
        dig.Put(3, 0, 1); Steps(dig, 100);
        Near(dig.Depth(5, 0), 0, 0, "Ridge before excavation");
        dug = true; dig.InvalidateTerrain(); Steps(dig, 100);
        Check(dig.Depth(5, 0) > .01, "Excavation opens runoff path");
        Near(dig.Volume, 1, 1e-12, "Excavation conserves water");
        var restored = new WorldWaterGrid(4, (x, z) => z != 0 || x < 0 || x > 8 ? 100 : 0);
        var snapshot = dig.Cells.ToDictionary(c => $"{c.Key.X},{c.Key.Z}", c => c.Value);
        string json = JsonSerializer.Serialize(snapshot);
        foreach (var c in JsonSerializer.Deserialize<Dictionary<string, double>>(json)!)
        { string[] p = c.Key.Split(','); restored.Put(int.Parse(p[0]), int.Parse(p[1]), c.Value); }
        Near(restored.Volume, dig.Volume, 1e-12, "Water save roundtrip");
        // Both render chunks ask for precisely the same world-coordinate corner.
        Near(restored.CornerLevel(4, 0), dig.CornerLevel(4, 0), 1e-12, "Shared boundary vertex levels after save");
        double vessel = restored.Take(3, 0, .005);
        double beforePour = restored.Volume; restored.Put(5, 0, vessel);
        Near(restored.Volume, beforePour + vessel, 1e-12, "Vessel put/take balance");
        Console.WriteLine("PASS: excavation invalidation, save roundtrip, shared corner levels and vessel balance.");
        var evap = new WorldWaterGrid(4, (x, z) => (x == 0 || x == 2) && z == 0 ? 0 : 100);
        evap.Put(0, 0, .00001); evap.Put(2, 0, .01);
        evap.Step(.05, 0, .001);
        Near(evap.Depth(0, 0), 0, 0, "Shallow puddle dries first");
        Near(evap.Depth(2, 0), .00995, 1e-14, "Depth removed per exposed square metre");
        Near(evap.Volume + evap.Evaporated, .01001, 1e-14, "Atmospheric sink accounted for");
        double left = evap.Volume; evap.Step(.05, 0, 0);
        Near(evap.Volume, left, 1e-14, "Zero evaporation disables sink");
        var realistic = new WorldWaterGrid(4, (x, z) => x == 0 && z == 0 ? 0 : 100);
        realistic.Put(0, 0, .01);
        for (int i = 0; i < 72000; i++) realistic.Step(.05, 0, .5 / 3600000.0);
        Near(realistic.Depth(0, 0), .0095, 1e-10, "0.5 mm/h removes 0.5 mm in one simulated hour");
        Console.WriteLine("PASS: evaporation depth/rate, dry-cell removal, atmospheric mass balance and disable switch.");
        var climate = new WorldClimate(.5);
        Check(climate.CurrentSeason == WorldClimate.Season.Spring, "Spring first");
        climate.Advance(WorldClimate.SecondsPerDay * 28); Check(climate.CurrentSeason == WorldClimate.Season.Summer, "Summer transition");
        double summer = climate.EvaporationFactor;
        climate.Advance(WorldClimate.SecondsPerDay * 28); Check(climate.CurrentSeason == WorldClimate.Season.Autumn, "Autumn transition");
        climate.Advance(WorldClimate.SecondsPerDay * 28); Check(climate.CurrentSeason == WorldClimate.Season.Winter, "Winter transition");
        Check(climate.EvaporationFactor < summer, "Cold winter evaporates slower than warm summer");
        climate.Advance(WorldClimate.SecondsPerDay * 28); Check(climate.CurrentSeason == WorldClimate.Season.Spring, "Year wraps to spring");
        climate.SelectSeason(WorldClimate.Season.Summer);
        double noon = climate.EvaporationFactor;
        climate.Advance(WorldClimate.SecondsPerDay * .5);
        Check(climate.Sunlight == 0 && climate.EvaporationFactor < noon, "Night evaporates slower without sun");
        var savedClimate = new WorldClimate(climate.Day);
        Near(savedClimate.Hour, climate.Hour, 1e-10, "Saved clock restores exact time");
        Check(savedClimate.CurrentSeason == climate.CurrentSeason, "Saved season restored");
        var cloud = new WorldWaterGrid(4, (x, z) => (x == 0 || x == 8) && z == 0 ? 0 : 100);
        cloud.Put(0, 0, 1); cloud.Put(8, 0, 1); cloud.SetRain(0, 0, true); cloud.Step(.05, 0, .01);
        Check(cloud.Depth(0, 0) > cloud.Depth(8, 0), "Local rainy cloud reduces evaporation only in selected chunk");
        Console.WriteLine("PASS: four seasons, year wrap, temperature/sun evaporation, rainy cloud, manual season choice and saved clock.");
        Near(SnowClimate.SnowFraction(-5), 1, 0, "Snow below freezing");
        Near(SnowClimate.SnowFraction(0), 1, 0, "Snow at freezing");
        Near(SnowClimate.SnowFraction(1), .5, 0, "Mixed precipitation between zero and two");
        Near(SnowClimate.SnowFraction(2), 0, 0, "Rain from two degrees");
        var snow = new WorldWaterGrid(4, (x,z) => x >= 4 ? -2 : 0);
        snow.SetRain(0,0,true); snow.Step(.05,.01,0,-5,0);
        Near(snow.SnowVolume,16*.05*.01,1e-14,"Cold precipitation stored as water-equivalent snow");
        Near(snow.Volume,0,0,"Snowfall does not also create liquid water");
        snow.SetRain(0,0,false); double snowBefore=snow.SnowVolume;
        snow.Step(.05,0,0,-2,1,72);
        Near(snow.SnowVolume,snowBefore,0,"Cold snow persists when precipitation stops");
        double shaded=SnowClimate.MeltDepth(5,0,3600), sunny=SnowClimate.MeltDepth(5,1,3600);
        Check(sunny>shaded && SnowClimate.MeltDepth(10,1,3600)>sunny,"Warmth and sun accelerate melting");
        snow.Step(.05,0,0,10,1,7200);
        Check(snow.Volume>0 && snow.SnowVolume<snowBefore,"Positive temperature creates meltwater");
        Near(snow.Volume+snow.SnowVolume,snowBefore,1e-12,"Snow melt conserves water exactly");
        Check(snow.Depth(4,0)>0,"Meltwater crosses a chunk boundary immediately");
        var mixed=new WorldWaterGrid(4,(x,z)=>0);mixed.SetRain(-1,-1,true);mixed.Step(.05,.01,0,1,0);
        Check(mixed.Volume>0 && mixed.SnowVolume>0,"Mixed rain/snow both reach the world");
        Near(mixed.Volume+mixed.SnowVolume,mixed.RainAdded,1e-12,"Mixed precipitation total mass balance");
        var snowSnapshot=snow.SnowCells.ToDictionary(c=>$"{c.Key.X},{c.Key.Z}",c=>c.Value);
        var restoredSnow=new WorldWaterGrid(4,(x,z)=>0);
        foreach(var entry in JsonSerializer.Deserialize<Dictionary<string,double>>(JsonSerializer.Serialize(snowSnapshot))!)
        {var p=entry.Key.Split(',');restoredSnow.PutSnow(int.Parse(p[0]),int.Parse(p[1]),entry.Value);}
        Near(restoredSnow.SnowVolume,snow.SnowVolume,1e-12,"Snow save roundtrip");
        Near(restoredSnow.SnowCornerDepth(4,0),snow.SnowCornerDepth(4,0),1e-12,"Shared snow cover at chunk boundary");
        var thaw=new WorldWaterGrid(4,(x,z)=>0);thaw.PutSnow(0,0,.000001);thaw.Step(.05,0,0,30,1,100000);
        Near(thaw.SnowVolume,0,0,"Complete thaw removes snow entries");
        Near(thaw.Volume,.000001,1e-14,"Complete thaw returns all stored mass");
        Console.WriteLine("PASS: snow/rain thresholds, persistence, sun/warmth melt, cross-chunk runoff, mixed precipitation, snow save and full thaw mass balance.");
    }
}
