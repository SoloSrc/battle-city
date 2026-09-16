namespace BattleCity.Duel.Core;

/// <summary>Per-duel settings; the defaults are the Goat Format match rules (GDD §3.1).</summary>
public sealed record DuelOptions
{
    public ulong Seed { get; init; } = 1;

    /// <summary>Who takes turn 1; null flips the coin.</summary>
    public int? FirstPlayer { get; init; }

    /// <summary>Shuffle both decks at the start. Tests turn it off to script hands: the last card of a deck list is the top card.</summary>
    public bool Shuffle { get; init; } = true;

    /// <summary>When a player's only legal action is <c>Pass</c>, the engine passes for them.</summary>
    public bool AutoPass { get; init; } = true;

    public int OpeningHandSize { get; init; } = DuelCoreInfo.OpeningHandSize;

    public int StartingLifePoints { get; init; } = DuelCoreInfo.StartingLifePoints;

    public int MinDeckSize { get; init; } = 40;

    public int MaxDeckSize { get; init; } = 60;

    public int HandLimit { get; init; } = 6;
}
