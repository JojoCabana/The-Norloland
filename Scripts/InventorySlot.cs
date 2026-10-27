using Godot;
using System;

public partial class InventorySlot : PanelContainer
{
    public int SlotIndex;
    public bool HasItem;
    public Action<int, int> MoveStack;
    public Action<int> SplitStack;

    public override Variant _GetDragData(Vector2 position)
    {
        if (!HasItem) return default;
        SetDragPreview(new Label { Text = "Stapel verschieben" });
        var data = new Godot.Collections.Dictionary
        {
            ["inventory"] = GetParent().GetInstanceId(),
            ["slot"] = SlotIndex
        };
        return data;
    }

    public override bool _CanDropData(Vector2 position, Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary) return false;
        var payload = data.AsGodotDictionary();
        return payload.ContainsKey("inventory") && payload.ContainsKey("slot") &&
            payload["inventory"].AsUInt64() == GetParent().GetInstanceId();
    }

    public override void _DropData(Vector2 position, Variant data)
    {
        if (_CanDropData(position, data))
            MoveStack?.Invoke(data.AsGodotDictionary()["slot"].AsInt32(), SlotIndex);
    }

    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton button && button.Pressed &&
            button.ButtonIndex == MouseButton.Right && HasItem)
        {
            SplitStack?.Invoke(SlotIndex);
            AcceptEvent();
        }
    }
}
