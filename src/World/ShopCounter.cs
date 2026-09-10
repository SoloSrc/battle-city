using BattleCity.Characters;
using BattleCity.Core;
using Godot;

namespace BattleCity.World;

/// <summary>Shop counter: interacting raises <see cref="ShopRequested"/> for the shop UI (systems.md §3.3).</summary>
public partial class ShopCounter : Area3D, IInteractable
{
    [Signal]
    public delegate void ShopRequestedEventHandler(string shopId, Character player);

    [Export]
    public string ShopId { get; set; } = "card_shop";

    public string Prompt => "Shop";

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
        EmitSignal(SignalName.ShopRequested, ShopId, by);
    }
}
