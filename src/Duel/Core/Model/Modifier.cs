using System;

namespace BattleCity.Duel.Core.Model;

/// <summary>
/// One continuous change to the duel (systems.md §5.4). A modifier applies
/// to one card (<see cref="Card"/>), to everything a player controls or the
/// player themself (<see cref="Player"/>), or to the whole duel (neither).
/// Effects that resolve register timed modifiers on
/// <see cref="DuelState.Modifiers"/>; Continuous effects and equips
/// contribute theirs anew every time <c>Modifiers.Recompute</c> runs, so
/// nothing stored ever points at a card that left the field.
/// </summary>
public sealed record Modifier(ModifierKind Kind, int Value, Guid Source, Guid? Card = null, int? Player = null, int? ExpiresAfterTurn = null)
{
    /// <summary>A modifier on <paramref name="card"/>; <paramref name="expiresAfterTurn"/> null means for as long as the card stays on the field.</summary>
    public static Modifier OnCard(ModifierKind kind, Guid source, Guid card, int value = 0, int? expiresAfterTurn = null) =>
        new(kind, value, source, card, null, expiresAfterTurn);

    /// <summary>A modifier on <paramref name="player"/>: on the player for player kinds, on every monster they control for card kinds.</summary>
    public static Modifier OnPlayer(ModifierKind kind, Guid source, int player, int value = 0, int? expiresAfterTurn = null) =>
        new(kind, value, source, null, player, expiresAfterTurn);

    /// <summary>A modifier on both players.</summary>
    public static Modifier Global(ModifierKind kind, Guid source, int value = 0, int? expiresAfterTurn = null) =>
        new(kind, value, source, null, null, expiresAfterTurn);

    /// <summary>Card kinds project onto <see cref="CardInstance"/>; the rest onto <see cref="PlayerState"/>.</summary>
    public bool IsCardKind => Kind is ModifierKind.Atk or ModifierKind.Def
        or ModifierKind.CannotAttack or ModifierKind.CannotBeAttacked or ModifierKind.CannotChangePosition
        or ModifierKind.CannotBeDestroyedByBattle or ModifierKind.CannotBeTributed
        or ModifierKind.Piercing or ModifierKind.CanAttackDirectly or ModifierKind.EffectsNegated;
}

/// <summary>What a <see cref="Modifier"/> changes.</summary>
public enum ModifierKind
{
    /// <summary>ATK change by <see cref="Modifier.Value"/> (card kind).</summary>
    Atk,

    /// <summary>DEF change by <see cref="Modifier.Value"/> (card kind).</summary>
    Def,

    CannotAttack,

    CannotBeAttacked,

    CannotChangePosition,

    CannotBeDestroyedByBattle,

    /// <summary>Cannot be tributed for a Tribute Summon (tokens from Scapegoat).</summary>
    CannotBeTributed,

    /// <summary>Inflicts piercing battle damage when attacking a Defense Position monster.</summary>
    Piercing,

    /// <summary>May attack directly even when the opponent controls monsters.</summary>
    CanAttackDirectly,

    /// <summary>The card's effects are negated: it contributes no modifiers and its links are skipped.</summary>
    EffectsNegated,

    /// <summary>The player takes no battle damage (player kind).</summary>
    NoBattleDamage,

    /// <summary>The player cannot activate Trap Cards and their Traps' effects are negated (player kind).</summary>
    TrapsNegated,
}
