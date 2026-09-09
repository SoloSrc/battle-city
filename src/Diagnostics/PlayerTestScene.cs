using System;
using System.Collections.Generic;
using BattleCity.Characters;
using BattleCity.Core;
using Godot;

namespace BattleCity.Diagnostics;

/// <summary>
/// Drives Player.tscn with synthetic input over a 0.15 m kerb, a 0.3 m step
/// and a 30° ramp, then interacts with a test interactable. Headless CI runs
/// it with <c>--fixed-fps 60</c>; in the editor with a gamepad or keyboard the
/// script stays out of the way and the scene is a free-roam test course.
/// </summary>
public partial class PlayerTestScene : Node3D
{
    private const string FailPrefix = "PlayerTest FAIL";
    private const int WalkEnd = 150;
    private const int RunEnd = 250;
    private const int InteractFrame = 300;
    private const int CheckFrame = 330;

    [Export]
    public Character? Player { get; set; }

    [Export]
    public PlayerController? Controller { get; set; }

    [Export]
    public TestInteractable? Interactable { get; set; }

    [Export]
    public Camera3D? Camera { get; set; }

    [Export]
    public Label? ReportLabel { get; set; }

    /// <summary>Feed scripted input; on by default when headless or when <c>-- --scripted</c> is passed.</summary>
    [Export]
    public bool Scripted { get; set; }

    private readonly List<string> _report = new();
    private int _frame;
    private int _pass;
    private int _fail;
    private bool _done;
    private Vector3 _start;
    private Vector3 _cameraOffset;
    private float _maxSpeed;
    private float _lastY;
    private float _maxDrop;
    private int _airFrames;
    private string _prompt = string.Empty;

    public override void _Ready()
    {
        Scripted = Scripted || DisplayServer.GetName() == "headless"
            || Array.IndexOf(OS.GetCmdlineUserArgs(), "--scripted") >= 0;
        if (Player is null || Controller is null)
        {
            Fail("Player or Controller not wired");
            _done = true;
            return;
        }

        _start = Player.GlobalPosition;
        _lastY = _start.Y;
        if (Camera is not null)
        {
            _cameraOffset = Camera.GlobalPosition - Player.GlobalPosition;
        }

        Controller.PromptChanged += prompt => _prompt = prompt;
        Report("INFO", Scripted ? "scripted input (headless)" : "live input: left stick / WASD, run above 60 % or Shift, E / A to interact, Tab / Start toggles nothing here");
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_done || Player is null || Controller is null)
        {
            return;
        }

        if (Camera is not null)
        {
            Camera.GlobalPosition = Player.GlobalPosition + _cameraOffset;
        }

        if (!Scripted)
        {
            return;
        }

        _frame++;
        if (_frame < WalkEnd)
        {
            Input.ActionPress(InputActions.MoveUp, 0.5f);
            Input.ActionRelease(InputActions.RunToggle);
        }
        else if (_frame < RunEnd)
        {
            Input.ActionPress(InputActions.MoveUp, 1.0f);
        }
        else
        {
            Input.ActionRelease(InputActions.MoveUp);
        }

        if (_frame == InteractFrame)
        {
            Input.ActionPress(InputActions.Interact);
        }
        else if (_frame == InteractFrame + 1)
        {
            Input.ActionRelease(InputActions.Interact);
        }

        _maxSpeed = Mathf.Max(_maxSpeed, Controller.Speed);
        float y = Player.GlobalPosition.Y;
        _maxDrop = Mathf.Max(_maxDrop, _lastY - y);
        _lastY = y;
        if (_frame > 5 && !Player.IsOnFloor())
        {
            _airFrames++;
        }

        if (_frame >= CheckFrame)
        {
            Finish();
        }
    }

    private void Finish()
    {
        _done = true;
        Vector3 end = Player!.GlobalPosition;
        float forward = _start.Z - end.Z;
        Check(forward > 11.0f, Inv($"travelled {forward:F2} m toward −Z through kerb, step and ramp"));
        Check(end.Y > 1.4f, Inv($"climbed to y = {end.Y:F2} m (kerb 0.15 + step 0.30 + ramp 1.0)"));
        Check(_maxSpeed > 4.0f, Inv($"peak speed {_maxSpeed:F2} m/s (run above 60 % deflection)"));
        Check(_airFrames <= 12, $"{_airFrames} airborne frames (slope and step snapping)");
        Check(_maxDrop < 0.05f, Inv($"largest single-frame drop {_maxDrop:F3} m (no jitter)"));
        Check(Mathf.Abs(end.X) < 0.5f, Inv($"stayed on the course line, x = {end.X:F2}"));
        Check(Interactable is { UseCount: > 0 }, $"interact used the test interactable (prompt '{_prompt}')");
        string summary = $"PlayerTest summary: {_pass} pass, {_fail} fail";
        GD.Print(summary);
        if (ReportLabel is not null)
        {
            ReportLabel.Text = summary + "\n" + string.Join("\n", _report);
        }
    }

    private static string Inv(FormattableString text) => FormattableString.Invariant(text);

    private void Check(bool ok, string message)
    {
        if (ok)
        {
            _pass++;
            Report("PASS", message);
        }
        else
        {
            Fail(message);
        }
    }

    private void Fail(string message)
    {
        _fail++;
        Report("FAIL", message);
        GD.PrintErr($"{FailPrefix}: {message}");
    }

    private void Report(string status, string message)
    {
        _report.Add($"[{status}] {message}");
        GD.Print($"PlayerTest {status}: {message}");
    }
}
