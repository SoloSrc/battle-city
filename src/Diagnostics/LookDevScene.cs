using System;
using BattleCity.Characters;
using Godot;

namespace BattleCity.Diagnostics;

/// <summary>
/// Look development scene (#97): the district with two cameras matched to the
/// two approved concept paintings, so every art round is judged in the same
/// framing. `-- --capture &lt;dir&gt;` renders one PNG per concept and quits;
/// `tools/lookdev_sidebyside.py` composites them against the concepts.
/// Interactively, `interact` toggles between the two cameras.
/// </summary>
public partial class LookDevScene : Node3D
{
    /// <summary>Concept aspect: both paintings are 1672 x 941.</summary>
    private static readonly Vector2I _captureSize = new(1672, 941);

    /// <summary>Frames before the first shot, so imports and shadows settle deterministically.</summary>
    private const int SettleFrames = 120;

    /// <summary>Frames between camera switch and capture.</summary>
    private const int SwitchFrames = 10;

    private sealed record Shot(string Camera, string File);

    private static readonly Shot[] _shots =
    {
        new("WorldCamera", "lookdev_world_render.png"),
        new("DuelCamera", "lookdev_duel_render.png"),
    };

    private string? _captureDir;
    private int _frame;
    private int _shot;
    private int _current;

    // Matched by eye against the concepts; adjust here when a round drifts.
    private static readonly Vector3 _worldEye = new(45, 29, 96);
    private static readonly Vector3 _worldTarget = new(32, 0, 78);
    private const float WorldFov = 35f;
    private static readonly Vector3 _duelEye = new(25.0f, 1.7f, 83.5f);
    private static readonly Vector3 _duelTarget = new(21.5f, 1.8f, 73.5f);
    private const float DuelFov = 40f;

    public override void _Ready()
    {
        Aim("WorldCamera", _worldEye, _worldTarget, WorldFov);
        Aim("DuelCamera", _duelEye, _duelTarget, DuelFov);
        foreach (string name in new[] { "Player", "Opponent" })
        {
            var character = GetNodeOrNull<Character>(name);
            character?.PlayState(Character.DuelReadyState);
            character?.Disk?.Deploy();
        }

        string[] args = OS.GetCmdlineUserArgs();
        int capture = Array.IndexOf(args, "--capture");
        if (capture >= 0 && capture + 1 < args.Length)
        {
            _captureDir = args[capture + 1];
            DirAccess.MakeDirRecursiveAbsolute(_captureDir);
            GetWindow().Size = _captureSize;
            GetWindow().Mode = Window.ModeEnum.Windowed;
        }

        SetCamera(0);
    }

    public override void _Process(double delta)
    {
        if (_captureDir is null)
        {
            return;
        }

        _frame++;
        int due = SettleFrames + _shot * SwitchFrames;
        if (_shot >= _shots.Length || _frame != due)
        {
            return;
        }

        string path = $"{_captureDir}/{_shots[_shot].File}";
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"LookDev: captured {path}");
        _shot++;
        if (_shot == _shots.Length)
        {
            GetTree().Quit();
            return;
        }

        SetCamera(_shot);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_captureDir is null && @event.IsActionPressed("interact"))
        {
            SetCamera((_current + 1) % _shots.Length);
        }
    }

    private void Aim(string node, Vector3 eye, Vector3 target, float fov)
    {
        var camera = GetNode<Camera3D>(node);
        camera.Position = eye;
        camera.LookAt(target, Vector3.Up);
        camera.Fov = fov;
    }

    private void SetCamera(int index)
    {
        _current = index;
        GetNode<Camera3D>(_shots[index].Camera).Current = true;
    }
}
