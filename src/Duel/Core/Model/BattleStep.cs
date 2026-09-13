namespace BattleCity.Duel.Core.Model;

/// <summary>Steps of the Battle Phase (GDD §3.2); <see cref="None"/> outside it.</summary>
public enum BattleStep
{
    None,
    Start,
    Battle,
    Damage,
    End,
}
