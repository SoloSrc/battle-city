using System.Collections.Generic;
using BattleCity.Duel.Core.Commands;

namespace BattleCity.Duel.Core.Ai;

/// <summary>Something that answers for a player when they have priority (systems.md §7).</summary>
public interface IDuelAgent
{
    /// <summary>Picks one of <paramref name="legal"/> (never empty) for <paramref name="player"/>.</summary>
    PlayerCommand Choose(DuelEngine engine, int player, IReadOnlyList<PlayerCommand> legal);
}
