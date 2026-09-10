using System.Collections.Generic;
using BattleCity.Characters;
using BattleCity.Core;
using Godot;

namespace BattleCity.World;

/// <summary>
/// The duelist marker: a child of a <see cref="Character"/> instance
/// (<c>scenes/world/Duelist.tscn</c>). Holds the id, the detection cone and
/// the interaction area (systems.md §3.3, §4.3). Detection and challenge are
/// reported through signals; <see cref="EncounterSystem"/> decides what
/// happens. The cone is live while <see cref="IsArmed"/>: the designer's
/// <see cref="Armed"/> switch, not locked behind <see cref="RequiredFlag"/>,
/// not yet <see cref="Defeated"/>, and not inside the post-duel disarm window.
/// </summary>
public partial class Duelist : Area3D, IInteractable
{
    [Signal]
    public delegate void PlayerSpottedEventHandler(Character player);

    [Signal]
    public delegate void ChallengedEventHandler(Character player);

    [Export]
    public string DuelistId { get; set; } = "d1";

    /// <summary>Shown in the placeholder dialogue until data/duelists.json (issue #26) supplies names.</summary>
    [Export]
    public string DisplayName { get; set; } = "Duelist";

    /// <summary>Placeholder challenge line until the dialogue system lands.</summary>
    [Export(PropertyHint.MultilineText)]
    public string ChallengeLine { get; set; } = "Let's duel!";

    /// <summary>Flag that unlocks this duelist; empty means always unlocked. Locked duelists neither spot nor accept challenges.</summary>
    [Export]
    public string RequiredFlag { get; set; } = "";

    [ExportGroup("Detection cone (systems.md §4.3)")]
    [Export(PropertyHint.Range, "0,20,0.5,suffix:m")]
    public float ConeRange { get; set; } = 8.0f;

    [Export(PropertyHint.Range, "0,180,5,suffix:°")]
    public float ConeAngle { get; set; } = 60.0f;

    /// <summary>Designer switch for the cone; a duelist with it off is challenge-only.</summary>
    [Export]
    public bool Armed { get; set; } = true;

    /// <summary>Whether the player may challenge by interacting (rematches stay on after a victory).</summary>
    [Export]
    public bool Challengeable { get; set; } = true;

    /// <summary>True while <see cref="RequiredFlag"/> is set and missing from the game flags.</summary>
    public bool Locked { get; private set; }

    /// <summary>Set after the player's first victory; the cone stays off, rematches remain manual.</summary>
    public bool Defeated { get; set; }

    /// <summary>Seconds left in the post-duel disarm window (systems.md §4.3: 3 s).</summary>
    public float RearmIn { get; private set; }

    public bool IsArmed => Armed && !Locked && !Defeated && RearmIn <= 0.0f;

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
        Locked = RequiredFlag.Length > 0;
    }

    public override void _PhysicsProcess(double delta)
    {
        Character? player = GetTree().GetFirstNodeInGroup(Groups.Player) as Character;
        if (RearmIn > 0.0f)
        {
            RearmIn -= (float)delta;
            if (RearmIn <= 0.0f)
            {
                RearmIn = 0.0f;
                // Re-arm edge-triggered: a player still standing in the cone must leave and come back.
                _spotted = player is not null && IsInCone(player.GlobalPosition);
            }

            return;
        }

        if (!IsArmed || player is null || Game.Instance is { IsTransitioning: true })
        {
            _spotted = false;
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

    /// <summary>Updates <see cref="Locked"/> and <see cref="Defeated"/> from the game flags (called by <c>Game</c> on load and on every flag change).</summary>
    public void ApplyFlags(IReadOnlySet<string> flags)
    {
        Locked = RequiredFlag.Length > 0 && !flags.Contains(RequiredFlag);
        if (flags.Contains(Flags.Defeated(DuelistId)))
        {
            Defeated = true;
        }
    }

    /// <summary>Turns the cone off for <paramref name="seconds"/>; after that it re-arms edge-triggered.</summary>
    public void DisarmFor(float seconds)
    {
        RearmIn = Mathf.Max(seconds, 0.001f);
        _spotted = false;
    }

    public bool CanInteract(Character by) => Challengeable && !Locked;

    public void Interact(Character by)
    {
        EmitSignal(SignalName.Challenged, by);
    }
}
