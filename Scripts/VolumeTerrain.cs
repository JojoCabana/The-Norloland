using System;
using System.Collections.Generic;
using System.Numerics;

// Port of 3dspiel terrain_field.gd / terrain_mesher.gd: positive density is solid,
// intersecting noise fields carve tunnels, sphere subtraction implements the cutter.
// All coordinates are global; chunk boundaries do not participate in generation.
public sealed class VolumeTerrain
{
    public const float Step = 0.5f;
    public sealed class Cut
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Radius { get; set; }
    }
    public sealed class Parcel
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public Dictionary<string, double> CubicMetres { get; set; } = new();
    }
    public readonly int Seed;
    public readonly Func<float,float,float> Height;
    public readonly Cut[] Cuts;
    private readonly Dictionary<(int,int,int),List<Cut>> _cuts = new();
    public VolumeTerrain(int seed, Func<float,float,float> height, Cut[] cuts)
    {
        Seed=seed; Height=height; Cuts=cuts;
        foreach(var c in cuts)
        for(int x=Cell(c.X-c.Radius);x<=Cell(c.X+c.Radius);x++)
        for(int y=Cell(c.Y-c.Radius);y<=Cell(c.Y+c.Radius);y++)
        for(int z=Cell(c.Z-c.Radius);z<=Cell(c.Z+c.Radius);z++)
        {
            var key=(x,y,z);
            if(!_cuts.TryGetValue(key,out var list)) _cuts[key]=list=new();
            list.Add(c);
        }
    }
    private static int Cell(float x)=>(int)MathF.Floor(x/8);
    private static float Fade(float t)=>t*t*(3-2*t);
    public static float Noise(float x,float y,float z,int seed)
    {
        int ix=(int)MathF.Floor(x),iy=(int)MathF.Floor(y),iz=(int)MathF.Floor(z);
        float tx=Fade(x-ix),ty=Fade(y-iy),tz=Fade(z-iz);
        static float L(float a,float b,float t)=>a+(b-a)*t;
        float H(int a,int b,int c)
        {
            uint h=unchecked((uint)(a*73856093^b*19349663^c*83492791^seed));
            h^=h>>16;h*=0x7feb352d;h^=h>>15;h*=0x846ca68b;h^=h>>16;
            return (h&0xffffff)/8388607.5f-1;
        }
        return L(L(L(H(ix,iy,iz),H(ix+1,iy,iz),tx),L(H(ix,iy+1,iz),H(ix+1,iy+1,iz),tx),ty),
                 L(L(H(ix,iy,iz+1),H(ix+1,iy,iz+1),tx),L(H(ix,iy+1,iz+1),H(ix+1,iy+1,iz+1),tx),ty),tz);
    }
    public float Density(Vector3 p, bool caves=true)
    {
        float depth=Height(p.X,p.Z)-p.Y;
        float d=depth;
        if(caves && depth>6)
        {
            float a=Noise(p.X*.052f,p.Y*.0624f,p.Z*.052f,Seed+401);
            float b=Noise((p.X+83)*.052f,p.Y*.0624f,(p.Z-137)*.052f,Seed+709);
            float tunnel=(MathF.Sqrt(a*a+b*b)-.155f)*18;
            float chamber=(.39f-Noise(p.X*.026f,p.Y*.02912f,p.Z*.026f,Seed+1103))*25;
            d=MathF.Min(depth,MathF.Max(MathF.Min(tunnel,chamber),6-depth));
        }
        if(_cuts.TryGetValue((Cell(p.X),Cell(p.Y),Cell(p.Z)),out var list))
            foreach(var c in list) d=MathF.Min(d,Vector3.Distance(p,new(c.X,c.Y,c.Z))-c.Radius);
        return d;
    }
    public string Material(Vector3 p)
    {
        float depth=Height(p.X,p.Z)-p.Y;
        if(depth<.45f) return "earth";
        if(depth<2.2f) return "sand";
        if(depth>7 && MathF.Abs(Noise(p.X*.19f,p.Y*.12f,p.Z*.19f,Seed+2203))<.045f)
            return "iron_ore";
        return "stone";
    }
    public Parcel Measure(Cut cut)
    {
        const float s=.25f;
        var parcel=new Parcel{X=cut.X,Y=cut.Y,Z=cut.Z};
        var center=new Vector3(cut.X,cut.Y,cut.Z);
        for(int x=(int)MathF.Floor((cut.X-cut.Radius)/s);x<MathF.Ceiling((cut.X+cut.Radius)/s);x++)
        for(int y=(int)MathF.Floor((cut.Y-cut.Radius)/s);y<MathF.Ceiling((cut.Y+cut.Radius)/s);y++)
        for(int z=(int)MathF.Floor((cut.Z-cut.Radius)/s);z<MathF.Ceiling((cut.Z+cut.Radius)/s);z++)
        {
            var p=new Vector3(x+.5f,y+.5f,z+.5f)*s;
            if(Vector3.DistanceSquared(center,p)>=cut.Radius*cut.Radius || Density(p)<=0) continue;
            string material=Material(p);
            parcel.CubicMetres.TryGetValue(material,out double amount);
            parcel.CubicMetres[material]=amount+s*s*s;
        }
        return parcel;
    }
    private static readonly Vector3[] Corners={new(0,0,0),new(1,0,0),new(1,1,0),new(0,1,0),new(0,0,1),new(1,0,1),new(1,1,1),new(0,1,1)};
    private static readonly int[][] Tets={new[]{0,5,1,6},new[]{0,1,2,6},new[]{0,2,3,6},new[]{0,3,7,6},new[]{0,7,4,6},new[]{0,4,5,6}};
    public sealed class Geometry
    {
        public Vector3 Origin;
        public List<Vector3> Vertices=new();
        public List<Vector3> Normals=new();
        // r sand, g forest litter, b earth, a stone; same contract as terrain.gdshader.
        public List<Vector4> Weights=new();
    }
    public Geometry Generate(int originX,int originZ,int size,float bottom,float top,bool caves=true)
    {
        int n=(int)MathF.Round(size/Step),ny=(int)MathF.Ceiling((top-bottom)/Step);
        var origin=new Vector3(originX,bottom,originZ);
        var geo=new Geometry{Origin=origin};
        int Stride(int x,int y,int z)=>x+(n+1)*(y+(ny+1)*z);
        var values=new float[(n+1)*(ny+1)*(n+1)];
        for(int z=0;z<=n;z++)for(int x=0;x<=n;x++)for(int y=0;y<=ny;y++)
            values[Stride(x,y,z)]=Density(origin+new Vector3(x,y,z)*Step,caves);
        var p=new Vector3[8]; var d=new float[8];
        var inside=new int[4];var outside=new int[4];
        Vector3 Cross(int a,int b)=>Vector3.Lerp(p[a],p[b],d[a]/(d[a]-d[b]));
        Vector3 Normal(Vector3 global)
        {
            const float e=.04f;
            var g=new Vector3(Density(global+new Vector3(e,0,0),caves)-Density(global-new Vector3(e,0,0),caves),
                Density(global+new Vector3(0,e,0),caves)-Density(global-new Vector3(0,e,0),caves),
                Density(global+new Vector3(0,0,e),caves)-Density(global-new Vector3(0,0,e),caves));
            return g.LengthSquared()<1e-12f?Vector3.UnitY:-Vector3.Normalize(g);
        }
        void Vertex(Vector3 local)
        {
            var global=origin+local;
            geo.Vertices.Add(local);geo.Normals.Add(Normal(global));
            float depth=Height(global.X,global.Z)-global.Y;
            string material=Material(global);
            geo.Weights.Add(material=="sand"?new Vector4(1,0,0,0):
                depth<.12f?new Vector4(0,.25f,0,0):material=="earth"?new Vector4(0,0,1,0):new Vector4(0,0,material=="iron_ore"?1:0,1));
        }
        void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector3 air)
        {
            if(Vector3.Cross(b-a,c-a).LengthSquared()<1e-12f)return;
            if(Vector3.Dot(Vector3.Cross(b-a,c-a),air-a)>0)(b,c)=(c,b);
            Vertex(a);Vertex(b);Vertex(c);
        }
        for(int z=0;z<n;z++)for(int y=0;y<ny;y++)for(int x=0;x<n;x++)
        {
            int solid=0;
            for(int i=0;i<8;i++)
            {
                var c=Corners[i];p[i]=(new Vector3(x,y,z)+c)*Step;
                d[i]=values[Stride(x+(int)c.X,y+(int)c.Y,z+(int)c.Z)];if(d[i]>=0)solid++;
            }
            if(solid==0||solid==8)continue;
            foreach(var t in Tets)
            {
                int ni=0,no=0;var air=Vector3.Zero;
                foreach(int i in t)if(d[i]>=0)inside[ni++]=i;else{outside[no++]=i;air+=p[i];}
                if(ni==0||no==0)continue;air/=no;
                if(ni==1||no==1)
                {
                    int one=ni==1?inside[0]:outside[0];var rest=ni==1?outside:inside;
                    Triangle(Cross(one,rest[0]),Cross(one,rest[1]),Cross(one,rest[2]),air);
                }
                else
                {
                    var a=Cross(inside[0],outside[0]);var b=Cross(inside[0],outside[1]);
                    var c=Cross(inside[1],outside[0]);var e=Cross(inside[1],outside[1]);
                    Triangle(a,b,c,air);Triangle(b,e,c,air);
                }
            }
        }
        return geo;
    }
}
