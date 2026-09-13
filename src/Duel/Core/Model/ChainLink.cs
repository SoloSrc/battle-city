using System;
using BattleCity.Duel.Core.Effects;

namespace BattleCity.Duel.Core.Model;

/// <summary>One link of the chain: an activated effect waiting to resolve (systems.md §5.2).</summary>
public sealed class ChainLink
{
    public ChainLink(int index, int player, CardInstance source, IEffect effect)
    {
        Index = index;
        Player = player;
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Effect = effect ?? throw new ArgumentNullException(nameof(effect));
    }

    /// <summary>1-based chain link number.</summary>
    public int Index { get; }

    public int Player { get; }

    public CardInstance Source { get; }

    public IEffect Effect { get; }
}
