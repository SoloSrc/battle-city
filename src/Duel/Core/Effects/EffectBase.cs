using System;
using System.Collections.Generic;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects;

/// <summary>
/// Defaults for <see cref="IEffect"/>: no costs, no targets, no trigger,
/// no modifiers, always activatable. An effect overrides only what it uses.
/// </summary>
public abstract class EffectBase : IEffect
{
    public abstract string Id { get; }

    public abstract EffectKind Kind { get; }

    public abstract SpellSpeed Speed { get; }

    public virtual bool IsMandatory => false;

    public virtual TriggerWindow? Trigger => null;

    public virtual SummonLimit Limits => SummonLimit.None;

    public virtual bool RemainsOnField => false;

    public virtual bool UsableInDamageStep => false;

    public virtual bool OncePerTurn => false;

    public virtual bool FiresIn(TriggerWindow window) => Trigger == window;

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

    /// <summary>Nothing by default, for Continuous effects that only contribute <see cref="Modifiers"/>.</summary>
    public virtual void Resolve(DuelEngine engine, ChainLink link)
    {
    }

    public virtual IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source) => Array.Empty<Modifier>();

    public virtual void OnLeftField(DuelEngine engine, CardInstance source, Location from, Guid? equippedTo)
    {
    }
}
