using BattleCity.Core;
using Godot;

namespace BattleCity.World;

/// <summary>
/// Camera clamp volume for one area (systems.md §3.4, §4.2). The level designer
/// places one per walkable region and sets <see cref="Size"/>; the
/// <see cref="CameraRig"/> keeps its XZ inside the union of all volumes, inset
/// by its soft margin. Adjacent volumes should overlap by at least twice that
/// margin so the seam has no dead band. An <see cref="Interior"/> volume also
/// overrides pitch, distance and FOV while the target is inside it.
/// </summary>
[Tool]
public partial class CameraBounds : Area3D
{
    private Vector3 _size = new(20.0f, 10.0f, 20.0f);

    [Export(PropertyHint.None, "suffix:m")]
    public Vector3 Size
    {
        get => _size;
        set
        {
            _size = new Vector3(Mathf.Max(value.X, 0.1f), Mathf.Max(value.Y, 0.1f), Mathf.Max(value.Z, 0.1f));
            SyncShape();
        }
    }

    [ExportGroup("Interior override (systems.md §4.2)")]
    [Export]
    public bool Interior { get; set; }

    [Export(PropertyHint.Range, "10,85,1,suffix:°")]
    public float Pitch { get; set; } = 50.0f;

    [Export(PropertyHint.Range, "2,30,0.1,suffix:m")]
    public float Distance { get; set; } = 7.0f;

    [Export(PropertyHint.Range, "10,90,1,suffix:°")]
    public float Fov { get; set; } = 35.0f;

    [Export]
    public CollisionShape3D? Shape { get; set; }

    public override void _Ready()
    {
        SyncShape();
        if (Engine.IsEditorHint())
        {
            return;
        }

        AddToGroup(Groups.CameraBounds);
        CollisionLayer = PhysicsLayers.Trigger;
        CollisionMask = 0;
        Monitoring = false;
    }

    /// <summary>True when <paramref name="worldPoint"/> lies inside the full volume.</summary>
    public bool Contains(Vector3 worldPoint)
    {
        Vector3 local = ToLocal(worldPoint);
        Vector3 half = _size * 0.5f;
        return Mathf.Abs(local.X) <= half.X && Mathf.Abs(local.Y) <= half.Y && Mathf.Abs(local.Z) <= half.Z;
    }

    /// <summary>
    /// Clamps the XZ of <paramref name="worldPoint"/> into the volume inset by
    /// <paramref name="margin"/> on each side. Height is left untouched.
    /// </summary>
    public Vector3 ClampXZ(Vector3 worldPoint, float margin)
    {
        Vector3 local = ToLocal(worldPoint);
        Vector3 half = _size * 0.5f;
        float hx = Mathf.Max(half.X - margin, 0.0f);
        float hz = Mathf.Max(half.Z - margin, 0.0f);
        local.X = Mathf.Clamp(local.X, -hx, hx);
        local.Z = Mathf.Clamp(local.Z, -hz, hz);
        Vector3 clamped = ToGlobal(local);
        clamped.Y = worldPoint.Y;
        return clamped;
    }

    private void SyncShape()
    {
        Shape ??= GetNodeOrNull<CollisionShape3D>("Shape");
        if (Shape is null)
        {
            return;
        }

        if (Shape.Shape is not BoxShape3D box)
        {
            box = new BoxShape3D();
            Shape.Shape = box;
        }

        box.Size = _size;
    }
}
