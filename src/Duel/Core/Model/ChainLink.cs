using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Effects;

namespace BattleCity.Duel.Core.Model;

/// <summary>
/// One link of the chain: an activated effect waiting to resolve (systems.md
/// §5.2). Costs and targets are the answers to the effect's
/// <see cref="Choice"/>s, in the order the effect listed them.
/// </summary>
public sealed class ChainLink
{
    public ChainLink(int index, int player, CardInstance source, IEffect effect, ActivationContext? context = null)
    {
        Index = index;
        Player = player;
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Effect = effect ?? throw new ArgumentNullException(nameof(effect));
        Context = context ?? ActivationContext.None;
        Costs = new List<IReadOnlyList<Guid>>();
        Targets = new List<IReadOnlyList<Guid>>();
    }

    private ChainLink(ChainLink other, CardInstance source)
    {
        Index = other.Index;
        Player = other.Player;
        Source = source;
        Effect = other.Effect;
        Context = other.Context;
        Costs = other.Costs.Select(c => (IReadOnlyList<Guid>)c.ToList()).ToList();
        Targets = other.Targets.Select(t => (IReadOnlyList<Guid>)t.ToList()).ToList();
        CostsPaid = other.CostsPaid;
        Negated = other.Negated;
    }

    /// <summary>1-based chain link number.</summary>
    public int Index { get; }

    public int Player { get; }

    public CardInstance Source { get; }

    public IEffect Effect { get; }

    /// <summary>Why the effect activated: the trigger window and where the card came from, or <see cref="ActivationContext.None"/>.</summary>
    public ActivationContext Context { get; }

    /// <summary>Answers to <see cref="IEffect.Costs"/>, one list per choice.</summary>
    public List<IReadOnlyList<Guid>> Costs { get; }

    /// <summary>Answers to <see cref="IEffect.Targets"/>, one list per choice.</summary>
    public List<IReadOnlyList<Guid>> Targets { get; }

    /// <summary>Set once <see cref="IEffect.PayCosts"/> ran; costs are paid before targets are declared.</summary>
    public bool CostsPaid { get; set; }

    /// <summary>A negated link is skipped when the chain resolves; its card still goes where it would have gone.</summary>
    public bool Negated { get; set; }

    /// <summary>The first target of the first target choice, or null.</summary>
    public Guid? Target => Targets.Count > 0 && Targets[0].Count > 0 ? Targets[0][0] : null;

    /// <summary>Copies the link for a cloned state; <paramref name="source"/> is the clone of <see cref="Source"/>.</summary>
    public ChainLink Clone(CardInstance source) => new(this, source ?? throw new ArgumentNullException(nameof(source)));
}
