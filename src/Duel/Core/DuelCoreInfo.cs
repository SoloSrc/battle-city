namespace BattleCity.Duel.Core;

/// <summary>
/// Static facts about the duel engine. Kept Godot-free like the rest of Duel.Core
/// (architecture.md §3). The engine itself lands with issue #25.
/// </summary>
public static class DuelCoreInfo
{
    /// <summary>The card pool and rules era the engine implements.</summary>
    public const string Ruleset = "Goat Format";

    /// <summary>Starting life points per duelist.</summary>
    public const int StartingLifePoints = 8000;

    /// <summary>Cards drawn into the opening hand.</summary>
    public const int OpeningHandSize = 5;
}
