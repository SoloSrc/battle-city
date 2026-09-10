using BattleCity.Core;
using Godot;

namespace BattleCity.World;

/// <summary>
/// Duel staging site (systems.md §3.4, §4.3). The centre is the node origin,
/// the stand points sit <see cref="StandDistance"/> apart along
/// <see cref="Axis"/>, and <see cref="ClearanceSize"/> is the invisible volume
/// that must be free of world collision (16 × 12 m pocket). The editor gizmo
/// draws the footprint, axis and stand points.
/// </summary>
public partial class EncounterSite : Node3D
{
    [Export]
    public string Id { get; set; } = "site";

    [Export]
    public string DuelistId { get; set; } = "d1";

    /// <summary>Local direction from stand point A to stand point B.</summary>
    [Export]
    public Vector3 Axis { get; set; } = Vector3.Right;

    [Export(PropertyHint.Range, "3,12,0.5,suffix:m")]
    public float StandDistance { get; set; } = 7.0f;

    [Export(PropertyHint.None, "suffix:m")]
    public Vector3 ClearanceSize { get; set; } = new(16.0f, 4.0f, 12.0f);

    public Vector3 AxisWorld => GlobalTransform.Basis * Axis.Normalized();

    /// <summary>Stand point on the negative side of the axis (the player's side by default).</summary>
    public Vector3 StandPointA => GlobalPosition - AxisWorld * (StandDistance * 0.5f);

    /// <summary>Stand point on the positive side of the axis (the duelist's side by default).</summary>
    public Vector3 StandPointB => GlobalPosition + AxisWorld * (StandDistance * 0.5f);

    /// <summary>Clearance box in world space, centred on the site (axis-aligned to the node).</summary>
    public Transform3D ClearanceTransform => new(GlobalTransform.Basis.Orthonormalized(), GlobalPosition + Vector3.Up * (ClearanceSize.Y * 0.5f));

    public override void _Ready()
    {
        AddToGroup(Groups.EncounterSite);
    }
}
