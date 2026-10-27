using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NVector = System.Numerics.Vector3;

public partial class Main
{
    private readonly HashSet<Vector2I> _volumeDirty = new();
    private Task<VolumeTerrain.Geometry> _volumeTask;
    private Vector2I _volumeCoordinate;
    private Node3D _volumeOwner;
    private readonly HashSet<Vector2I> _volumeReady = new();
    private float _volumeBottom = -32;
    private bool _closing;

    private VolumeTerrain MakeField(bool snapshot=false, Vector2I coordinate=default)
    {
        var cuts=WorldSave.Current.Cuts.ToArray();
        if(!snapshot) return new VolumeTerrain(WorldSeed,SampleOriginalSurface,cuts);
        int ox=coordinate.X*ChunkSize-1,oz=coordinate.Y*ChunkSize-1,n=ChunkSize+3;
        var grid=new float[n,n];
        for(int x=0;x<n;x++)for(int z=0;z<n;z++)grid[x,z]=GetHeight(ox+x,oz+z);
        float Height(float x,float z)
        {
            float fx=Math.Clamp(x-ox,0,n-1.001f),fz=Math.Clamp(z-oz,0,n-1.001f);
            int ix=(int)MathF.Floor(fx),iz=(int)MathF.Floor(fz);
            float tx=fx-ix,tz=fz-iz;
            return (grid[ix,iz]*(1-tx)+grid[ix+1,iz]*tx)*(1-tz)+(grid[ix,iz+1]*(1-tx)+grid[ix+1,iz+1]*tx)*tz;
        }
        return new VolumeTerrain(WorldSeed,Height,cuts);
    }
    private float SampleOriginalSurface(float x,float z)
    {
        int ix=Mathf.FloorToInt(x),iz=Mathf.FloorToInt(z);
        float tx=x-ix,tz=z-iz;
        return Mathf.Lerp(Mathf.Lerp(GetHeight(ix,iz),GetHeight(ix+1,iz),tx),
            Mathf.Lerp(GetHeight(ix,iz+1),GetHeight(ix+1,iz+1),tx),tz);
    }
    public bool TerrainReadyAt(Vector3 position)
    {
        var coordinate=new Vector2I(Mathf.FloorToInt(position.X/ChunkSize),Mathf.FloorToInt(position.Z/ChunkSize));
        return _chunks.ContainsKey(coordinate) && (_waterLabChunks.Contains(coordinate)||_volumeReady.Contains(coordinate));
    }
    public string GroundMaterialAt(Vector3 position)=>MakeField().Material(new NVector(position.X,position.Y-.05f,position.Z));
    public bool IsRockLayerAt(Vector3 position)=>GroundMaterialAt(position) is "stone" or "iron_ore";
    public double ExcavateVolume(Vector3 position,float radius)
    {
        if(_waterLab!=null)return 0; // The surface-water laboratory remains a separate test.
        var field=MakeField();
        var cut=new VolumeTerrain.Cut{X=position.X,Y=position.Y,Z=position.Z,Radius=radius};
        var parcel=field.Measure(cut);
        double amount=parcel.CubicMetres.Values.Sum();
        if(amount<=0)return 0;
        WorldSave.Current.Cuts.Add(cut);
        WorldSave.Current.Parcels.Add(parcel);
        foreach(var pair in parcel.CubicMetres)
        {
            WorldSave.Current.MaterialVolumes.TryGetValue(pair.Key,out double old);
            WorldSave.Current.MaterialVolumes[pair.Key]=old+pair.Value;
        }
        foreach(var pair in _chunks)
        {
            float x=pair.Key.X*ChunkSize,z=pair.Key.Y*ChunkSize;
            if(position.X+radius<x||position.X-radius>x+ChunkSize||position.Z+radius<z||position.Z-radius>z+ChunkSize)continue;
            _volumeDirty.Add(pair.Key);
            CullUnsupportedPlants(pair.Value,MakeField());
        }
        return amount;
    }
    private void CullUnsupportedPlants(Node node,VolumeTerrain field)
    {
        foreach(Node child in node.GetChildren())
        {
            if(child is MultiMeshInstance3D mm && mm.Multimesh!=null)
            {
                var multi=mm.Multimesh;
                bool tree=mm.Name.ToString().StartsWith("NatureTree");
                if(!tree && !mm.Name.ToString().Contains("Grass"))continue;
                for(int i=0;i<multi.InstanceCount;i++)
                {
                    var t=multi.GetInstanceTransform(i);var p=mm.ToGlobal(t.Origin);
                    if(field.Density(new NVector(p.X,p.Y-.12f,p.Z))<0)
                    {
                        // Do not remove trunks whose base still has support.
                        t.Basis=Basis.Identity.Scaled(Vector3.One*.00001f);multi.SetInstanceTransform(i,t);
                    }
                }
            }
        }
    }
    private void TickVolumeTerrain()
    {
        if(_closing)return;
        if(_player.Position.Y<_volumeBottom+8)
        {
            _volumeBottom-=16;
            foreach(var key in _chunks.Keys)_volumeDirty.Add(key);
        }
        if(_volumeTask!=null && _volumeTask.IsCompleted)
        {
            if(_volumeTask.IsFaulted){GD.PushError(_volumeTask.Exception?.ToString());_volumeDirty.Add(_volumeCoordinate);}
            else if(_chunks.TryGetValue(_volumeCoordinate,out var current) && current==_volumeOwner && !_waterLabChunks.Contains(_volumeCoordinate) && !_volumeDirty.Contains(_volumeCoordinate))
            {
                ApplyVolumeGeometry(_volumeCoordinate,_volumeTask.Result);
                _volumeReady.Add(_volumeCoordinate);
            }
            _volumeTask=null;
        }
        if(_volumeTask!=null || _volumeDirty.Count==0)return;
        var valid=_volumeDirty.Where(k=>_chunks.ContainsKey(k)&&!_waterLabChunks.Contains(k)).ToArray();
        if(valid.Length==0){_volumeDirty.Clear();return;}
        var player2=new Vector2(_player.Position.X,_player.Position.Z);
        _volumeCoordinate=valid.OrderBy(k=>(new Vector2(k.X*ChunkSize,k.Y*ChunkSize)-player2).LengthSquared()).First();
        _volumeOwner=_chunks[_volumeCoordinate];_volumeDirty.Remove(_volumeCoordinate);
        var field=MakeField(true,_volumeCoordinate);
        int ox=_volumeCoordinate.X*ChunkSize,oz=_volumeCoordinate.Y*ChunkSize,size=ChunkSize;
        float top=0;
        for(int x=0;x<=size;x++)for(int z=0;z<=size;z++)top=Mathf.Max(top,field.Height(ox+x,oz+z));
        top=MathF.Ceiling(top)+1;float bottom=_volumeBottom;
        _volumeTask=Task.Run(()=>field.Generate(ox,oz,size,bottom,top));
    }
    private void ApplyVolumeGeometry(Vector2I coordinate,VolumeTerrain.Geometry geometry)
    {
        if(!_terrainMeshes.TryGetValue(coordinate,out var visual))return;
        var vertices=geometry.Vertices.Select(v=>new Vector3(v.X,v.Y,v.Z)).ToArray();
        var normals=geometry.Normals.Select(v=>new Vector3(v.X,v.Y,v.Z)).ToArray();
        var colors=geometry.Weights.Select(v=>new Color(v.X,v.Y,v.Z,v.W)).ToArray();
        var mesh=new ArrayMesh();
        if(vertices.Length>0)
        {
            var arrays=new Godot.Collections.Array();arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex]=vertices;
            arrays[(int)Mesh.ArrayType.Normal]=normals;
            arrays[(int)Mesh.ArrayType.Color]=colors;
            mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles,arrays);
        }
        visual.Mesh=mesh;visual.Position=new Vector3(0,geometry.Origin.Y,0);
        visual.MaterialOverride=_volumeMaterial;
        var collider=_terrainColliders[coordinate];
        collider.Position=visual.Position;
        if(vertices.Length==0)collider.Shape=null;
        else { var shape=new ConcavePolygonShape3D();shape.SetFaces(vertices);collider.Shape=shape; }
        CullUnsupportedPlants(_chunks[coordinate],MakeField());
    }
}
