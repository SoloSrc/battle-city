using BattleCity.Characters;
using BattleCity.Core;
using Godot;

namespace BattleCity.World;

/// <summary>
/// Interior transition (architecture.md §7.1). Interacting raises
/// <see cref="TransitionRequested"/>; <c>Game.Transition</c> (issue #23) fades,
/// loads <see cref="TargetScene"/> and places the player at <see cref="TargetSpawn"/>.
/// <see cref="ReturnSpawn"/> names the spawn in this scene that the target's
/// exit door returns to; the level checklist verifies both ends.
/// </summary>
public partial class Door : Area3D, IInteractable
{
    [Signal]
    public delegate void TransitionRequestedEventHandler(string targetScene, string targetSpawn, Character player);

    [Export(PropertyHint.File, "*.tscn")]
    public string TargetScene { get; set; } = "";

    [Export]
    public string TargetSpawn { get; set; } = "";

    [Export]
    public string ReturnSpawn { get; set; } = "";

    [Export]
    public string Verb { get; set; } = "Enter";

    public string Prompt => Verb;

    public override void _Ready()
    {
        AddToGroup(Groups.Interactable);
        CollisionLayer = PhysicsLayers.Interactable;
        CollisionMask = 0;
        Monitoring = false;
    }

    public bool CanInteract(Character by) => !string.IsNullOrEmpty(TargetScene);

    public void Interact(Character by)
    {
        EmitSignal(SignalName.TransitionRequested, TargetScene, TargetSpawn, by);
    }
}
