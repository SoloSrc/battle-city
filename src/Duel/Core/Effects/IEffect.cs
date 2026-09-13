using System.Collections.Generic;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects;

/// <summary>
/// A card effect keyed by id (systems.md §5.4). Tier 1 ships the interface
/// and Pot of Greed; targets, costs and modifiers arrive with tier 2+.
/// </summary>
public interface IEffect
{
    /// <summary>Registry id, also the value used in <c>data/cards/*.json</c> <c>effects</c>.</summary>
    string Id { get; }

    EffectKind Kind { get; }

    SpellSpeed Speed { get; }

    bool IsMandatory { get; }

    TriggerWindow? Trigger { get; }

    /// <summary>Whether the effect may be activated now by its controller (timing and activation conditions).</summary>
    bool CanActivate(DuelState state, CardInstance source);

    /// <summary>Costs to pay at activation; empty for tier 1.</summary>
    IReadOnlyList<Choice> Costs(DuelState state, CardInstance source);

    /// <summary>Targets declared at activation; empty for tier 1.</summary>
    IReadOnlyList<Choice> Targets(DuelState state, CardInstance source);

    /// <summary>Applies the effect when its chain link resolves.</summary>
    void Resolve(DuelEngine engine, ChainLink link);
}
