namespace BattleCity.Duel.Core.Model;

/// <summary>Turn phases (GDD §3.2).</summary>
public enum Phase
{
    Draw,
    Standby,
    Main1,
    Battle,
    Main2,
    End,
}
