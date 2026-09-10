using BattleCity.Characters;
using BattleCity.Core;
using Godot;

namespace BattleCity.World;

/// <summary>
/// Talking NPC marker, a child of a <see cref="Character"/> instance
/// (<c>scenes/world/TalkNpc.tscn</c>). Interacting raises <see cref="TalkRequested"/>
/// with the dialogue id; the dialogue system runs it (systems.md §3.3).
/// </summary>
public partial class TalkNpc : Area3D, IInteractable
{
    [Signal]
    public delegate void TalkRequestedEventHandler(string dialogueId, Character player);

    [Export]
    public string DialogueId { get; set; } = "npc";

    /// <summary>Appearance preset id applied once CharacterAppearance lands.</summary>
    [Export]
    public string Appearance { get; set; } = "default";

    /// <summary>Turn to face the player when talked to.</summary>
    [Export]
    public bool FacePlayer { get; set; } = true;

    public string Prompt => "Talk";

    public Character? Character => GetParentOrNull<Character>();

    public override void _Ready()
    {
        AddToGroup(Groups.Npc);
        CollisionLayer = PhysicsLayers.Interactable;
        CollisionMask = 0;
        Monitoring = false;
    }

    public bool CanInteract(Character by) => true;

    public void Interact(Character by)
    {
        if (FacePlayer && Character is not null)
        {
            Vector3 to = by.GlobalPosition - Character.GlobalPosition;
            to.Y = 0.0f;
            if (to.LengthSquared() > 0.001f)
            {
                Character.LookAt(Character.GlobalPosition + to, Vector3.Up, true);
            }
        }

        EmitSignal(SignalName.TalkRequested, DialogueId, by);
    }
}
