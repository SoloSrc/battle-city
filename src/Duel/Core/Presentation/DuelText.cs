using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Presentation;

/// <summary>
/// English for the HUD (GDD §3.4): event lines for the log, command labels,
/// phase names, what a response window is about, and the inspector lines of a
/// card. Names come from the state, never from card-specific code.
/// </summary>
public static class DuelText
{
    private static readonly string[] _phaseNames = { "Draw", "Standby", "Main 1", "Battle", "Main 2", "End" };

    public static string PhaseName(Phase phase) => _phaseNames[(int)phase];

    /// <summary>"You" for <paramref name="viewer"/>, "Opponent" for the other player.</summary>
    public static string PlayerName(int player, int viewer) => player == viewer ? "You" : "Opponent";

    public static string CardName(DuelState state, Guid card)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Find(card)?.Def.Name ?? "a card";
    }

    /// <summary>A card's name for <paramref name="viewer"/>: a face-down card of the other player is "a face-down card".</summary>
    public static string VisibleName(DuelState state, Guid card, int viewer)
    {
        ArgumentNullException.ThrowIfNull(state);
        CardInstance? instance = state.Find(card);
        if (instance is null)
        {
            return "a card";
        }

        bool hidden = instance.Controller != viewer && (instance.Loc == Location.Hand || (instance.IsOnField && instance.IsFaceDown) || instance.Loc is Location.Deck or Location.FusionDeck);
        return hidden ? "a face-down card" : instance.Def.Name;
    }

    /// <summary>The type line of the inspector: "Spellcaster / Effect · EARTH · Level 4" or "Quick-Play Spell".</summary>
    public static string TypeLine(CardDefinition def)
    {
        ArgumentNullException.ThrowIfNull(def);
        if (def.Monster is { } m)
        {
            string category = m.Category == MonsterCategory.Normal ? "Normal" : m.Category.ToString();
            return Inv($"{m.Type} / {category} · {m.Attribute.ToString().ToUpperInvariant()} · Level {m.Level}");
        }

        if (def.Spell is { } s)
        {
            return s.Subtype == SpellSubtype.Normal ? "Normal Spell" : s.Subtype == SpellSubtype.Quick ? "Quick-Play Spell" : $"{s.Subtype} Spell";
        }

        if (def.Trap is { } t)
        {
            return t.Subtype == TrapSubtype.Normal ? "Normal Trap" : $"{t.Subtype} Trap";
        }

        return def.Kind.ToString();
    }

    /// <summary>"ATK 1900 / DEF 900" with the current bonuses when the card is on the field; empty for Spells and Traps.</summary>
    public static string StatLine(CardInstance card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card.Def.Monster is not { } m)
        {
            return string.Empty;
        }

        if (card.IsOnField && (card.AtkBonus != 0 || card.DefBonus != 0))
        {
            return Inv($"ATK {card.Atk} ({m.Atk}{card.AtkBonus:+#;-#;+0}) / DEF {card.DefValue} ({m.Def}{card.DefBonus:+#;-#;+0})");
        }

        return Inv($"ATK {card.Atk} / DEF {card.DefValue}");
    }

    /// <summary>The label of a command for a menu or the log.</summary>
    public static string Describe(PlayerCommand command, DuelState state, int viewer)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(state);
        return command switch
        {
            Pass => "Pass",
            NormalSummon c => $"Summon {CardName(state, c.Card)}",
            SetMonster c => $"Set {CardName(state, c.Card)}",
            ChangePosition c => $"Change the position of {CardName(state, c.Card)}",
            FlipSummon c => $"Flip Summon {CardName(state, c.Card)}",
            ActivateSpell c => $"Activate {CardName(state, c.Card)}",
            ActivateTrap c => $"Activate {CardName(state, c.Card)}",
            ActivateEffect c => $"Activate {CardName(state, c.Card)}",
            AnswerChoice c => c.Selected.Count == 0 ? "Decline" : $"Choose {string.Join(", ", c.Selected.Select(id => CardName(state, id)))}",
            SetSpellTrap c => $"Set {CardName(state, c.Card)}",
            EnterBattlePhase => "Enter the Battle Phase",
            DeclareAttack c => c.Target is { } t ? $"Attack {VisibleName(state, t, viewer)} with {CardName(state, c.Attacker)}" : $"Attack directly with {CardName(state, c.Attacker)}",
            Discard c => $"Discard {CardName(state, c.Card)}",
            Surrender => "Surrender",
            _ => command.GetType().Name,
        };
    }

    /// <summary>What the player is being asked to respond to: the window and its card, or the last chain link.</summary>
    public static string ResponseText(DuelState state, int viewer)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Chain.Count > 0)
        {
            ChainLink link = state.Chain[^1];
            return $"{PlayerName(link.Player, viewer)} activated {link.Source.Def.Name} (chain link {link.Index + 1}). Respond?";
        }

        string who = PlayerName(state.TurnPlayer, viewer);
        return state.Window switch
        {
            Window.Summon when state.WindowCard is { } card => $"{who} Summoned {VisibleName(state, card, viewer)}. Respond?",
            Window.AttackDeclared when state.Attacker is { } attacker => state.AttackTarget is { } target
                ? $"{CardName(state, attacker)} attacks {VisibleName(state, target, viewer)}. Respond?"
                : $"{CardName(state, attacker)} attacks directly. Respond?",
            Window.DamageBeforeCalc => "Before damage calculation. Respond?",
            Window.DamageAfterCalc => "After damage calculation. Respond?",
            _ => $"{who}: {PhaseName(state.Phase)} Phase. Respond?",
        };
    }

    /// <summary>One log line per event; null for bookkeeping events the log skips.</summary>
    public static string? Describe(DuelEvent e, DuelState state, int viewer)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(state);
        return e switch
        {
            DuelStarted s => $"Duel start: {PlayerName(s.FirstPlayer, viewer)} go first",
            TurnStarted t => Inv($"Turn {t.Turn}: {PlayerName(t.Player, viewer)}"),
            PhaseChanged p => $"{PhaseName(p.Phase)} Phase",
            CardDrawn d => d.Player == viewer ? $"You drew {CardName(state, d.Card)}" : "Opponent drew a card",
            MonsterSummoned m => $"{PlayerName(m.Player, viewer)} Summoned {CardName(state, m.Card)}{Tributes(state, m.Tributes)}",
            MonsterSet m => $"{PlayerName(m.Player, viewer)} Set a monster{Tributes(state, m.Tributes)}",
            PositionChanged p => $"{CardName(state, p.Card)} changed to {PositionName(p.To)}",
            MonsterFlipSummoned f => $"{PlayerName(f.Player, viewer)} Flip Summoned {CardName(state, f.Card)}",
            MonsterFlipped f => $"{CardName(state, f.Card)} was flipped face-up",
            SpellTrapSet s => $"{PlayerName(s.Player, viewer)} Set a Spell or Trap",
            SpellActivated s => $"{PlayerName(s.Player, viewer)} activated {CardName(state, s.Card)}",
            TrapActivated t => $"{PlayerName(t.Player, viewer)} activated {CardName(state, t.Card)}",
            EffectActivated a => $"{PlayerName(a.Player, viewer)} activated the effect of {CardName(state, a.Card)}",
            ChoiceRequested c => c.Player == viewer ? c.Prompt : null,
            ChoiceAnswered c => c.Player == viewer ? null : c.Selected.Count == 0 ? "Opponent declined" : $"Opponent chose {string.Join(", ", c.Selected.Select(id => VisibleName(state, id, viewer)))}",
            ChainLinkNegated n => $"{CardName(state, n.Card)} was negated",
            ChainLinkResolved r => $"{CardName(state, r.Card)} resolved",
            BattlePhaseEntered b => $"{PlayerName(b.Player, viewer)} entered the Battle Phase",
            AttackDeclared a => a.Target is { } t ? $"{CardName(state, a.Attacker)} attacks {VisibleName(state, t, viewer)}" : $"{CardName(state, a.Attacker)} attacks directly",
            AttackCancelled a => $"{CardName(state, a.Attacker)}'s attack ended: no battle took place",
            BattleFought f => f.Target is { } t
                ? Inv($"{CardName(state, f.Attacker)} (ATK {f.AttackerAtk}) attacked {VisibleName(state, t, viewer)} ({(f.TargetInDefense ? "DEF" : "ATK")} {f.TargetValue})")
                : Inv($"{CardName(state, f.Attacker)} (ATK {f.AttackerAtk}) attacked directly"),
            BattleDamage d => Inv($"{PlayerName(d.Player, viewer)} took {d.Amount} battle damage"),
            NoBattleDamage => "No battle damage",
            EffectDamage d => Inv($"{PlayerName(d.Player, viewer)} took {d.Amount} damage"),
            LifePointsPaid p => Inv($"{PlayerName(p.Player, viewer)} paid {p.Amount} Life Points"),
            LifePointsGained g => Inv($"{PlayerName(g.Player, viewer)} gained {g.Amount} Life Points"),
            MonsterDestroyed d => d.Reason switch
            {
                DestroyReason.Battle => $"{CardName(state, d.Card)} was destroyed by battle",
                DestroyReason.Effect => $"{CardName(state, d.Card)} was destroyed by an effect",
                _ => $"{CardName(state, d.Card)} was destroyed",
            },
            SpellTrapDestroyed d => $"{CardName(state, d.Card)} was destroyed",
            MonsterSpecialSummoned s => $"{PlayerName(s.Player, viewer)} Special Summoned {CardName(state, s.Card)}",
            TokenCreated t => $"{PlayerName(t.Player, viewer)} got a {CardName(state, t.Card)}",
            TokenRemoved t => $"A {t.CardId.Replace('_', ' ')} left the field",
            CardBanished b => $"{CardName(state, b.Card)} was banished",
            CardReturnedToHand r => $"{CardName(state, r.Card)} returned to the hand",
            CardReturnedToDeck r => $"{CardName(state, r.Card)} returned to the Deck",
            DeckShuffled s => $"{PlayerName(s.Player, viewer)} shuffled the Deck",
            ControlChanged c => $"{PlayerName(c.To, viewer)} took control of {CardName(state, c.Card)}",
            CardEquipped q => $"{CardName(state, q.Equip)} was equipped to {CardName(state, q.Target)}",
            MonsterAbsorbed a => $"{CardName(state, a.Card)} was absorbed by {CardName(state, a.Target)}",
            CardsRevealed r => $"{PlayerName(r.Player, viewer)} revealed {string.Join(", ", r.Cards.Select(id => CardName(state, id)))}",
            MonsterFlippedFaceDown f => $"{CardName(state, f.Card)} was flipped face-down",
            SpiritReturned s => $"{CardName(state, s.Card)} returned to the hand",
            CounterChanged c => Inv($"{CardName(state, c.Card)}: {c.Count} {c.Counter} counter(s)"),
            CardSentToGraveyard g => g.From == Location.Hand || g.From == Location.Deck ? $"{CardName(state, g.Card)} was sent to the Graveyard" : null,
            CardDiscarded d => $"{PlayerName(d.Player, viewer)} discarded {CardName(state, d.Card)}",
            DuelEnded ended => ended.Winner is { } w ? $"{PlayerName(w, viewer)} won ({ended.Outcome})" : $"Draw ({ended.Outcome})",
            _ => null,
        };
    }

    public static string PositionName(Position position) =>
        position switch
        {
            Position.FaceUpAttack => "Attack Position",
            Position.FaceUpDefense => "Defense Position",
            Position.FaceDownDefense => "face-down Defense Position",
            Position.FaceDown => "face-down",
            _ => "face-up",
        };

    private static string Tributes(DuelState state, IReadOnlyList<Guid> tributes) =>
        tributes.Count == 0 ? string.Empty : $" tributing {string.Join(" and ", tributes.Select(id => CardName(state, id)))}";

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
