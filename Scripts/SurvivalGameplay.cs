using Godot;
using System;
using System.Collections.Generic;

public partial class SurvivalGameplay : Node
{
    private static HashSet<string> ConsumedResources => WorldSave.Current.Consumed;
    private Main _main = null!;
    private PlayerController _player = null!;
    private Camera3D _camera = null!;
    private Label _statusLabel = null!;
    private Label _hintLabel = null!;
    private Label _craftInventory = null!;
    private Label _crosshair = null!;
    private Label _targetPrompt = null!;
    private ColorRect _craftBackdrop = null!;
    private PanelContainer _craftPanel = null!;
    private PanelContainer _buildBookPanel = null!;
    private GridContainer _itemGrid = null!;
    private readonly System.Collections.Generic.List<(string Kind, int Count)> _slots = new();
    private const int StackLimit = 99;
    private static readonly string[] ItemKinds = { "wood", "sapling", "small_stone", "medium_stone", "large_stone", "stick", "dirt", "stone", "axe", "shovel", "pickaxe" };
    private static readonly string[] ItemNames = { "Holz", "Setzling", "Kleiner Stein", "Mittlerer Stein", "Großer Stein", "Stock", "Erde", "Steinblock", "Axt", "Schaufel", "Spitzhacke" };
    private bool _miningHeld;
    private float _mineProgress;
    private string _currentTool = "Hand";
    private int _smallStone;
    private int _mediumStone;
    private int _largeStone;
    private int _sticks;
    private int _dirt;
    private int _stoneBlocks;
    private int _wood;
    private int _saplings;
    private bool _hasAxe;
    private bool _hasShovel;
    private bool _hasPickaxe;

    public bool CraftingVisible => _craftPanel != null && _craftPanel.Visible;

    public static bool IsResourceConsumed(string persistentId)
        => !string.IsNullOrEmpty(persistentId) && ConsumedResources.Contains(persistentId);

    private static void MarkResourceConsumed(string persistentId)
    {
        if (!string.IsNullOrEmpty(persistentId))
        {
            ConsumedResources.Add(persistentId);
            WorldSave.Save();
        }
    }

    public static void MarkWorldResourceConsumed(string persistentId)
        => MarkResourceConsumed(persistentId);

