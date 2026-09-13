using System;
using BattleCity.Duel.Core.Effects;

namespace BattleCity.Duel.Core.Model;

/// <summary>A Trigger effect whose condition was met and that waits to be put on the chain (systems.md §5.2 <c>Triggers</c>).</summary>
public sealed record PendingTrigger(int Player, Guid Card, string EffectId, TriggerWindow Window, Location? From, bool Mandatory);
