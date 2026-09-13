namespace BattleCity.Duel.Core.Effects;

/// <summary>When a Trigger effect may activate (systems.md §5.4). The engine queues a <c>PendingTrigger</c> for every effect whose window fires.</summary>
public enum TriggerWindow
{
    /// <summary>The card was Normal, Flip or Special Summoned.</summary>
    OnSummon,

    /// <summary>The card was destroyed by battle (fires after damage calculation).</summary>
    OnDestroyedByBattle,

    /// <summary>The card was flipped face-up by a Flip Summon or by battle (fires after damage calculation).</summary>
    OnFlip,

    /// <summary>The card was sent to the Graveyard from anywhere; <c>ActivationContext.From</c> says where from.</summary>
    OnSentToGrave,

    /// <summary>The Standby Phase began with the card face-up on the field.</summary>
    Standby,

    /// <summary>The End Phase began with the card face-up on the field.</summary>
    EndPhase,
}
