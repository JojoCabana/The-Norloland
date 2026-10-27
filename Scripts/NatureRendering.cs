using Godot;
using System.Collections.Generic;

public partial class Main
{
    private Material _volumeMaterial;
    private readonly List<Mesh>[] _natureTrees={new(),new(),new()};
    private readonly List<Mesh> _natureShort=new();
    private readonly List<Mesh> _natureTall=new();
    private void InitializeNature()
    {
        var terrain=new ShaderMaterial {Shader=GD.Load<Shader>("res://Shaders/nature_terrain.gdshader")};
        foreach(var entry in new[]{("grass","leafy_grass"),("sand","sand_01"),("soil","forest_ground_04")})
        foreach(var map in new[]{("color","Diffuse"),("normal","nor_gl"),("rough","Rough")})
            terrain.SetShaderParameter(entry.Item1+"_"+map.Item1,GD.Load<Texture2D>($"res://Assets/Nature/ground/{entry.Item2}_{map.Item2}.jpg"));
        _volumeMaterial=terrain;
        LoadNatureMeshes("jacaranda_tree/jacaranda_near.gltf",_natureTrees[0],"jacaranda_tree",.025f);
        LoadNatureMeshes("fir_tree_01/fir_realtime.gltf",_natureTrees[1],"fir_tree_01",.025f);
        LoadNatureMeshes("jacaranda_tree/jacaranda_far.gltf",_natureTrees[2],"jacaranda_tree",.025f);
        var shortMeshes=new List<Mesh>();var tallMeshes=new List<Mesh>();
        LoadNatureMeshes("grass_bermuda_01/grass_bermuda_01.gltf",shortMeshes,"grass_bermuda_01",.14f);
        LoadNatureMeshes("grass_medium_01/grass_medium_01.gltf",tallMeshes,"grass_medium_01",.14f);
        foreach(int i in new[]{0,1,2})if(i<shortMeshes.Count)_natureShort.Add(shortMeshes[i]);
        foreach(int i in new[]{0,6,8})if(i<tallMeshes.Count)_natureTall.Add(tallMeshes[i]);
    }
    private void LoadNatureMeshes(string path,List<Mesh> result,string species,float wind)
    {
        var scene=GD.Load<PackedScene>("res://Assets/Nature/"+path);
        if(scene==null)throw new System.InvalidOperationException("Missing nature model: "+path);
        var root=scene.Instantiate();
        void Collect(Node node)
        {
            if(node is MeshInstance3D instance && instance.Mesh!=null)
            {
                var mesh=(Mesh)instance.Mesh.Duplicate();
                for(int i=0;i<mesh.GetSurfaceCount();i++)
                {
                    if(mesh.SurfaceGetMaterial(i) is not StandardMaterial3D source)continue;
                    string name=source.ResourceName.ToLowerInvariant();
                    if(species.StartsWith("grass")||name.Contains("leaves")||name.Contains("twig"))
                    {
                        var material=new ShaderMaterial{Shader=GD.Load<Shader>("res://Shaders/nature_grass.gdshader")};
                        material.SetShaderParameter("albedo_texture",source.AlbedoTexture);
                        material.SetShaderParameter("normal_texture",source.NormalTexture);
                        material.SetShaderParameter("uv_scale",source.Uv1Scale);
                        material.SetShaderParameter("uv_offset",source.Uv1Offset);
                        material.SetShaderParameter("wind_strength",wind);
                        material.SetShaderParameter("use_alpha",true);
                        material.SetShaderParameter("alpha_texture",GD.Load<Texture2D>($"res://Assets/Nature/{species}/textures/foliage_alpha.png"));
                        if(mesh is ArrayMesh array)array.SurfaceSetMaterial(i,material);
                    }
                }
                result.Add(mesh);
            }
            foreach(Node child in node.GetChildren())Collect(child);
        }
        Collect(root);root.Free();
    }
    private static void AddNatureBatch(Node3D chunk,string name,Mesh mesh,List<Transform3D> transforms,float end,bool shadows,float begin=0)
    {
        if(transforms.Count==0)return;
        var multi=new MultiMesh{TransformFormat=MultiMesh.TransformFormatEnum.Transform3D,Mesh=mesh,InstanceCount=transforms.Count};
        for(int i=0;i<transforms.Count;i++)multi.SetInstanceTransform(i,transforms[i]);
        chunk.AddChild(new MultiMeshInstance3D{Name=name,Multimesh=multi,VisibilityRangeBegin=begin,VisibilityRangeEnd=end,
            CastShadow=shadows?GeometryInstance3D.ShadowCastingSetting.On:GeometryInstance3D.ShadowCastingSetting.Off});
    }
    private void AddNatureTrees(Node3D chunk,List<Transform3D>[] transforms)
    {
        int index=0;
        foreach(var mesh in _natureTrees[0])AddNatureBatch(chunk,"NatureTreeNear_"+index++,mesh,transforms[0],24,true);
        foreach(var mesh in _natureTrees[2])AddNatureBatch(chunk,"NatureTreeFar_"+index++,mesh,transforms[0],100,true,24);
        foreach(var mesh in _natureTrees[1])AddNatureBatch(chunk,"NatureTreeFir_"+index++,mesh,transforms[1],100,true);
    }
    private void AddNatureGrass(Node3D chunk,List<Transform3D> shortTransforms,List<Transform3D> tallTransforms)
    {
        void Variants(List<Mesh> meshes,List<Transform3D> source,string name,float distance)
        {
            if(meshes.Count==0)return;
            for(int variant=0;variant<meshes.Count;variant++)
            {
                var batch=new List<Transform3D>();
                for(int i=variant;i<source.Count;i+=meshes.Count)batch.Add(source[i]);
                AddNatureBatch(chunk,name+variant,meshes[variant],batch,distance,false);
            }
        }
        Variants(_natureShort,shortTransforms,"NatureShortGrass_",25);
        Variants(_natureTall,tallTransforms,"NatureTallGrass_",30);
    }
}
