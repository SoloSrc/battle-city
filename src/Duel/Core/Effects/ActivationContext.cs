using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects;

/// <summary>
/// Why an effect is being considered (systems.md §5.4): for a Trigger effect
/// the window that fired and, for graveyard triggers, where the card came
/// from. Manual activations use <see cref="None"/>; the timing they need is
/// read from <see cref="DuelState.Window"/> and the chain.
/// </summary>
public sealed record ActivationContext(TriggerWindow? Trigger = null, Location? From = null)
{
    public static readonly ActivationContext None = new();
}
