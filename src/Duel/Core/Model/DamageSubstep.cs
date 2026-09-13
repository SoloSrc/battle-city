namespace BattleCity.Duel.Core.Model;

/// <summary>Damage Step sub-steps (systems.md §5.5); <see cref="None"/> outside the Damage Step.</summary>
public enum DamageSubstep
{
    None,
    StartDamage,
    BeforeCalc,
    Calc,
    AfterCalc,
    EndDamage,
}
