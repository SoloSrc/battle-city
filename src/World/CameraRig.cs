using System;
using BattleCity.Characters;
using BattleCity.Core;
using Godot;

namespace BattleCity.World;

/// <summary>
/// The overworld camera (systems.md §4.2): fixed yaw, tilted pivot, camera at a
/// distance along the pivot's +Z. Follows the target with exponential smoothing,
/// clamps to the union of <see cref="CameraBounds"/> volumes inset by
/// <see cref="SoftMargin"/>, and blends to an interior volume's pitch, distance
/// and FOV while the target is inside it. The tuning values are exported so
/// the director can compare framings in the inspector; the GDD values are the
/// defaults and change only by director decision.
/// </summary>
public partial class CameraRig : Node3D
{
    [ExportGroup("Framing (systems.md §4.2)")]
    [Export(PropertyHint.Range, "-180,180,1,suffix:°")]
    public float Yaw { get; set; }

    [Export(PropertyHint.Range, "10,85,1,suffix:°")]
    public float Pitch { get; set; } = 57.0f;

    [Export(PropertyHint.Range, "2,30,0.1,suffix:m")]
    public float Distance { get; set; } = 12.0f;

    [Export(PropertyHint.Range, "10,90,1,suffix:°")]
    public float Fov { get; set; } = 35.0f;

    /// <summary>Height above the target's origin the camera looks at.</summary>
    [Export(PropertyHint.Range, "0,2,0.05,suffix:m")]
    public float FocusHeight { get; set; } = 0.9f;

    [ExportGroup("Follow")]
    [Export(PropertyHint.Range, "0,1,0.01,suffix:s")]
    public float Smoothing { get; set; } = 0.15f;

    /// <summary>Time constant for blending pitch, distance and FOV between profiles.</summary>
    [Export(PropertyHint.Range, "0,2,0.05,suffix:s")]
    public float ProfileBlend { get; set; } = 0.4f;

    [Export(PropertyHint.Range, "0,5,0.1,suffix:m")]
    public float SoftMargin { get; set; } = 1.0f;

    /// <summary>Follow target; the first node in the <c>player</c> group when empty.</summary>
    [Export]
    public Node3D? Target { get; set; }

    [Export]
    public Node3D? Pivot { get; set; }

    [Export]
    public Camera3D? Camera { get; set; }

    public CameraBounds? ActiveBounds { get; private set; }

    public float CurrentPitch { get; private set; }

    public float CurrentDistance { get; private set; }

    public float CurrentFov { get; private set; }

    /// <summary>True on the last update when the bounds moved the rig away from the target.</summary>
    public bool IsClamped { get; private set; }

    /// <summary>Screen-up direction on the ground plane; matches <see cref="PlayerController.CameraYaw"/>.</summary>
    public Vector3 Forward => new Vector3(0.0f, 0.0f, -1.0f).Rotated(Vector3.Up, Mathf.DegToRad(Yaw));

    public override void _Ready()
    {
        Pivot ??= GetNodeOrNull<Node3D>("Pivot");
        Camera ??= Pivot?.GetNodeOrNull<Camera3D>("Camera3D");
        Target ??= GetTree().GetFirstNodeInGroup(Groups.Player) as Node3D;
        CurrentPitch = Pitch;
        CurrentDistance = Distance;
        CurrentFov = Fov;
        AlignController();
        Snap();
    }

    public override void _PhysicsProcess(double delta)
    {
        Follow((float)delta);
    }

    /// <summary>Jumps to the target immediately (spawn, door transition).</summary>
    public void Snap()
    {
        ActiveBounds = FindActiveBounds(TargetPoint());
        (float pitch, float distance, float fov) = ProfileFor(ActiveBounds);
        CurrentPitch = pitch;
        CurrentDistance = distance;
        CurrentFov = fov;
        GlobalPosition = ClampToBounds(TargetPoint());
        Apply();
    }

    private void Follow(float delta)
    {
        Vector3 desired = TargetPoint();
        ActiveBounds = FindActiveBounds(desired);
        Vector3 clamped = ClampToBounds(desired);
        IsClamped = clamped.DistanceSquaredTo(desired) > 1e-6f;

        float k = Smoothing <= 0.0f ? 1.0f : 1.0f - Mathf.Exp(-delta / Smoothing);
        GlobalPosition = GlobalPosition.Lerp(clamped, k);

        (float pitch, float distance, float fov) = ProfileFor(ActiveBounds);
        float p = ProfileBlend <= 0.0f ? 1.0f : 1.0f - Mathf.Exp(-delta / ProfileBlend);
        CurrentPitch = Mathf.Lerp(CurrentPitch, pitch, p);
        CurrentDistance = Mathf.Lerp(CurrentDistance, distance, p);
        CurrentFov = Mathf.Lerp(CurrentFov, fov, p);
        Apply();
    }

    private void Apply()
    {
        Rotation = new Vector3(0.0f, Mathf.DegToRad(Yaw), 0.0f);
        if (Pivot is not null)
        {
            Pivot.Rotation = new Vector3(-Mathf.DegToRad(CurrentPitch), 0.0f, 0.0f);
        }

        if (Camera is not null)
        {
            Camera.Position = new Vector3(0.0f, 0.0f, CurrentDistance);
            Camera.Fov = CurrentFov;
        }
    }

    private Vector3 TargetPoint()
    {
        Vector3 origin = Target?.GlobalPosition ?? GlobalPosition;
        return origin + Vector3.Up * FocusHeight;
    }

    private (float Pitch, float Distance, float Fov) ProfileFor(CameraBounds? bounds)
    {
        return bounds is { Interior: true }
            ? (bounds.Pitch, bounds.Distance, bounds.Fov)
            : (Pitch, Distance, Fov);
    }

    private CameraBounds? FindActiveBounds(Vector3 point)
    {
        CameraBounds? best = null;
        foreach (Node node in GetTree().GetNodesInGroup(Groups.CameraBounds))
        {
            if (node is CameraBounds bounds && bounds.Contains(point)
                && (best is null || bounds.Priority > best.Priority))
            {
                best = bounds;
            }
        }

        return best;
    }

    /// <summary>
    /// Nearest point inside the union of all inset volumes. With no volumes in
    /// the scene the rig follows freely.
    /// </summary>
    private Vector3 ClampToBounds(Vector3 point)
    {
        Vector3 best = point;
        float bestDistance = float.PositiveInfinity;
        bool any = false;
        foreach (Node node in GetTree().GetNodesInGroup(Groups.CameraBounds))
        {
            if (node is not CameraBounds bounds)
            {
                continue;
            }

            any = true;
            Vector3 candidate = bounds.ClampXZ(point, SoftMargin);
            float distance = candidate.DistanceSquaredTo(point);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return any ? best : point;
    }

    private void AlignController()
    {
        if (Target is null)
        {
            return;
        }

        foreach (Node child in Target.GetChildren())
        {
            if (child is PlayerController controller)
            {
                controller.CameraYaw = Yaw;
            }
        }
    }
}
