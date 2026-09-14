using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Effects;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Rules;

/// <summary>
/// The modifier registry (systems.md §5.4). <see cref="Recompute"/> projects
/// the timed modifiers on <see cref="DuelState.Modifiers"/> plus what every
/// face-up card's effects contribute right now onto the computed fields the
/// rules read: <see cref="CardInstance.AtkBonus"/>, <see cref="CardInstance.Restrictions"/>
/// and <see cref="PlayerState.Restrictions"/>. The engine runs it after
/// every state change, so the computed fields are never stale.
/// </summary>
internal static class Modifiers
{
    public static void Recompute(DuelEngine engine)
    {
        DuelState s = engine.State;
        var field = s.Players.SelectMany(FieldCards).ToList();
        foreach (CardInstance card in field)
        {
            card.AtkBonus = 0;
            card.DefBonus = 0;
            card.Restrictions = Restriction.None;
        }

        foreach (PlayerState p in s.Players)
        {
            p.Restrictions = PlayerRestriction.None;
        }

        // Timed modifiers first, so an effect-negation among them silences the contributions below.
        foreach (Modifier m in s.Modifiers)
        {
            Apply(s, field, m);
        }

        // Monsters and Spells contribute before Traps: Jinzo's negation must be known before a Continuous Trap is read.
        foreach (CardInstance card in field.Where(c => !c.Def.IsTrap))
        {
            Contribute(engine, s, field, card);
        }

        foreach (CardInstance card in field.Where(c => c.Def.IsTrap))
        {
            if (!s.Player(card.Controller).Has(PlayerRestriction.TrapsNegated))
            {
                Contribute(engine, s, field, card);
            }
        }
    }

    /// <summary>Drops every modifier on <paramref name="card"/>: it left the field or was flipped face-down.</summary>
    public static void RemoveFor(DuelEngine engine, CardInstance card)
    {
        var gone = engine.State.Modifiers.Where(m => m.Card == card.Id).ToList();
        foreach (Modifier m in gone)
        {
            engine.State.Modifiers.Remove(m);
            engine.Emit(new Events.ModifierRemoved(m));
        }
    }

    /// <summary>Drops the modifiers that ran out with the current turn (End Phase).</summary>
    public static void Expire(DuelEngine engine)
    {
        int turn = engine.State.TurnNumber;
        var gone = engine.State.Modifiers.Where(m => m.ExpiresAfterTurn is { } t && t <= turn).ToList();
        foreach (Modifier m in gone)
        {
            engine.State.Modifiers.Remove(m);
            engine.Emit(new Events.ModifierRemoved(m));
        }
    }

    private static IEnumerable<CardInstance> FieldCards(PlayerState p) =>
        p.Monsters.Concat(p.SpellTraps).Concat(p.FieldZone is null ? Enumerable.Empty<CardInstance>() : new[] { p.FieldZone });

    private static void Contribute(DuelEngine engine, DuelState s, List<CardInstance> field, CardInstance card)
    {
        if (!card.IsFaceUp || card.Has(Restriction.EffectsNegated))
        {
            return;
        }

        foreach (IEffect effect in engine.EffectsOf(card))
        {
            foreach (Modifier m in effect.Modifiers(s, card))
            {
                Apply(s, field, m);
            }
        }
    }

    private static void Apply(DuelState s, List<CardInstance> field, Modifier m)
    {
        if (m.IsCardKind)
        {
            if (m.Card is { } id)
            {
                CardInstance? card = field.Find(c => c.Id == id);
                if (card is not null)
                {
                    ApplyToCard(card, m);
                }

                return;
            }

            foreach (CardInstance card in field.Where(c => c.IsMonster && (m.Player is null || c.Controller == m.Player)))
            {
                ApplyToCard(card, m);
            }

            return;
        }

        foreach (PlayerState p in s.Players.Where(p => m.Player is null || p.Index == m.Player))
        {
            p.Restrictions |= m.Kind switch
            {
                ModifierKind.NoBattleDamage => PlayerRestriction.NoBattleDamage,
                ModifierKind.TrapsNegated => PlayerRestriction.TrapsNegated,
                _ => PlayerRestriction.None,
            };
        }
    }

    private static void ApplyToCard(CardInstance card, Modifier m)
    {
        switch (m.Kind)
        {
            case ModifierKind.Atk:
                card.AtkBonus += m.Value;
                break;
            case ModifierKind.Def:
                card.DefBonus += m.Value;
                break;
            default:
                card.Restrictions |= m.Kind switch
                {
                    ModifierKind.CannotAttack => Restriction.CannotAttack,
                    ModifierKind.CannotBeAttacked => Restriction.CannotBeAttacked,
                    ModifierKind.CannotChangePosition => Restriction.CannotChangePosition,
                    ModifierKind.CannotBeDestroyedByBattle => Restriction.CannotBeDestroyedByBattle,
                    ModifierKind.CannotBeTributed => Restriction.CannotBeTributed,
                    ModifierKind.Piercing => Restriction.Piercing,
                    ModifierKind.CanAttackDirectly => Restriction.CanAttackDirectly,
                    ModifierKind.EffectsNegated => Restriction.EffectsNegated,
                    _ => Restriction.None,
                };
                break;
        }
    }
}
