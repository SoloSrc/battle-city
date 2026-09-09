using BattleCity.Characters;
using BattleCity.World;
using Godot;

namespace BattleCity.Diagnostics;

/// <summary>Minimal interactable for diagnostic scenes: counts uses and prints them.</summary>
public partial class TestInteractable : Area3D, IInteractable
{
    [Export]
    public string Prompt { get; set; } = "Test";

    public int UseCount { get; private set; }

    public bool CanInteract(Character by) => true;

    public void Interact(Character by)
    {
        UseCount++;
        GD.Print($"TestInteractable '{Name}' used by {by.Name} ({UseCount})");
    }
}
