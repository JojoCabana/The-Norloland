using Godot;
using System.IO;
using System.Linq;

// CI-only image capture of the normal world; no separate player mode, basin,
// dam or alternative terrain. Precipitation goes through the production solver.
public partial class Main
{
    private bool _snowCaptureBusy;
    private async void TickSnowCapture()
    {
        if (!OS.GetCmdlineUserArgs().Contains("--snow-capture") || _snowCaptureBusy) return;
        _climate = new WorldClimate(88.5); ApplyClimateLighting();
        if (_volumeTask != null || _volumeDirty.Count > 0 || _pendingChunks.Count > 0 || _chunkBuildJob != null) return;
        _snowCaptureBusy = true;
        _player.SetPhysicsProcess(false);
        float height = SampleOriginalSurface(8, 8);
        var camera = new Camera3D { Current = true, Fov = 72, Far = 160,
            Position = new Vector3(8, height + 5, 23) };
        AddChild(camera); camera.LookAt(new Vector3(8, height + 1, 8));
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _hud.Visible = false; _debugHud.Visible = false; _survival.SetWorldInterfaceVisible(false);
        for (int i = 0; i < 30; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Directory.CreateDirectory("build");
        using var image = GetViewport().GetTexture().GetImage();
        Error result = image.SavePng("build/snow-world.png");
        GD.Print("SNOW_WORLD_CAPTURE " + result);
        GetTree().Quit(result == Error.Ok ? 0 : 1);
    }
}
