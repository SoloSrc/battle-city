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

    /// <summary>The Flip Effects of monsters it destroys by battle do not fire (Blade Knight while it is its controller's only monster).</summary>
    NegatesFlipEffectsOfDestroyed = 1 << 11,

    /// <summary>The effects of monsters it destroys by battle are negated: no flip, battle-destruction or graveyard trigger fires for them (Dark Balter the Terrible).</summary>
    NegatesEffectsOfDestroyed = 1 << 12,

    /// <summary>A face-down Defense Position monster it attacks is destroyed at the start of the Damage Step without being flipped (Mystic Swordsman LV2).</summary>
    DestroysFaceDownTargets = 1 << 13,

    /// <summary>May attack every monster the opponent controls once each in the same Battle Phase (Asura Priest).</summary>
    AttacksEveryMonster = 1 << 14,

    /// <summary>Destroyed when it becomes the target of a card effect (Reaper on the Nightmare).</summary>
    DestroyedWhenTargeted = 1 << 15,
}

/// <summary>What the active modifiers impose on a player; recomputed by the engine, read by the rules.</summary>
[Flags]
public enum PlayerRestriction
{
    None = 0,
    NoBattleDamage = 1 << 0,
    TrapsNegated = 1 << 1,

    /// <summary>Cannot banish cards from either Graveyard (the opponent of Kycoo the Ghost Destroyer).</summary>
    CannotBanishFromGraveyard = 1 << 2,

    /// <summary>Cannot Normal, Flip or Special Summon this turn; Sets are still allowed (Scapegoat).</summary>
    CannotSummon = 1 << 3,
}
