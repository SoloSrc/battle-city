namespace BattleCity.Duel.Core.Model;

/// <summary>Why a duel ended (GDD §3.1 win conditions).</summary>
public enum DuelOutcome
{
    /// <summary>Still in progress.</summary>
    None,

    /// <summary>A player's life points reached 0.</summary>
    LifePoints,

    /// <summary>A player had to draw from an empty deck.</summary>
    DeckOut,

    /// <summary>A player surrendered.</summary>
    Surrender,

    /// <summary>Both players lost at the same time.</summary>
    Draw,
}
