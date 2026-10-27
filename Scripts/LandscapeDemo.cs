using Godot;
using System;
using System.IO;
using System.Linq;

public partial class Main
{
    private int _demoFrame;
    private bool _demoBusy;
    private Camera3D _demoCamera;
    private double _demoWait;
    private async void TickLandscapeDemo(double delta)
    {
        if(!OS.GetCmdlineUserArgs().Contains("--landscape-demo")||_demoBusy)return;
        _demoWait+=delta;
        if((int)(_demoWait*15)%150==0)GD.Print($"LANDSCAPE_LOADING pending={_pendingChunks.Count} dirty={_volumeDirty.Count} task={_volumeTask?.Status} player={_player.Position}");
        if(_demoWait>3600){GD.PushError("Landscape generation timed out");GetTree().Quit(2);return;}
        if(_volumeTask!=null||_volumeDirty.Count>0||_pendingChunks.Count>0||_chunkBuildJob!=null)return;
        if(_demoCamera==null)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetSize(new Vector2I(640,360));
            GetViewport().GetWindow().ContentScaleSize=new Vector2I(640,360);
            _player.SetPhysicsProcess(false);
            _demoCamera=new Camera3D{Current=true,Fov=72,Far=160};AddChild(_demoCamera);
            foreach(Node child in GetChildren())if(child is CanvasLayer layer)layer.Visible=false;_survival.SetWorldInterfaceVisible(false);_survival.SetProcess(false);_hud.Visible=false;_debugHud.Visible=false;
            Directory.CreateDirectory("build/landscape-frames");
            GD.Print("LANDSCAPE_DEMO_READY");
        }
        _demoBusy=true;
        float angle=-.9f+_demoFrame/149f*1.35f;
        var center=new Vector3(0,SampleOriginalSurface(0,0)+2,0);
        _demoCamera.GlobalPosition=center+new Vector3(Mathf.Sin(angle)*19,5+Mathf.Sin(_demoFrame/149f*Mathf.Pi)*1.5f,Mathf.Cos(angle)*19);
        _demoCamera.LookAt(center,Vector3.Up);
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng($"build/landscape-frames/{_demoFrame:D4}.png");
        _demoFrame++;_demoBusy=false;
        if(_demoFrame%15==0)GD.Print($"LANDSCAPE_DEMO_FRAME {_demoFrame}/150");
        if(_demoFrame==150){GD.Print("LANDSCAPE_DEMO_COMPLETE 150 frames / 15 fps / 10 seconds");GetTree().Quit();}
    }
}
