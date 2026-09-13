using System;
using System.Collections.Generic;

namespace BattleCity.Duel.Core.Effects;

/// <summary>
/// Maps effect ids from card data to <see cref="IEffect"/> factories
/// (systems.md §5.3). The card loader refuses ids that are not registered,
/// so an unimplemented tier 2+ card fails at load time rather than mid-duel.
/// </summary>
public sealed class EffectRegistry
{
    private readonly Dictionary<string, Func<IEffect>> _factories = new(StringComparer.Ordinal);

    /// <summary>Every effect implemented so far (tier 1: Pot of Greed).</summary>
    public static EffectRegistry CreateDefault()
    {
        var registry = new EffectRegistry();
        registry.Register(PotOfGreedEffect.EffectId, static () => new PotOfGreedEffect());
        return registry;
    }

    public IEnumerable<string> Ids => _factories.Keys;

    public void Register(string id, Func<IEffect> factory)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(factory);
        _factories[id] = factory;
    }

    public bool Contains(string id) => _factories.ContainsKey(id);

    public IEffect Create(string id)
    {
        if (!_factories.TryGetValue(id, out Func<IEffect>? factory))
        {
            throw new KeyNotFoundException($"No effect registered for id '{id}'.");
        }

        return factory();
    }
}
