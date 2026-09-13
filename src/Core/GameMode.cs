namespace BattleCity.Core;

/// <summary>Top-level state of <see cref="Game"/> (systems.md §2.2). Only Overworld and Interior allow movement.</summary>
public enum GameMode
{
    Boot,
    Overworld,
    Interior,
    Duel,
}
