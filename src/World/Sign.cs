using BattleCity.Characters;
using BattleCity.Core;
using Godot;

namespace BattleCity.World;

/// <summary>Sign: one text box on interact (systems.md §3.3).</summary>
public partial class Sign : Area3D, IInteractable
{
    [Signal]
    public delegate void ReadEventHandler(string text, Character player);

    [Export(PropertyHint.MultilineText)]
    public string Text { get; set; } = "";

    public string Prompt => "Read";

    public override void _Ready()
    {
        AddToGroup(Groups.Interactable);
        CollisionLayer = PhysicsLayers.Interactable;
        CollisionMask = 0;
        Monitoring = false;
    }

    public bool CanInteract(Character by) => true;

    public void Interact(Character by)
    {
        EmitSignal(SignalName.Read, Text, by);
    }
}
