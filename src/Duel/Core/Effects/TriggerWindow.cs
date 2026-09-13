namespace BattleCity.Duel.Core.Effects;

/// <summary>When a Trigger effect may activate (systems.md §5.4). Tier 2 effects consume these windows.</summary>
public enum TriggerWindow
{
    OnSummon,
    OnDestroyedByBattle,
    OnFlip,
    OnSentToGrave,
    Standby,
    EndPhase,
}
