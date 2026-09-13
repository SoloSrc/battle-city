namespace BattleCity.Duel.Core.Model;

/// <summary>Battle position of a monster, or face state of a Spell/Trap (systems.md §5.2).</summary>
public enum Position
{
    /// <summary>Monster, face-up attack position.</summary>
    FaceUpAttack,

    /// <summary>Monster, face-up defense position.</summary>
    FaceUpDefense,

    /// <summary>Monster, face-down defense position (a Set monster).</summary>
    FaceDownDefense,

    /// <summary>Spell/Trap Set face-down, or any card outside the field.</summary>
    FaceDown,

    /// <summary>Spell/Trap face-up on the field.</summary>
    FaceUp,
}
