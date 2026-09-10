using BattleCity.Characters;

namespace BattleCity.World;

/// <summary>
/// Anything the player can use with the <c>interact</c> action (systems.md §3.3).
/// Implementers are Area3D or PhysicsBody3D nodes on the <c>interactable</c> layer.
/// </summary>
public interface IInteractable
{
    /// <summary>Short verb phrase for the prompt, e.g. "Talk", "Read", "Enter".</summary>
    string Prompt { get; }

    /// <summary>Whether the prompt shows and <see cref="Interact"/> may run right now.</summary>
    bool CanInteract(Character by);

    void Interact(Character by);
}
