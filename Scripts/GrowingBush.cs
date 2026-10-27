using Godot;
using System;
using System.Collections.Generic;

public partial class GrowingBush : Node3D
{
    private const float MatureHeight = 1.6f;
    private const float GrowthSeconds = 1800.0f;
    private static Dictionary<string, float> SavedGrowth => WorldSave.Current.Growth;
    // Shared geometry is never mutated after construction.
    private static Mesh _branchMesh;
    private static Mesh _leafMesh;
    private float _growth;
    private float _growthSaveTimer;
    private Node3D _visual = null!;
    private Color _biomeTint = Colors.White;
    public string PersistentId => _persistentId;
    public int StickYield => 1 + Mathf.RoundToInt(_growth * 3.0f);
    private static Mesh _canopyMesh;
    private int _seed;
    private string _persistentId = "";
    private float _initialGrowth;

    public void Configure(int seed, float biome, string persistentId, float initialGrowth = 0.0f)
    {
        _seed = seed;
        _persistentId = persistentId;
        _initialGrowth = Mathf.Clamp(initialGrowth, 0.0f, 1.0f);
        _biomeTint = biome < -0.25f ? new Color(0.92f, 0.85f, 0.68f) :
            biome > 0.4f ? new Color(0.78f, 0.92f, 0.73f) : new Color(0.88f, 0.96f, 0.79f);
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Pausable;
        _growth = SavedGrowth.TryGetValue(_persistentId, out float savedGrowth) ? savedGrowth : _initialGrowth;
        BuildVisual();
        UpdateGrowthScale();
        SetProcess(_growth < 1.0f);
    }

    public override void _Process(double delta)
    {
        if (_growth >= 1.0f)
            return;
        _growth = Mathf.Min(1.0f, _growth + (float)delta / GrowthSeconds);
        _growthSaveTimer += (float)delta;
        if (_growthSaveTimer >= 1.0f)
        {
            SavedGrowth[_persistentId] = _growth;
            _growthSaveTimer = 0.0f;
        }
        UpdateGrowthScale();
        if (_growth >= 1.0f)
        {
            SavedGrowth[_persistentId] = _growth;
            SetProcess(false);
        }
    }

    public override void _ExitTree()
    {
        SavedGrowth[_persistentId] = _growth;
    }

    public int Harvest()
    {
        int sticks = 1 + Mathf.RoundToInt(_growth * 3.0f);
        _growth = 0.0f;
        SetProcess(true);
        SavedGrowth[_persistentId] = _growth;
        WorldSave.Save();
        UpdateGrowthScale();
        return sticks;
    }

    private void UpdateGrowthScale()
    {
        if (_visual == null)
            return;
        float height = Mathf.Lerp(0.8f, MatureHeight, _growth);
        _visual.Scale = new Vector3(height / MatureHeight, height / MatureHeight, height / MatureHeight);
    }

