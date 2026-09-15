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

    /// <summary>The card battled a monster (fires after damage calculation for both monsters, wherever they ended up); <c>ActivationContext.Battled</c> is the other monster.</summary>
    OnBattle,

    /// <summary>The card inflicted battle damage to a player (fires after damage calculation); <c>ActivationContext.Battled</c> is the monster it fought, null for a direct attack.</summary>
    OnBattleDamage,

    /// <summary>A Spell Card was activated while the card was face-up on the field (Skilled Dark Magician's counters).</summary>
    SpellActivated,
}
