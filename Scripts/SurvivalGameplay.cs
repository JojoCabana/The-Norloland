using Godot;
using System;
using System.Collections.Generic;

public partial class SurvivalGameplay : Node
{
    private static readonly HashSet<string> ConsumedResources = new();
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
            ConsumedResources.Add(persistentId);
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
        _itemGrid = new GridContainer { Columns = 2 };
        content.AddChild(_itemGrid);
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
        if (_craftInventory != null)
            _craftInventory.Text = $"Holz: {_wood}   Setzlinge: {_saplings}\nStöcke: {_sticks}   Kleine Steine: {_smallStone}   Große Steine: {_largeStone}\nErde: {_dirt}   Steinblöcke: {_stoneBlocks}\nWerkzeuge: {(_hasAxe ? "Axt " : "")}{(_hasShovel ? "Schaufel " : "")}{(_hasPickaxe ? "Spitzhacke" : "Noch keine")}";
        if (_itemGrid != null)
        {
            foreach (Node child in _itemGrid.GetChildren()) child.QueueFree();
            _itemGrid.AddChild(CreateItemSlot("Holz", _wood, new Color(0.40f, 0.22f, 0.10f)));
            _itemGrid.AddChild(CreateItemSlot("Setzlinge", _saplings, new Color(0.24f, 0.52f, 0.18f)));
            _itemGrid.AddChild(CreateItemSlot("Steine", _smallStone + _largeStone, new Color(0.45f, 0.47f, 0.45f)));
            _itemGrid.AddChild(CreateItemSlot("Erde", _dirt, new Color(0.30f, 0.18f, 0.10f)));
        }
        if (_statusLabel != null)
            _statusLabel.Text = $"Werkzeug: {_currentTool}  (1 Hand · 2 Schaufel · 3 Spitzhacke · 4 Axt)\nStöcke {_sticks}  |  kleine Steine {_smallStone}  |  große Steine {_largeStone}";
    }

    private static Control CreateItemSlot(string name, int count, Color color)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(220, 34) };
        string iconName = name switch
        {
            "Holz" => "wood",
            "Setzlinge" => "sapling",
            "Steine" => "large_stone",
            "Erde" => "dirt",
            _ => "small_stone"
        };
        var icon = new TextureRect
        {
            Texture = GD.Load<Texture2D>($"res://Assets/Textures/Items/{iconName}.png"),
            CustomMinimumSize = new Vector2(36, 36),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        row.AddChild(icon);
        row.AddChild(new Label { Text = $"  {name}  × {count}", VerticalAlignment = VerticalAlignment.Center });
        return row;
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
