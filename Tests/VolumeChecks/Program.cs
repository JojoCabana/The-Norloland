using System.Numerics;
using System.Text.Json;
static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
var field=new VolumeTerrain(741932,(x,z)=>20,Array.Empty<VolumeTerrain.Cut>());
Check(field.Density(new(0,21,0))<0 && field.Density(new(0,19,0))>0,"solid terrain and surface air");
int air=0,solid=0;
for(int z=-32;z<32;z+=2)for(int x=-32;x<32;x+=2)for(int y=-12;y<12;y+=2)
{if(field.Density(new(x,y,z))<0)air++;else solid++;}
Check(air>50 && solid>50,"seeded underground tunnels and solid walls exist");
Check(field.Material(new(0,19.9f,0))=="earth" && field.Material(new(0,19,0))=="sand" && field.Material(new(0,17,0))=="stone","separate earth sand and rock layers");
var cuts=new[]{new VolumeTerrain.Cut{X=8,Y=18.5f,Z=4,Radius=1.3f}};
var parcel=field.Measure(cuts[0]);
var dug=new VolumeTerrain(741932,(x,z)=>20,cuts);
Check(parcel.CubicMetres.Values.Sum()>0,"excavation produces measured material");
Check(dug.Measure(cuts[0]).CubicMetres.Count==0,"repeated digging cannot duplicate material");
Check(dug.Density(new(8,18.5f,4))<0 && dug.Density(new(8,20,4))>=0,"excavated cavity retains its ceiling");
var restored=new VolumeTerrain(741932,(x,z)=>20,JsonSerializer.Deserialize<VolumeTerrain.Cut[]>(JsonSerializer.Serialize(cuts))!);
Check(restored.Density(new(8,18.5f,4))==dug.Density(new(8,18.5f,4)),"saved cuts reproduce the cavity");
var left=dug.Generate(0,0,8,16,21,false);var right=dug.Generate(8,0,8,16,21,false);
HashSet<string> Edge(VolumeTerrain.Geometry g){var result=new HashSet<string>();foreach(var v in g.Vertices){var p=v+g.Origin;if(MathF.Abs(p.X-8)<.0001f)result.Add($"{p.Y:F4},{p.Z:F4}");}return result;}
Check(Edge(left).SetEquals(Edge(right)) && Edge(left).Count>0,"neighboring chunks share exact boundary geometry");
Check(left.Normals.All(n=>float.IsFinite(n.X)&&MathF.Abs(n.Length()-1)<.001f),"finite unit normals");
Check(left.Vertices.Count%3==0 && left.Vertices.Count>0,"mesher produces triangles");
Console.WriteLine($"Volume terrain checks passed. Cave samples: {air}; triangles: {left.Vertices.Count/3}.");
