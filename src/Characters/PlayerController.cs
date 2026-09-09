using System;
using BattleCity.Core;
using BattleCity.World;
using Godot;

namespace BattleCity.Characters;

/// <summary>
/// Gamepad-first analogue movement for the avatar (gdd.md §1.2, systems.md §4.1).
/// Child of a <see cref="Character"/>: reads the move actions, turns the body
/// toward the stick, blends the locomotion parameter, keeps the capsule on
/// slopes and steps up kerbs, and runs the <c>interact</c> probe.
/// </summary>
public partial class PlayerController : Node
{
    /// <summary>Prompt of the nearest usable interactable, empty when none.</summary>
    [Signal]
    public delegate void PromptChangedEventHandler(string prompt);

    [ExportGroup("Movement (systems.md §4.1)")]
    [Export(PropertyHint.Range, "0,10,0.1,suffix:m/s")]
    public float WalkSpeed { get; set; } = 2.2f;

    [Export(PropertyHint.Range, "0,10,0.1,suffix:m/s")]
    public float RunSpeed { get; set; } = 4.5f;

    /// <summary>Stick deflection above which the character runs.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")]
    public float RunThreshold { get; set; } = 0.6f;

    [Export(PropertyHint.Range, "0.01,1,0.01,suffix:s")]
    public float AccelTime { get; set; } = 0.12f;

    [Export(PropertyHint.Range, "0.01,1,0.01,suffix:s")]
    public float DecelTime { get; set; } = 0.08f;

    [Export(PropertyHint.Range, "0,1440,10,suffix:°/s")]
    public float TurnRate { get; set; } = 720.0f;

    [Export(PropertyHint.Range, "0,1,0.05,suffix:m")]
    public float StepHeight { get; set; } = 0.3f;

    /// <summary>Yaw of the camera in degrees; 0 means stick-up moves toward −Z (fixed-yaw camera).</summary>
    [Export(PropertyHint.Range, "-180,180,1,suffix:°")]
    public float CameraYaw { get; set; }

    [ExportGroup("State")]
    /// <summary>False while dialogue, menus or an encounter own the input.</summary>
    [Export]
    public bool InputEnabled { get; set; } = true;

    public Character? Character { get; private set; }

    /// <summary>Current planar speed in m/s.</summary>
    public float Speed { get; private set; }

    public bool IsRunning { get; private set; }

    public IInteractable? CurrentInteractable { get; private set; }

    private float _gravity;
    private Vector3 _planarVelocity;
    private string _lastPrompt = string.Empty;

    public override void _Ready()
    {
        Character = GetParent<Character>();
        _gravity = ProjectSettings.GetSetting("physics/3d/default_gravity").AsSingle();
        Character.FloorMaxAngle = Mathf.DegToRad(40.0f);
        Character.FloorSnapLength = 0.3f;
        Character.FloorStopOnSlope = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Character is null)
        {
            return;
        }

        float dt = (float)delta;
        Vector2 stick = InputEnabled ? Input.GetVector(InputActions.MoveLeft, InputActions.MoveRight, InputActions.MoveUp, InputActions.MoveDown) : Vector2.Zero;
        float deflection = Mathf.Min(stick.Length(), 1.0f);
        bool runHeld = InputEnabled && Input.IsActionPressed(InputActions.RunToggle);
        IsRunning = deflection > 0.0f && (deflection >= RunThreshold || runHeld);

        Vector3 wish = Vector3.Zero;
        if (deflection > 0.0f)
        {
            wish = new Vector3(stick.X, 0.0f, stick.Y).Rotated(Vector3.Up, Mathf.DegToRad(CameraYaw)).Normalized();
        }

        float targetSpeed = deflection <= 0.0f ? 0.0f : IsRunning ? RunSpeed : WalkSpeed;
        Vector3 targetVelocity = wish * targetSpeed;
        float rate = (targetSpeed > _planarVelocity.Length() ? RunSpeed / AccelTime : RunSpeed / DecelTime) * dt;
        _planarVelocity = _planarVelocity.MoveToward(targetVelocity, rate);
        Speed = _planarVelocity.Length();

        if (wish != Vector3.Zero)
        {
            TurnToward(wish, dt);
        }

        float verticalVelocity = Character.IsOnFloor() ? -0.1f : Character.Velocity.Y - _gravity * dt;
        Character.Velocity = new Vector3(_planarVelocity.X, verticalVelocity, _planarVelocity.Z);
        Character.MoveAndSlide();
        if (wish != Vector3.Zero && Character.IsOnWall())
        {
            TryStepUp(wish);
        }

        Character.SetLocomotion(Speed);
        UpdatePrompt();
        if (InputEnabled && Input.IsActionJustPressed(InputActions.Interact))
        {
            CurrentInteractable?.Interact(Character);
        }
    }

    private void TurnToward(Vector3 direction, float dt)
    {
        float targetYaw = Mathf.Atan2(-direction.X, -direction.Z);
        float yaw = Mathf.RotateToward(Character!.Rotation.Y, targetYaw, Mathf.DegToRad(TurnRate) * dt);
        Character.Rotation = new Vector3(0.0f, yaw, 0.0f);
    }

    /// <summary>
    /// Kerb and stair helper: when a wall blocks the move, probe up, forward and
    /// down; if a walkable floor sits within <see cref="StepHeight"/>, snap onto it.
    /// </summary>
    private void TryStepUp(Vector3 direction)
    {
        Character body = Character!;
        Vector3 up = Vector3.Up * StepHeight;
        Vector3 forward = direction * 0.25f;
        var hit = new KinematicCollision3D();

        Transform3D from = body.GlobalTransform;
        if (body.TestMove(from, up, hit))
        {
            up = hit.GetTravel();
        }

        Transform3D raised = from.Translated(up);
        if (body.TestMove(raised, forward, hit))
        {
            return;
        }

        Transform3D ahead = raised.Translated(forward);
        if (!body.TestMove(ahead, -up, hit))
        {
            return;
        }

        float floorAngle = hit.GetNormal().AngleTo(Vector3.Up);
        if (floorAngle > body.FloorMaxAngle)
        {
            return;
        }

        body.GlobalPosition = ahead.Origin + hit.GetTravel();
        body.Velocity = new Vector3(_planarVelocity.X, 0.0f, _planarVelocity.Z);
    }

    private void UpdatePrompt()
    {
        CurrentInteractable = FindNearestInteractable();
        string prompt = CurrentInteractable?.Prompt ?? string.Empty;
        if (prompt != _lastPrompt)
        {
            _lastPrompt = prompt;
            EmitSignal(SignalName.PromptChanged, prompt);
        }
    }

    private IInteractable? FindNearestInteractable()
    {
        if (Character?.InteractionShape is not Area3D probe)
        {
            return null;
        }

        IInteractable? best = null;
        float bestDistance = float.MaxValue;
        Vector3 origin = probe.GlobalPosition;
        foreach (Node3D candidate in probe.GetOverlappingAreas())
        {
            Consider(candidate, origin, ref best, ref bestDistance);
        }

        foreach (Node3D candidate in probe.GetOverlappingBodies())
        {
            Consider(candidate, origin, ref best, ref bestDistance);
        }

        return best;
    }

    private void Consider(Node3D candidate, Vector3 origin, ref IInteractable? best, ref float bestDistance)
    {
        if (candidate is not IInteractable interactable || !interactable.CanInteract(Character!))
        {
            return;
        }

        float distance = candidate.GlobalPosition.DistanceSquaredTo(origin);
        if (distance < bestDistance)
        {
            bestDistance = distance;
            best = interactable;
        }
    }
}
