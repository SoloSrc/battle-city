using BattleCity.Characters;
using BattleCity.Core;
using Godot;

namespace BattleCity.World;

/// <summary>
/// Keeps the player in the built area (architecture.md §7.1). While the
/// player is inside, the last grounded position is remembered; if the player
/// ever leaves the volume (a gap in the kit, a fall) they are put back there
/// and <see cref="PlayerRecovered"/> is raised. Levels still use kit collision
/// and invisible walls as the first line; this is the safety net.
/// </summary>
[Tool]
public partial class LevelBounds : Area3D
{
    [Signal]
    public delegate void PlayerRecoveredEventHandler(Vector3 from, Vector3 to);

    private Vector3 _size = new(120.0f, 40.0f, 120.0f);
    private Vector3 _lastSafe;
    private bool _hasSafe;

    [Export(PropertyHint.None, "suffix:m")]
    public Vector3 Size
    {
        get => _size;
        set
        {
            _size = new Vector3(Mathf.Max(value.X, 0.1f), Mathf.Max(value.Y, 0.1f), Mathf.Max(value.Z, 0.1f));
            BoxVolume.SyncShape(this, _size);
        }
    }

    /// <summary>Only positions this far inside the volume count as safe, so a recovery lands clear of the edge.</summary>
    [Export(PropertyHint.Range, "0,5,0.1,suffix:m")]
    public float SafeMargin { get; set; } = 1.0f;

    public int Recoveries { get; private set; }

    public override void _Ready()
    {
        BoxVolume.SyncShape(this, _size);
        if (Engine.IsEditorHint())
        {
            return;
        }

        CollisionLayer = PhysicsLayers.Trigger;
        CollisionMask = 0;
        Monitoring = false;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || GetTree().GetFirstNodeInGroup(Groups.Player) is not Character player)
        {
            return;
        }

        Vector3 position = player.GlobalPosition;
        if (Contains(position))
        {
            if (player.IsOnFloor() && Contains(position, SafeMargin))
            {
                _lastSafe = position;
                _hasSafe = true;
            }

            return;
        }

        if (!_hasSafe)
        {
            return;
        }

        Recoveries++;
        player.GlobalPosition = _lastSafe;
        player.Velocity = Vector3.Zero;
        EmitSignal(SignalName.PlayerRecovered, position, _lastSafe);
    }

    public bool Contains(Vector3 worldPoint, float inset = 0.0f)
    {
        Vector3 local = ToLocal(worldPoint);
        Vector3 half = _size * 0.5f - Vector3.One * inset;
        return Mathf.Abs(local.X) <= half.X && Mathf.Abs(local.Y) <= half.Y && Mathf.Abs(local.Z) <= half.Z;
    }
}
