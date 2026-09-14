using System;
using BattleCity.Duel.Core.Effects;

namespace BattleCity.Duel.Core.Model;

/// <summary>A Trigger effect whose condition was met and that waits to be put on the chain (systems.md §5.2 <c>Triggers</c>); <see cref="Context"/> carries the window that fired and its details.</summary>
public sealed record PendingTrigger(int Player, Guid Card, string EffectId, ActivationContext Context, bool Mandatory)
{
    public TriggerWindow Window => Context.Trigger!.Value;
}
