using BattleCity.Characters;
using BattleCity.Core;
using Godot;

namespace BattleCity.World;

/// <summary>
/// The duelist marker: a child of a <see cref="Character"/> instance
/// (<c>scenes/world/Duelist.tscn</c>). Holds the id, the detection cone and
/// the interaction area (systems.md §3.3, §4.3). Detection and challenge are
/// reported through signals; the encounter system (issue #23) decides what
/// happens. The cone is armed while <see cref="Armed"/> and disarmed by the
/// encounter system after a duel.
/// </summary>
public partial class Duelist : Area3D, IInteractable
{
    [Signal]
    public delegate void PlayerSpottedEventHandler(Character player);

    [Signal]
    public delegate void ChallengedEventHandler(Character player);

    [Export]
    public string DuelistId { get; set; } = "d1";

    [ExportGroup("Detection cone (systems.md §4.3)")]
    [Export(PropertyHint.Range, "0,20,0.5,suffix:m")]
    public float ConeRange { get; set; } = 8.0f;

    [Export(PropertyHint.Range, "0,180,5,suffix:°")]
    public float ConeAngle { get; set; } = 60.0f;

    /// <summary>Whether the cone triggers; false after the first victory or for locked duelists.</summary>
    [Export]
    public bool Armed { get; set; } = true;

    /// <summary>Whether the player may challenge by interacting (false while locked).</summary>
    [Export]
    public bool Challengeable { get; set; } = true;

    public string Prompt => "Duel";

    public Character? Character => GetParentOrNull<Character>();

    /// <summary>World-space facing of the cone, the character's −Z.</summary>
    public Vector3 Facing => -(Character?.GlobalTransform.Basis.Z ?? GlobalTransform.Basis.Z);

    private bool _spotted;

    public override void _Ready()
    {
        AddToGroup(Groups.Duelist);
        AddToGroup(Groups.Npc);
        CollisionLayer = PhysicsLayers.Interactable;
        CollisionMask = 0;
        Monitoring = false;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Armed)
        {
            _spotted = false;
            return;
        }

        if (GetTree().GetFirstNodeInGroup(Groups.Player) is not Character player)
        {
            return;
        }

        bool inside = IsInCone(player.GlobalPosition);
        if (inside && !_spotted)
        {
            _spotted = true;
            EmitSignal(SignalName.PlayerSpotted, player);
        }
        else if (!inside)
        {
            _spotted = false;
        }
    }

    /// <summary>True when <paramref name="point"/> is within range and half-angle of the facing.</summary>
    public bool IsInCone(Vector3 point)
    {
        Vector3 origin = Character?.GlobalPosition ?? GlobalPosition;
        Vector3 to = point - origin;
        to.Y = 0.0f;
        float distance = to.Length();
        if (distance < 0.01f)
        {
            return true;
        }

        if (distance > ConeRange)
        {
            return false;
        }

        Vector3 facing = Facing;
        facing.Y = 0.0f;
        float angle = Mathf.RadToDeg(facing.Normalized().AngleTo(to / distance));
        return angle <= ConeAngle * 0.5f;
    }

    public bool CanInteract(Character by) => Challengeable;

    public void Interact(Character by)
    {
        EmitSignal(SignalName.Challenged, by);
    }
}
