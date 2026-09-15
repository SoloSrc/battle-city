using System;

namespace BattleCity.Duel.Core.Effects;

/// <summary>Ways a monster may not arrive on the field, declared by its effects (systems.md §5.4).</summary>
[Flags]
public enum SummonLimit
{
    None = 0,

    /// <summary>Cannot be Normal Summoned (Chaos Sorcerer).</summary>
    CannotBeNormalSummoned = 1 << 0,

    /// <summary>Cannot be Set (Mystic Swordsman LV2, Chaos Sorcerer).</summary>
    CannotBeSet = 1 << 1,
}
