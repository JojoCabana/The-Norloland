using Godot;

public partial class ResourceNode : Area3D
{
    public string ResourceKind { get; set; } = "";
    public int HitPoints { get; set; } = 1;
    public GrowingBush Bush { get; set; } = null!;
}
