using System.Collections.Generic;

namespace BattleCity.Data;

/// <summary>
/// One autosave (systems.md §9, GDD §7): where the player is, what they own and
/// what they did. <c>Defeated</c> holds the duelist ids whose <c>defeated:</c>
/// flag is set; <c>Flags</c> the rest. <c>RngDraws</c> is how far the game RNG
/// advanced from <c>Seed</c>, so booster drops and duel seeds resume in order.
/// </summary>
public sealed record SaveData(
    ulong Seed,
    ulong RngDraws,
    string Name,
    IReadOnlyDictionary<string, string> Appearance,
    string Level,
    string Spawn,
    int Coins,
    IReadOnlyDictionary<string, int> Owned,
    IReadOnlyList<string> Deck,
    IReadOnlyList<string> FusionDeck,
    IReadOnlyList<string> Defeated,
    IReadOnlyList<string> Flags);
