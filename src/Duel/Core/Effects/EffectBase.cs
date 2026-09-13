using System;
using System.Collections.Generic;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects;

/// <summary>
/// Defaults for <see cref="IEffect"/>: no costs, no targets, no trigger,
/// always activatable. An effect overrides only what it uses.
/// </summary>
public abstract class EffectBase : IEffect
{
    public abstract string Id { get; }

    public abstract EffectKind Kind { get; }

    public abstract SpellSpeed Speed { get; }

    public virtual bool IsMandatory => false;

    public virtual TriggerWindow? Trigger => null;

    public virtual bool UsableInDamageStep => false;

    public virtual bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return true;
    }

    public virtual IReadOnlyList<Choice> Costs(DuelState state, CardInstance source) => Array.Empty<Choice>();

    public virtual IReadOnlyList<Choice> Targets(DuelState state, CardInstance source) => Array.Empty<Choice>();

    public virtual void PayCosts(DuelEngine engine, ChainLink link)
    {
    }

    public abstract void Resolve(DuelEngine engine, ChainLink link);
}
