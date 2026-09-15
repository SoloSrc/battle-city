namespace BattleCity.Duel.Core.Effects;

/// <summary>Effect kinds (systems.md §5.4).</summary>
public enum EffectKind
{
    Ignition,
    Trigger,
    Quick,
    Continuous,
    Flip,
    Activation,
    Condition,

    /// <summary>An inherent Special Summon from the hand (Chaos Sorcerer): activated like an Ignition effect, its costs are paid, then the monster is Special Summoned without a chain link.</summary>
    SummonProcedure,
}
