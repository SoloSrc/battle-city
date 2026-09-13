using System;
using System.Collections.Generic;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Rng;

namespace BattleCity.Duel.Core.Ai;

/// <summary>Picks a legal action uniformly at random; the fuzz duels of systems.md §5.7 use two of these.</summary>
public sealed class RandomAgent : IDuelAgent
{
    private readonly DuelRng _rng;

    public RandomAgent(ulong seed)
    {
        _rng = new DuelRng(seed);
    }

    public PlayerCommand Choose(DuelEngine engine, int player, IReadOnlyList<PlayerCommand> legal)
    {
        ArgumentNullException.ThrowIfNull(legal);
        return legal[_rng.Next(legal.Count)];
    }
}
