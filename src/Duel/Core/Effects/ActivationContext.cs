using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects;

/// <summary>
/// Why an effect is being considered (systems.md §5.4): for a Trigger effect
/// the window that fired, for graveyard triggers where the card came from
/// and for summon triggers how the monster was Summoned. Manual activations
/// use <see cref="None"/>; the timing they need is read from
/// <see cref="DuelState.Window"/> and the chain.
/// </summary>
public sealed record ActivationContext(TriggerWindow? Trigger = null, Location? From = null, SummonKind? Summon = null)
{
    public static readonly ActivationContext None = new();
}
