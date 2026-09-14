using System;

namespace BattleCity.Duel.Core.Model;

/// <summary>What the active modifiers forbid or grant a card; recomputed by the engine, read by the rules.</summary>
[Flags]
public enum Restriction
{
    None = 0,
    CannotAttack = 1 << 0,
    CannotBeAttacked = 1 << 1,
    CannotChangePosition = 1 << 2,
    CannotBeDestroyedByBattle = 1 << 3,
    CannotBeTributed = 1 << 4,
    Piercing = 1 << 5,
    CanAttackDirectly = 1 << 6,
    EffectsNegated = 1 << 7,

    /// <summary>Must attack when able: its controller cannot pass over an attack it could make (Berserk Gorilla).</summary>
    MustAttack = 1 << 8,

    /// <summary>Destroyed when its battle position is changed to Defense Position (Berserk Gorilla).</summary>
    DestroyedInDefensePosition = 1 << 9,

    /// <summary>Switched to Defense Position at the end of a Battle Phase in which it attacked, then locked until the end of its controller's next turn (Goblin Attack Force, Giant Orc).</summary>
    DefenseAfterAttack = 1 << 10,
}

/// <summary>What the active modifiers impose on a player; recomputed by the engine, read by the rules.</summary>
[Flags]
public enum PlayerRestriction
{
    None = 0,
    NoBattleDamage = 1 << 0,
    TrapsNegated = 1 << 1,
}
