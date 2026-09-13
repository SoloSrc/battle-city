namespace BattleCity.Duel.Core.Model;

/// <summary>
/// The response window the duel is in (systems.md §5.5). Two consecutive
/// passes on an empty chain close the window: <see cref="Open"/> advances
/// the phase or step, <see cref="Summon"/> returns to the open Main Phase,
/// the attack windows move the Damage Step along.
/// </summary>
public enum Window
{
    /// <summary>No event is waiting for responses; the turn player acts freely.</summary>
    Open,

    /// <summary>A monster was just Summoned (<see cref="DuelState.WindowCard"/>); the turn player holds ignition priority, then the opponent may respond.</summary>
    Summon,

    /// <summary>An attack was declared (<see cref="DuelState.Attacker"/>); both players may respond before the Damage Step.</summary>
    AttackDeclared,

    /// <summary>Damage Step before damage calculation: Counter Traps and ATK/DEF modifiers only.</summary>
    DamageBeforeCalc,

    /// <summary>Damage Step after damage calculation: flip effects and battle triggers, and responses chained to them.</summary>
    DamageAfterCalc,
}
