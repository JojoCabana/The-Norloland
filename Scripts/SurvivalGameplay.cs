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
    private ColorRect _craftBackdrop = null!;
    private PanelContainer _craftPanel = null!;
    private bool _miningHeld;
    private float _mineProgress;
    private string _currentTool = "Hand";
    private int _smallStone;
    private int _mediumStone;
    private int _largeStone;
    private int _sticks;
    private int _dirt;
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
        if (_miningHeld)
            UpdateMining((float)delta);
    }

    public void ShowCraftingMenu()
    {
        _craftPanel.Visible = true;
        _craftBackdrop.Visible = true;
        _main.SetCraftingOverlayOpen(true);
        RefreshInventoryText();
    }

    public void HideCraftingMenu()
    {
        _craftPanel.Visible = false;
        _craftBackdrop.Visible = false;
        _main.SetCraftingOverlayOpen(false);
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

        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        _craftPanel.AddChild(content);
        var title = new Label { Text = "RUCKSACK & STEINWERKZEUGE", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 22);
        content.AddChild(title);
        _craftInventory = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        content.AddChild(_craftInventory);
        content.AddChild(new Label
        {
            Text = "Axt und Schaufel: je 1 Stock + 1 mittlerer Stein + 1 Schlagstein.\nSpitzhacke: 1 Stock + 1 großer Stein + 1 Schlagstein.\nSteine klopfen und Spalten verbraucht jeweils den Schlagstein.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        });
        content.AddChild(CreateCraftButton("Steinaxt herstellen", () => CraftTool("axe")));
        content.AddChild(CreateCraftButton("Steinschaufel herstellen", () => CraftTool("shovel")));
        content.AddChild(CreateCraftButton("Steinspitzhacke herstellen", () => CraftTool("pickaxe")));
        content.AddChild(CreateCraftButton("Großen Stein in 2 mittlere spalten", SplitLargeStone));
        content.AddChild(CreateCraftButton("Schließen  ·  Esc", HideCraftingMenu));
        AddChild(layer);

        _craftBackdrop.ZIndex = 1;
        center.ZIndex = 2;
        _statusLabel.ZIndex = 0;
        _hintLabel.ZIndex = 0;
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
            _craftInventory.Text = $"Stöcke: {_sticks}   Schlagsteine: {_smallStone}\nMittlere Steine: {_mediumStone}   Große Steine: {_largeStone}\nErde: {_dirt}\nWerkzeuge: {(_hasAxe ? "Axt " : "")}{(_hasShovel ? "Schaufel " : "")}{(_hasPickaxe ? "Spitzhacke" : "Noch keine")}";
        if (_statusLabel != null)
            _statusLabel.Text = $"Werkzeug: {_currentTool}  (1 Hand · 2 Schaufel · 3 Spitzhacke · 4 Axt)\nStöcke {_sticks}  |  Schlagsteine {_smallStone}  |  mittlere Steine {_mediumStone}  |  große Steine {_largeStone}";
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
            _hintLabel.Text = $"Du hast noch keine {tool switch { "Shovel" => "Schaufel", "Pickaxe" => "Spitzhacke", _ => "Axt" }}. Stelle sie mit C her.";
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
        else if (resource.ResourceKind.StartsWith("small_stone", StringComparison.Ordinal) ||
                 resource.ResourceKind.StartsWith("medium_stone", StringComparison.Ordinal) ||
                 resource.ResourceKind.StartsWith("large_stone", StringComparison.Ordinal))
        {
            switch (resource.ResourceKind)
            {
                case "small_stone": _smallStone++; break;
                case "medium_stone": _mediumStone++; break;
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
        if (resource != null && resource.ResourceKind == "stone_deposit")
        {
            if (!_hasPickaxe || _currentTool != "Pickaxe")
            {
                _mineProgress = 0.0f;
                _hintLabel.Text = "Felsgestein: Dafür brauchst du eine Spitzhacke (C).";
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
                    _mediumStone += 2;
                    _smallStone++;
                    MarkResourceConsumed(resource.PersistentId);
                    resource.QueueFree();
                    _hintLabel.Text = "Du gewinnst zwei mittlere Steine und einen Schlagstein.";
                    RefreshInventoryText();
                }
            }
            return;
        }

        var collider = hit["collider"].AsGodotObject() as Node;
        if (collider is StaticBody3D body && body.Name == "TerrainCollision")
        {
            if (_currentTool != "Hand" && _currentTool != "Shovel")
            {
                _mineProgress = 0.0f;
                _hintLabel.Text = "Erde gräbst du mit der Hand oder schneller mit der Schaufel.";
                return;
            }
            float required = _currentTool == "Shovel" ? 0.85f : 3.2f;
            _mineProgress += delta;
            _hintLabel.Text = $"Erde lösen  [{Mathf.Clamp(_mineProgress / required * 100.0f, 0, 100):F0}%]";
            if (_mineProgress >= required)
            {
                _mineProgress -= required;
                Vector3 position = (Vector3)hit["position"];
                float radius = _currentTool == "Shovel" ? 1.45f : 1.0f;
                float depth = _currentTool == "Shovel" ? 0.16f : 0.07f;
                _main.DigGroundAt(position, radius, depth);
                _dirt++;
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
        int requiredStone = isPickaxe ? _largeStone : _mediumStone;
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
            _mediumStone--;
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
        _mediumStone += 2;
        _hintLabel.Text = "Der große Stein wurde in zwei mittlere Steine gespalten.";
        RefreshInventoryText();
    }
}