    public void Initialize(Main main, PlayerController player)
    {
        _main = main;
        _player = player;
        ProcessMode = ProcessModeEnum.Pausable;
        _camera = player.GetNode<Camera3D>("Camera3D");
        CreateInterface();
        _smallStone = WorldSave.Current.Inventory.GetValueOrDefault("_smallStone");
        _mediumStone = WorldSave.Current.Inventory.GetValueOrDefault("_mediumStone");
        _largeStone = WorldSave.Current.Inventory.GetValueOrDefault("_largeStone");
        _sticks = WorldSave.Current.Inventory.GetValueOrDefault("_sticks");
        _dirt = WorldSave.Current.Inventory.GetValueOrDefault("_dirt");
        _stoneBlocks = WorldSave.Current.Inventory.GetValueOrDefault("_stoneBlocks");
        _wood = WorldSave.Current.Inventory.GetValueOrDefault("_wood");
        _saplings = WorldSave.Current.Inventory.GetValueOrDefault("_saplings");
        _hasAxe = WorldSave.Current.Inventory.GetValueOrDefault("_hasAxe") != 0;
        _hasShovel = WorldSave.Current.Inventory.GetValueOrDefault("_hasShovel") != 0;
        _hasPickaxe = WorldSave.Current.Inventory.GetValueOrDefault("_hasPickaxe") != 0;
        foreach (string saved in WorldSave.Current.Slots ?? new System.Collections.Generic.List<string>())
        {
            string[] parts = saved.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out int count) && count >= 0 && count <= StackLimit)
                _slots.Add((parts[0], count));
        }
        RefreshInventoryText();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left)
        {
            _miningHeld = mouseButton.Pressed && Input.MouseMode == Input.MouseModeEnum.Captured;
            if (!_miningHeld) _mineProgress = 0.0f;
            return;
        }

        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            return;

        if (key.Keycode == Key.C && !CraftingVisible)
        {
            ShowCraftingMenu();
            GetViewport().SetInputAsHandled();
        }
        else if (key.Keycode == Key.Key1)
            SelectTool("Hand");
        else if (key.Keycode == Key.Key2)
            SelectTool("Shovel");
        else if (key.Keycode == Key.Key3)
            SelectTool("Pickaxe");
        else if (key.Keycode == Key.Key4)
            SelectTool("Axe");
        else if (key.Keycode == Key.E)
        {
            Interact();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        UpdateAimPrompt();
        if (_miningHeld)
            UpdateMining((float)delta);
    }

    private void UpdateAimPrompt()
    {
        if (CraftingVisible || Input.MouseMode != Input.MouseModeEnum.Captured || !TryGetLookHit(out Godot.Collections.Dictionary hit))
        {
            _targetPrompt.Text = "";
            return;
        }
        ResourceNode resource = GetResourceNode(hit);
        if (resource != null)
        {
            _targetPrompt.Text = resource.ResourceKind switch
            {
                "bush" => "E · Stöcke sammeln",
                "tree" => _hasAxe ? "Linksklick halten · Baum fällen" : "Baum · Axt nötig",
                "stone_deposit" => _hasPickaxe ? "Linksklick halten · Fels abbauen" : "Fels · Spitzhacke nötig",
                _ => "E · Aufheben"
            };
            return;
        }
        Node collider = hit.ContainsKey("collider") ? hit["collider"].AsGodotObject() as Node : null;
        if (collider is StaticBody3D body && body.Name == "TerrainCollision")
        {
            Vector3 position = (Vector3)hit["position"];
            bool rock = _main.IsRockLayerAt(position.X, position.Z);
            _targetPrompt.Text = rock
                ? (_hasPickaxe ? "Linksklick halten · Stein abbauen" : "Stein · Spitzhacke nötig")
                : "Linksklick halten · Erde abbauen";
            return;
        }
        _targetPrompt.Text = "";
    }

    public void ShowCraftingMenu()
    {
        _craftPanel.Visible = true;
        _craftBackdrop.Visible = true;
        _crosshair.Visible = false;
        _targetPrompt.Visible = false;
        _main.SetCraftingOverlayOpen(true);
        RefreshInventoryText();
    }

    public void HideCraftingMenu()
    {
        _craftPanel.Visible = false;
        _craftBackdrop.Visible = false;
        _crosshair.Visible = true;
        _targetPrompt.Visible = true;
        _main.SetCraftingOverlayOpen(false);
    }

    public void ToggleCraftingMenu()
    {
        if (CraftingVisible)
            HideCraftingMenu();
        else
            ShowCraftingMenu();
    }

    private void ShowBuildBook()
    {
        _buildBookPanel.Visible = true;
        _craftPanel.Visible = false;
    }

    private void HideBuildBook()
    {
        _buildBookPanel.Visible = false;
        _craftPanel.Visible = true;
    }

    private void CreateInterface()
    {
        var layer = new CanvasLayer { Layer = 120, ProcessMode = ProcessModeEnum.Always };
        _statusLabel = new Label
        {
            Position = new Vector2(16, 48),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _statusLabel.AddThemeColorOverride("font_color", Colors.White);
        _statusLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        _statusLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        _statusLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        layer.AddChild(_statusLabel);

        _hintLabel = new Label
        {
            Position = new Vector2(16, 116),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _hintLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.90f, 0.70f));
        _hintLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        layer.AddChild(_hintLabel);

        var centerHud = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        centerHud.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(centerHud);
        var centerStack = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        centerStack.AddThemeConstantOverride("separation", 3);
        centerHud.AddChild(centerStack);
        _crosshair = new Label
        {
            Text = "+",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _crosshair.AddThemeFontSizeOverride("font_size", 24);
        _crosshair.AddThemeColorOverride("font_color", Colors.White);
        _crosshair.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.95f));
        centerStack.AddChild(_crosshair);
        _targetPrompt = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _targetPrompt.AddThemeColorOverride("font_color", new Color(1.0f, 0.95f, 0.78f));
        _targetPrompt.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.95f));
        centerStack.AddChild(_targetPrompt);

        _craftBackdrop = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.72f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false
        };
        _craftBackdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(_craftBackdrop);
        _craftPanel = new PanelContainer
        {
            Visible = false,
            CustomMinimumSize = new Vector2(500, 0)
        };
        _craftPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        var panelStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.055f, 0.075f, 0.065f, 0.98f),
            BorderColor = new Color(0.52f, 0.62f, 0.46f),
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10
        };
        panelStyle.ContentMarginLeft = 20;
        panelStyle.ContentMarginRight = 20;
        panelStyle.ContentMarginTop = 18;
        panelStyle.ContentMarginBottom = 18;
        _craftPanel.AddThemeStyleboxOverride("panel", panelStyle);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(center);
        center.AddChild(_craftPanel);

        _buildBookPanel = new PanelContainer
        {
            Visible = false,
            CustomMinimumSize = new Vector2(500, 0)
        };
        _buildBookPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        _buildBookPanel.AddThemeStyleboxOverride("panel", panelStyle);
        center.AddChild(_buildBookPanel);

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        _craftPanel.AddChild(content);
        var title = new Label { Text = "INVENTAR & HERSTELLEN  ·  I", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 22);
        content.AddChild(title);
        _craftInventory = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        content.AddChild(_craftInventory);
        var inventoryScroll = new ScrollContainer { CustomMinimumSize = new Vector2(600, 280) };
        _itemGrid = new GridContainer { Columns = 6 };
        _itemGrid.AddThemeConstantOverride("h_separation", 6);
        _itemGrid.AddThemeConstantOverride("v_separation", 6);
        inventoryScroll.AddChild(_itemGrid);
        content.AddChild(inventoryScroll);
        content.AddChild(new Label { Text = "Ziehen: verschieben / tauschen / stapeln · Rechtsklick: Stapel halbieren" });
        content.AddChild(new Label
        {
            Text = "Abgebaute Erde und Steinblöcke landen hier im Inventar.\nAxt und Schaufel: je 1 Stock + 1 großer Stein + 1 kleiner Schlagstein.\nSpitzhacke: 1 Stock + 1 großer Stein + 1 kleiner Schlagstein.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
        content.AddChild(CreateCraftButton("Steinaxt herstellen", () => CraftTool("axe")));
        content.AddChild(CreateCraftButton("Steinschaufel herstellen", () => CraftTool("shovel")));
        content.AddChild(CreateCraftButton("Steinspitzhacke herstellen", () => CraftTool("pickaxe")));
        content.AddChild(CreateCraftButton("Großen Stein in kleine Steine spalten", SplitLargeStone));
        content.AddChild(CreateCraftButton("Baubuch öffnen", ShowBuildBook));
        content.AddChild(CreateCraftButton("Schließen  ·  I / Esc", HideCraftingMenu));

        var book = new VBoxContainer();
        book.AddThemeConstantOverride("separation", 10);
        _buildBookPanel.AddChild(book);
        var bookTitle = new Label { Text = "BAUBUCH", HorizontalAlignment = HorizontalAlignment.Center };
        bookTitle.AddThemeFontSizeOverride("font_size", 24);
        book.AddChild(bookTitle);
        book.AddChild(new Label { Text = "Holz und Steine werden als Baumaterial gesammelt.\n\nFreigeschaltete Baupläne:\n• Einfaches Holzlager  ·  4 Holz\n• Feuerstelle  ·  3 Holz + 2 Steine\n• Kleiner Unterstand  ·  8 Holz + 2 Setzlinge\n\nDie Bauplätze werden im nächsten Schritt an der Mitte des Fadenkreuzes platziert.", AutowrapMode = TextServer.AutowrapMode.WordSmart });
        book.AddChild(CreateCraftButton("Zurück zum Inventar", HideBuildBook));
        AddChild(layer);

        _craftBackdrop.ZIndex = 1;
        center.ZIndex = 2;
        _buildBookPanel.ZIndex = 2;
        _statusLabel.ZIndex = 0;
        _hintLabel.ZIndex = 0;
        centerHud.ZIndex = 0;
    }

    private static Button CreateCraftButton(string text, Action callback)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0, 38),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        button.Pressed += callback;
        return button;
    }

    private void RefreshInventoryText()
    {
        WorldSave.Current.Inventory["_smallStone"] = _smallStone;
        WorldSave.Current.Inventory["_mediumStone"] = _mediumStone;
        WorldSave.Current.Inventory["_largeStone"] = _largeStone;
        WorldSave.Current.Inventory["_sticks"] = _sticks;
        WorldSave.Current.Inventory["_dirt"] = _dirt;
        WorldSave.Current.Inventory["_stoneBlocks"] = _stoneBlocks;
        WorldSave.Current.Inventory["_wood"] = _wood;
        WorldSave.Current.Inventory["_saplings"] = _saplings;
        WorldSave.Current.Inventory["_hasAxe"] = _hasAxe ? 1 : 0;
        WorldSave.Current.Inventory["_hasShovel"] = _hasShovel ? 1 : 0;
        WorldSave.Current.Inventory["_hasPickaxe"] = _hasPickaxe ? 1 : 0;
        if (_craftInventory != null)
            _craftInventory.Text = $"Holz: {_wood}   Setzlinge: {_saplings}\nStöcke: {_sticks}   Kleine Steine: {_smallStone}   Große Steine: {_largeStone}\nErde: {_dirt}   Steinblöcke: {_stoneBlocks}\nWerkzeuge: {(_hasAxe ? "Axt " : "")}{(_hasShovel ? "Schaufel " : "")}{(_hasPickaxe ? "Spitzhacke" : "Noch keine")}";
        RefreshSlots();
        if (_statusLabel != null)
            _statusLabel.Text = $"Werkzeug: {_currentTool}  (1 Hand · 2 Schaufel · 3 Spitzhacke · 4 Axt)\nStöcke {_sticks}  |  kleine Steine {_smallStone}  |  große Steine {_largeStone}";
    }

    private void RefreshSlots()
    {
        int[] amounts = { _wood, _saplings, _smallStone, _mediumStone, _largeStone, _sticks, _dirt, _stoneBlocks, _hasAxe ? 1 : 0, _hasShovel ? 1 : 0, _hasPickaxe ? 1 : 0 };
        while (_slots.Count < 24) _slots.Add(("", 0));
        for (int kindIndex = 0; kindIndex < ItemKinds.Length; kindIndex++)
        {
            string kind = ItemKinds[kindIndex];
            int remaining = amounts[kindIndex];
            for (int slot = 0; slot < _slots.Count; slot++)
            {
                if (_slots[slot].Kind != kind) continue;
                int keep = Math.Min(_slots[slot].Count, remaining);
                _slots[slot] = keep == 0 ? ("", 0) : (kind, keep);
                remaining -= keep;
            }
            for (int slot = 0; slot < _slots.Count && remaining > 0; slot++)
            {
                if (_slots[slot].Kind != kind || _slots[slot].Count >= StackLimit) continue;
                int add = Math.Min(StackLimit - _slots[slot].Count, remaining);
                _slots[slot] = (kind, _slots[slot].Count + add);
                remaining -= add;
            }
            while (remaining > 0)
            {
                int empty = _slots.FindIndex(item => item.Count == 0);
                if (empty < 0) { empty = _slots.Count; _slots.Add(("", 0)); }
                int add = Math.Min(StackLimit, remaining);
                _slots[empty] = (kind, add);
                remaining -= add;
            }
        }
        DrawSlots();
    }

    private void SaveSlots()
    {
        WorldSave.Current.Slots.Clear();
        foreach (var item in _slots) WorldSave.Current.Slots.Add(item.Kind + ":" + item.Count);
        _main.SaveWorldChanges();
    }

    private void DrawSlots()
    {
        SaveSlots();
        if (_itemGrid == null) return;
        foreach (Node child in _itemGrid.GetChildren())
        {
            _itemGrid.RemoveChild(child);
            child.QueueFree();
        }
        for (int i = 0; i < _slots.Count; i++)
        {
            var item = _slots[i];
            int type = Array.IndexOf(ItemKinds, item.Kind);
            var slot = new InventorySlot
            {
                SlotIndex = i, HasItem = item.Count > 0,
                MoveStack = MoveStack, SplitStack = SplitStack,
                CustomMinimumSize = new Vector2(94, 82),
                TooltipText = type < 0 ? "Leer" : ItemNames[type] + " × " + item.Count
            };
            var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            if (type >= 0)
            {
                string icon = item.Kind == "stone" || item.Kind == "medium_stone" ? "large_stone" : item.Kind;
                box.AddChild(new TextureRect
                {
                    Texture = GD.Load<Texture2D>($"res://Assets/Textures/Items/{icon}.png"),
                    CustomMinimumSize = new Vector2(48, 44),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                });
            }
            box.AddChild(new Label { Text = type < 0 ? "·" : ItemNames[type] + "\n× " + item.Count,
                HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore });
            slot.AddChild(box);
            _itemGrid.AddChild(slot);
        }
    }

    private void MoveStack(int source, int target)
    {
        if (source == target || source < 0 || target < 0 || source >= _slots.Count || target >= _slots.Count) return;
        var from = _slots[source];
        var to = _slots[target];
        if (from.Kind == to.Kind && to.Count < StackLimit)
        {
            int move = Math.Min(from.Count, StackLimit - to.Count);
            _slots[target] = (to.Kind, to.Count + move);
            _slots[source] = from.Count == move ? ("", 0) : (from.Kind, from.Count - move);
        }
        else { _slots[source] = to; _slots[target] = from; }
        SaveSlots();
        // Defer rebuilding controls until Godot has finished the drag callback.
        Callable.From(DrawSlots).CallDeferred();
    }

    private void SplitStack(int source)
    {
        if (_slots[source].Count < 2) return;
        int empty = _slots.FindIndex(item => item.Count == 0);
        if (empty < 0) { empty = _slots.Count; _slots.Add(("", 0)); }
        var item = _slots[source];
        int half = item.Count / 2;
        _slots[source] = (item.Kind, item.Count - half);
        _slots[empty] = (item.Kind, half);
        SaveSlots();
        Callable.From(DrawSlots).CallDeferred();
    }

    private void SelectTool(string tool)
    {
        bool available = tool switch
        {
            "Hand" => true,
            "Shovel" => _hasShovel,
            "Pickaxe" => _hasPickaxe,
            "Axe" => _hasAxe,
            _ => false
        };
        if (!available)
        {
            _hintLabel.Text = $"Du hast noch keine {tool switch { "Shovel" => "Schaufel", "Pickaxe" => "Spitzhacke", _ => "Axt" }}. Stelle sie mit I her.";
            return;
        }
        _currentTool = tool;
        _hintLabel.Text = "";
        RefreshInventoryText();
    }

    private void Interact()
    {
        if (CraftingVisible || Input.MouseMode != Input.MouseModeEnum.Captured)
            return;
        if (!TryGetLookHit(out Godot.Collections.Dictionary hit))
            return;
        ResourceNode resource = GetResourceNode(hit);
        if (resource == null)
            return;
        if (resource.ResourceKind == "bush" && resource.Bush != null)
        {
            int amount = resource.Bush.Harvest();
            _sticks += amount;
            _hintLabel.Text = $"Du sammelst {amount} Stock/Äste. Die Triebe wachsen langsam nach.";
            RefreshInventoryText();
        }
        else if (resource.ResourceKind == "tree")
        {
            if (!_hasAxe || _currentTool != "Axe")
            {
                _hintLabel.Text = "Zum Fällen brauchst du eine Axt und musst sie mit 4 auswählen.";
                return;
            }
            int wood = 2 + resource.HitPoints;
            _wood += wood;
            _saplings++;
            MarkResourceConsumed(resource.PersistentId);
            _main.RemoveTreeAt(resource.GlobalPosition, resource.PersistentId);
            resource.QueueFree();
            _hintLabel.Text = $"Baum gefällt: +{wood} Holz, +1 Setzling.";
            RefreshInventoryText();
        }
        else if (resource.ResourceKind.StartsWith("small_stone", StringComparison.Ordinal) ||
                 resource.ResourceKind.StartsWith("medium_stone", StringComparison.Ordinal) ||
                 resource.ResourceKind.StartsWith("large_stone", StringComparison.Ordinal))
        {
            switch (resource.ResourceKind)
            {
                case "small_stone": _smallStone++; break;
                case "medium_stone": _smallStone++; break;
                case "large_stone": _largeStone++; break;
            }
            MarkResourceConsumed(resource.PersistentId);
            resource.QueueFree();
            _hintLabel.Text = "Stein aufgehoben.";
            RefreshInventoryText();
        }
    }

    private void UpdateMining(float delta)
    {
        if (!TryGetLookHit(out Godot.Collections.Dictionary hit))
        {
            _mineProgress = 0.0f;
            _hintLabel.Text = "";
            return;
        }

        ResourceNode resource = GetResourceNode(hit);
        if (resource != null && resource.ResourceKind == "tree" && (!_hasAxe || _currentTool != "Axe"))
        {
            _hintLabel.Text = "Zum Fällen brauchst du eine Axt und musst sie mit 4 auswählen.";
            return;
        }
        if (resource != null && resource.ResourceKind == "tree")
        {
            _mineProgress += delta;
            const float required = 1.0f;
            _hintLabel.Text = $"Baum fällen  [{Mathf.Clamp(_mineProgress / required * 100.0f, 0, 100):F0}%]";
            if (_mineProgress >= required)
            {
                _mineProgress = 0.0f;
                resource.HitPoints--;
                if (resource.HitPoints <= 0)
                {
                    _wood += 6;
                    _saplings++;
                    MarkResourceConsumed(resource.PersistentId);
                    resource.QueueFree();
                    _hintLabel.Text = "+6 Holz, +1 Setzling im Inventar.";
                    RefreshInventoryText();
                }
            }
            return;
        }
        if (resource != null && resource.ResourceKind == "stone_deposit")
        {
            if (!_hasPickaxe || _currentTool != "Pickaxe")
            {
                _mineProgress = 0.0f;
                _hintLabel.Text = "Felsgestein: Dafür brauchst du eine Spitzhacke (I).";
                return;
            }
            _mineProgress += delta;
            float required = 1.35f;
            _hintLabel.Text = $"Stein abbauen  [{Mathf.Clamp(_mineProgress / required * 100.0f, 0, 100):F0}%]";
            if (_mineProgress >= required)
            {
                _mineProgress -= required;
                resource.HitPoints--;
                if (resource.HitPoints <= 0)
                {
                    _largeStone++;
                    _smallStone++;
                    MarkResourceConsumed(resource.PersistentId);
                    resource.QueueFree();
                    _hintLabel.Text = "Du gewinnst einen großen und einen kleinen Stein.";
                    RefreshInventoryText();
                }
            }
            return;
        }

        var collider = hit["collider"].AsGodotObject() as Node;
        if (collider is StaticBody3D body && body.Name == "TerrainCollision")
        {
            Vector3 position = (Vector3)hit["position"];
            bool rockLayer = _main.IsRockLayerAt(position.X, position.Z);
            if (rockLayer && (!_hasPickaxe || _currentTool != "Pickaxe"))
            {
                _mineProgress = 0.0f;
                _hintLabel.Text = "Stein abbauen: Dafür brauchst du eine Spitzhacke (I).";
                return;
            }
            if (!rockLayer && _currentTool != "Hand" && _currentTool != "Shovel")
            {
                _mineProgress = 0.0f;
                _hintLabel.Text = "Erde gräbst du mit der Hand oder schneller mit der Schaufel.";
                return;
            }
            float required = rockLayer ? 2.4f : _currentTool == "Shovel" ? 0.85f : 3.2f;
            _mineProgress += delta;
            _hintLabel.Text = (rockLayer ? "Stein lösen" : "Erde lösen") + $"  [{Mathf.Clamp(_mineProgress / required * 100.0f, 0, 100):F0}%]";
            if (_mineProgress >= required)
            {
                _mineProgress -= required;
                if (rockLayer)
                {
                    _main.DigGroundAt(position, 0.9f, 0.08f);
                    _stoneBlocks++;
                    _hintLabel.Text = "+1 Steinblock im Inventar.";
                }
                else
                {
                    float radius = _currentTool == "Shovel" ? 1.45f : 1.0f;
                    float depth = _currentTool == "Shovel" ? 0.16f : 0.07f;
                    _main.DigGroundAt(position, radius, depth);
                    _dirt++;
                    _hintLabel.Text = "+1 Erde im Inventar.";
                }
                RefreshInventoryText();
            }
            return;
        }

        _mineProgress = 0.0f;
        _hintLabel.Text = "";
    }

    private bool TryGetLookHit(out Godot.Collections.Dictionary hit)
    {
        Vector2 center = GetViewport().GetVisibleRect().Size * 0.5f;
        Vector3 origin = _camera.ProjectRayOrigin(center);
        Vector3 end = origin + _camera.ProjectRayNormal(center) * 4.0f;
        var query = PhysicsRayQueryParameters3D.Create(origin, end);
        query.CollideWithAreas = true;
        query.CollideWithBodies = true;
        query.CollisionMask = 1;
        hit = _player.GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count > 0;
    }

    private static ResourceNode GetResourceNode(Godot.Collections.Dictionary hit)
    {
        if (!hit.ContainsKey("collider")) return null;
        var collider = hit["collider"].AsGodotObject() as ResourceNode;
        return collider;
    }

    private void CraftTool(string kind)
    {
        bool isPickaxe = kind == "pickaxe";
        int requiredStone = _largeStone;
        if (_sticks < 1 || requiredStone < 1 || _smallStone < 1)
        {
            _hintLabel.Text = "Fehlt: 1 Stock, 1 passender Stein und 1 Schlagstein. Der Schlagstein wird verbraucht.";
            return;
        }
        _sticks--;
        _smallStone--;
        if (isPickaxe)
        {
            _largeStone--;
            _hasPickaxe = true;
        }
        else
        {
            _largeStone--;
            if (kind == "axe") _hasAxe = true;
            else _hasShovel = true;
        }
        _hintLabel.Text = kind switch
        {
            "axe" => "Steinaxt geklopft.",
            "shovel" => "Steinschaufel geklopft.",
            _ => "Steinspitzhacke geklopft."
        };
        RefreshInventoryText();
    }

    private void SplitLargeStone()
    {
        if (_largeStone < 1 || _smallStone < 1)
        {
            _hintLabel.Text = "Zum Spalten brauchst du einen großen Stein und einen Schlagstein, der dabei verbraucht wird.";
            return;
        }
        _largeStone--;
        _smallStone--;
        _smallStone += 2;
        _hintLabel.Text = "Der große Stein wurde in kleine Steine gespalten.";
        RefreshInventoryText();
    }
}
