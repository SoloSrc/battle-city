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

    /// <summary>The yaw in use: <see cref="Yaw"/>, or the duel framing's while one is active.</summary>
    public float CurrentYaw { get; private set; }

    /// <summary>A duel framing is set (systems.md §4.2 "Duel" column); the rig holds it instead of following.</summary>
    public bool DuelActive => _duel is not null;

    /// <summary>The point a held framing looks at (duel or encounter reveal), null while following the target.</summary>
    public Vector3? HeldFocus => _duel?.Focus;

    /// <summary>Holds the overworld framing on <paramref name="point"/> (an encounter reveal); <see cref="ExitDuel"/> returns to the target.</summary>
    public void Reveal(Vector3 point, float blendTime) =>
        EnterDuel(point + Vector3.Up * FocusHeight, CurrentYaw, Pitch, Distance, Fov, blendTime);

    /// <summary>0–1 progress of the last framing blend (duel in or out).</summary>
    public float BlendProgress { get; private set; } = 1.0f;

    private DuelFraming? _duel;
    private Framing _blendFrom;
    private float _blendTime;
    private bool _blendingOut;

    /// <summary>Screen-up direction on the ground plane; matches <see cref="PlayerController.CameraYaw"/>.</summary>
    public Vector3 Forward => new Vector3(0.0f, 0.0f, -1.0f).Rotated(Vector3.Up, Mathf.DegToRad(Yaw));

    public override void _Ready()
    {
        Pivot ??= GetNodeOrNull<Node3D>("Pivot");
        Camera ??= Pivot?.GetNodeOrNull<Camera3D>("Camera3D");
        Target ??= GetTree().GetFirstNodeInGroup(Groups.Player) as Node3D;
        CurrentYaw = Yaw;
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

    /// <summary>
    /// Blends to a fixed framing over <paramref name="blendTime"/> seconds and holds it: the camera looks
    /// at <paramref name="focus"/> from <paramref name="yaw"/> degrees, pitched <paramref name="pitch"/>
    /// degrees down, <paramref name="distance"/> metres away with a vertical <paramref name="fov"/>. Bounds
    /// do not apply while it holds.
    /// </summary>
    public void EnterDuel(Vector3 focus, float yaw, float pitch, float distance, float fov, float blendTime)
    {
        _blendFrom = Current();
        _duel = new DuelFraming(focus, yaw, pitch, distance, fov);
        _blendTime = Mathf.Max(0.0f, blendTime);
        _blendingOut = false;
        BlendProgress = _blendTime > 0.0f ? 0.0f : 1.0f;
    }

    /// <summary>Blends back to following the target over <paramref name="blendTime"/> seconds.</summary>
    public void ExitDuel(float blendTime)
    {
        if (_duel is null)
        {
            return;
        }

        _blendFrom = Current();
        _duel = null;
        _blendTime = Mathf.Max(0.0f, blendTime);
        _blendingOut = _blendTime > 0.0f;
        BlendProgress = _blendingOut ? 0.0f : 1.0f;
    }

    /// <summary>Jumps to the target immediately (spawn, door transition).</summary>
    public void Snap()
    {
        if (_duel is not null)
        {
            return;
        }

        CurrentYaw = Yaw;
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
        if (BlendProgress < 1.0f)
        {
            BlendProgress = Mathf.Min(1.0f, BlendProgress + delta / _blendTime);
        }

        if (_duel is { } duel)
        {
            Framing target = new(duel.Focus, duel.Yaw, duel.Pitch, duel.Distance, duel.Fov);
            Set(BlendProgress < 1.0f ? _blendFrom.Blend(target, Mathf.SmoothStep(0.0f, 1.0f, BlendProgress)) : target);
            IsClamped = false;
            Apply();
            return;
        }

        Vector3 desired = TargetPoint();
        ActiveBounds = FindActiveBounds(desired);
        Vector3 clamped = ClampToBounds(desired);
        IsClamped = clamped.DistanceSquaredTo(desired) > 1e-6f;
        (float pitch, float distance, float fov) = ProfileFor(ActiveBounds);

        if (_blendingOut)
        {
            Framing follow = new(clamped, Yaw, pitch, distance, fov);
            Set(_blendFrom.Blend(follow, Mathf.SmoothStep(0.0f, 1.0f, BlendProgress)));
            _blendingOut = BlendProgress < 1.0f;
            Apply();
            return;
        }

        float k = Smoothing <= 0.0f ? 1.0f : 1.0f - Mathf.Exp(-delta / Smoothing);
        GlobalPosition = GlobalPosition.Lerp(clamped, k);

        float p = ProfileBlend <= 0.0f ? 1.0f : 1.0f - Mathf.Exp(-delta / ProfileBlend);
        CurrentYaw = Yaw;
        CurrentPitch = Mathf.Lerp(CurrentPitch, pitch, p);
        CurrentDistance = Mathf.Lerp(CurrentDistance, distance, p);
        CurrentFov = Mathf.Lerp(CurrentFov, fov, p);
        Apply();
    }

    private Framing Current() => new(GlobalPosition, CurrentYaw, CurrentPitch, CurrentDistance, CurrentFov);

    private void Set(Framing framing)
    {
        GlobalPosition = framing.Position;
        CurrentYaw = framing.Yaw;
        CurrentPitch = framing.Pitch;
        CurrentDistance = framing.Distance;
        CurrentFov = framing.Fov;
    }

    private void Apply()
    {
        Rotation = new Vector3(0.0f, Mathf.DegToRad(CurrentYaw), 0.0f);
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

    private readonly record struct DuelFraming(Vector3 Focus, float Yaw, float Pitch, float Distance, float Fov);

    /// <summary>The rig's pose as one value so two of them can be blended.</summary>
    private readonly record struct Framing(Vector3 Position, float Yaw, float Pitch, float Distance, float Fov)
    {
        public Framing Blend(Framing to, float t) => new(
            Position.Lerp(to.Position, t),
            Mathf.LerpAngle(Mathf.DegToRad(Yaw), Mathf.DegToRad(to.Yaw), t) * (180.0f / Mathf.Pi),
            Mathf.Lerp(Pitch, to.Pitch, t),
            Mathf.Lerp(Distance, to.Distance, t),
            Mathf.Lerp(Fov, to.Fov, t));
    }
}
