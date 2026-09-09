using System;
using System.Collections.Generic;
using BattleCity.Characters;
using Godot;

namespace BattleCity.Diagnostics;

/// <summary>
/// Drives Character.tscn through idle → walk → run → duel ready on a frame
/// schedule, moves the body at the matching speed, and checks that the
/// AnimationTree animates the skeleton and that data-injected events fire.
/// Run with <c>--fixed-fps 60</c> headless so the schedule is deterministic.
/// </summary>
public partial class CharacterTestScene : Node3D
{
    private const string FailPrefix = "CharacterTest FAIL";
    private const int WalkFrame = 30;
    private const int RunFrame = 120;
    private const int DuelFrame = 200;
    private const int CheckFrame = 260;
    private const float WalkSpeed = 2.2f;
    private const float RunSpeed = 4.5f;

    [Export]
    public Character? Character { get; set; }

    [Export]
    public Label? ReportLabel { get; set; }

    private readonly List<string> _report = new();
    private readonly List<string> _events = new();
    private int _frame;
    private int _passCount;
    private int _failCount;
    private bool _done;
    private Vector3 _startPosition;
    private bool _poseMoved;

    public override void _Ready()
    {
        if (Character is null)
        {
            Fail("Character not wired");
            _done = true;
            return;
        }

        Character.AnimationEvent += name => _events.Add($"{name}@{Character.CurrentState}");
        Report(Character.HasBody ? "PASS" : "INFO", Character.HasBody ? "body loaded" : "no body delivered, placeholder capsule and profile skeleton");
        Report(Character.HasClips ? "PASS" : "INFO", Character.HasClips ? "clips loaded from the carrier" : "no clips delivered, empty idle");
        Report("INFO", $"clip aliases: {string.Join(", ", Character.ClipAliases)}");
        Report("INFO", $"events injected: {string.Join(", ", Character.InjectedEvents)}");
        Check(Character.Skeleton is not null && Character.Skeleton.FindBone(Character.MountBone) >= 0, "skeleton has LeftLowerArm");
        Check(Character.DuelDiskMount is not null, "DuelDiskMount attached");
        Report(Character.Disk is { IsLoaded: true } ? "PASS" : "INFO",
            Character.Disk is { IsLoaded: true }
                ? $"duel disk loaded, markers {string.Join(", ", Character.Disk.MarkerNames)}"
                : "duel disk prop not delivered");
        _startPosition = Character.GlobalPosition;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_done || Character is null)
        {
            return;
        }

        _frame++;
        float speed = _frame switch
        {
            < WalkFrame => 0.0f,
            < RunFrame => WalkSpeed,
            < DuelFrame => RunSpeed,
            _ => 0.0f,
        };
        Character.SetLocomotion(speed);
        Character.Velocity = -Character.GlobalBasis.Z * speed;
        Character.MoveAndSlide();

        if (_frame == DuelFrame)
        {
            Character.PlayState(Character.DuelReadyState);
            Character.Disk?.Deploy();
        }

        if (!_poseMoved && Character.Skeleton is not null && Character.HasClips)
        {
            for (int bone = 0; bone < Character.Skeleton.GetBoneCount() && !_poseMoved; bone++)
            {
                Quaternion pose = Character.Skeleton.GetBonePoseRotation(bone);
                Quaternion rest = Character.Skeleton.GetBoneRest(bone).Basis.GetRotationQuaternion();
                _poseMoved = !pose.IsEqualApprox(rest);
            }
        }

        if (_frame >= CheckFrame)
        {
            Finish();
        }
    }

    private void Finish()
    {
        _done = true;
        if (Character is null)
        {
            return;
        }

        float travelled = Character.GlobalPosition.DistanceTo(_startPosition);
        float expected = ((RunFrame - WalkFrame) * WalkSpeed + (DuelFrame - RunFrame) * RunSpeed) / 60.0f;
        Check(Mathf.Abs(travelled - expected) < 0.5f, FormattableString.Invariant($"moved {travelled:F2} m over the walk/run schedule (expected {expected:F2})"));
        if (Character.HasClips)
        {
            Check(_poseMoved, "skeleton left rest pose while walking");
            bool footsteps = _events.Exists(e => e.StartsWith(AnimationEvents.FootstepLeft, StringComparison.Ordinal))
                && _events.Exists(e => e.StartsWith(AnimationEvents.FootstepRight, StringComparison.Ordinal));
            Check(footsteps, $"footstep events fired ({_events.Count} events: {string.Join(", ", _events)})");
        }
        else
        {
            Report("INFO", "no clips, pose and event checks skipped");
        }

        Check(Character.CurrentState is Character.DuelReadyState or Character.DuelIdleState,
            $"state after DuelReady travel: {Character.CurrentState}");
        string summary = $"CharacterTest summary: {_passCount} pass, {_failCount} fail";
        GD.Print(summary);
        if (ReportLabel is not null)
        {
            ReportLabel.Text = summary + "\n" + string.Join("\n", _report);
        }
    }

    private void Check(bool ok, string message)
    {
        if (ok)
        {
            _passCount++;
            Report("PASS", message);
        }
        else
        {
            Fail(message);
        }
    }

    private void Fail(string message)
    {
        _failCount++;
        Report("FAIL", message);
        GD.PrintErr($"{FailPrefix}: {message}");
    }

    private void Report(string status, string message)
    {
        _report.Add($"[{status}] {message}");
        GD.Print($"CharacterTest {status}: {message}");
    }
}