    private void BuildVisual()
    {
        _visual = new Node3D { Name = "BushGrowthVisual" };
        AddChild(_visual);

        var random = new Random(_seed);
        var branchTransforms = new List<Transform3D>();
        var leafTransforms = new List<Transform3D>();
        int branchCount = 5 + random.Next(0, 3);
        for (int branch = 0; branch < branchCount; branch++)
        {
            float angle = Mathf.Tau * branch / branchCount + ((float)random.NextDouble() - 0.5f) * 0.55f;
            float reach = 0.34f + (float)random.NextDouble() * 0.30f;
            float length = 0.55f + (float)random.NextDouble() * 0.42f;
            float tilt = 0.48f + (float)random.NextDouble() * 0.35f;
            Vector3 center = new(Mathf.Cos(angle) * reach * 0.38f, length * 0.5f, Mathf.Sin(angle) * reach * 0.38f);
            Basis basis = Basis.FromEuler(new Vector3(Mathf.Sin(angle) * tilt, angle, -Mathf.Cos(angle) * tilt));
            branchTransforms.Add(new Transform3D(basis.Scaled(new Vector3(0.035f, length, 0.035f)), center));

            int leafCount = 4 + random.Next(0, 3);
            for (int leaf = 0; leaf < leafCount; leaf++)
            {
                float along = 0.28f + leaf * (0.66f / Math.Max(1, leafCount - 1));
                Vector3 leafPosition = new(
                    Mathf.Cos(angle) * reach * along,
                    length * along,
                    Mathf.Sin(angle) * reach * along);
                float leafWidth = 0.075f + (float)random.NextDouble() * 0.045f;
                float leafHeight = 0.11f + (float)random.NextDouble() * 0.07f;
                Vector3 rotation = new(
                    Mathf.Sin(angle) * (0.35f + (float)random.NextDouble() * 0.45f),
                    angle + ((float)random.NextDouble() - 0.5f) * 0.9f,
                    -Mathf.Cos(angle) * (0.25f + (float)random.NextDouble() * 0.5f));
                Basis leafBasis = Basis.FromEuler(rotation).Scaled(new Vector3(leafWidth, leafHeight, 0.025f));
                leafTransforms.Add(new Transform3D(leafBasis, leafPosition));
            }
        }

        var barkMaterial = new StandardMaterial3D
        {
            AlbedoTexture = GD.Load<Texture2D>("res://Assets/Textures/tree_bark.png"),
            AlbedoColor = new Color(0.86f, 0.79f, 0.66f),
            Roughness = 1.0f,
            TextureRepeat = true,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps
        };
        var branchMesh = _branchMesh ??= new CylinderMesh { TopRadius = 0.014f, BottomRadius = 0.035f, Height = 1.0f, RadialSegments = 5 };
        AddMultiMesh("BushTwigs", branchMesh, barkMaterial, branchTransforms);

        var leafMaterial = new StandardMaterial3D
        {
            AlbedoTexture = GD.Load<Texture2D>("res://Assets/Textures/forest_leaves.png"),
            AlbedoColor = _biomeTint,
            Roughness = 0.92f,
            TextureRepeat = true,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        AddMultiMesh("BushLeaves", _leafMesh ??= CreateLeafMesh(), leafMaterial, leafTransforms);
        // Broad green clusters make a living bush distinct from bare loose branches.
        _canopyMesh ??= new SphereMesh { Radius = 1, Height = 2, RadialSegments = 8, Rings = 4 };
        var canopy = new List<Transform3D>();
        for (int i = 0; i < 5; i++)
        {
            float angle = i * Mathf.Tau / 5;
            canopy.Add(new Transform3D(Basis.FromScale(new Vector3(0.32f, 0.38f, 0.32f)),
                new Vector3(Mathf.Cos(angle) * 0.28f, 0.65f + i % 2 * 0.16f, Mathf.Sin(angle) * 0.28f)));
        }
        AddMultiMesh("BushCanopy", _canopyMesh, leafMaterial, canopy);

        var pickArea = new ResourceNode
        {
            Name = "BushHarvestArea",
            ResourceKind = "bush",
            PersistentId = _persistentId,
            Bush = this,
            CollisionLayer = 1,
            CollisionMask = 0
        };
        pickArea.Position = new Vector3(0, 0.72f, 0);
        pickArea.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.78f } });
        _visual.AddChild(pickArea);
    }

    private void AddMultiMesh(string name, Mesh mesh, Material material, List<Transform3D> transforms)
    {
        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = transforms.Count
        };
        for (int i = 0; i < transforms.Count; i++)
            multimesh.SetInstanceTransform(i, transforms[i]);
        _visual.AddChild(new MultiMeshInstance3D { Name = name, Multimesh = multimesh, MaterialOverride = material });
    }

    private static ArrayMesh CreateLeafMesh()
    {
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        AddLeafVertex(surface, new Vector3(0, 0, 0), new Vector2(0.5f, 0));
        AddLeafVertex(surface, new Vector3(-0.42f, 0.42f, 0), new Vector2(0, 0.42f));
        AddLeafVertex(surface, new Vector3(0, 1, 0), new Vector2(0.5f, 1));
        AddLeafVertex(surface, new Vector3(0, 0, 0), new Vector2(0.5f, 0));
        AddLeafVertex(surface, new Vector3(0, 1, 0), new Vector2(0.5f, 1));
        AddLeafVertex(surface, new Vector3(0.42f, 0.42f, 0), new Vector2(1, 0.42f));
        AddLeafVertex(surface, new Vector3(0, 0, 0), new Vector2(0.5f, 0));
        AddLeafVertex(surface, new Vector3(0.42f, 0.42f, 0), new Vector2(1, 0.42f));
        AddLeafVertex(surface, new Vector3(-0.42f, 0.42f, 0), new Vector2(0, 0.42f));
        surface.GenerateNormals();
        return surface.Commit() as ArrayMesh;
    }

    private static void AddLeafVertex(SurfaceTool surface, Vector3 position, Vector2 uv)
    {
        surface.SetUV(uv);
        surface.AddVertex(position);
    }
}
