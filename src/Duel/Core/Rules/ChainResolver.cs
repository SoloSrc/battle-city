using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Effects;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Rules;

/// <summary>
/// Activation timing, the spell speed rule and chain resolution (systems.md
/// §5.5). Speed 1 (Normal Spells, Ignition effects) needs the turn player's
/// open Main Phase; speed 2 and 3 respond whenever their player holds
/// priority, within the Damage Step restrictions and <see cref="CanChain"/>.
/// </summary>
internal static class ChainResolver
{
    /// <summary>Spell speed rule: a link needs at least the previous link's speed, so only speed 3 answers a Counter Trap.</summary>
    public static bool CanChain(DuelState s, IEffect effect)
    {
        if (s.Chain.Count == 0)
        {
            return true;
        }

        return effect.Speed >= s.Chain[^1].Effect.Speed;
    }

    public static string? ValidateActivateSpell(DuelEngine engine, int player, Guid cardId)
    {
        DuelState s = engine.State;
        CardInstance? card = Zones.InHand(s, player, cardId) ?? Zones.OnSpellTrapZone(s, player, cardId);
        if (card is null)
        {
            return "the card is not in your hand or Set on your field";
        }

        if (card.Def.Spell is null)
        {
            return $"{card.Def.Name} is not a Spell";
        }

        if (card.Def.Spell.Subtype == SpellSubtype.Ritual)
        {
            return "Ritual Spells are not in the card pool";
        }

        if (card.Loc == Location.SpellTrapZone && card.IsFaceUp)
        {
            return $"{card.Def.Name} is already active";
        }

        if (card.Loc == Location.Hand && card.Def.Spell.Subtype != SpellSubtype.Field && s.Player(player).SpellTrapCount >= PlayerState.ZoneCount)
        {
            return "no free Spell & Trap Zone";
        }

        IEffect? activation = engine.EffectsOf(card).FirstOrDefault(e => e.Kind == EffectKind.Activation);
        if (activation is null)
        {
            return $"{card.Def.Name} has no activation effect";
        }

        return ValidateTiming(s, player, card, activation);
    }

    public static void ActivateSpell(DuelEngine engine, int player, Guid cardId)
    {
        DuelState s = engine.State;
        CardInstance card = Zones.InHand(s, player, cardId) ?? Zones.OnSpellTrapZone(s, player, cardId)!;
        int zone;
        if (card.Def.Spell!.Subtype == SpellSubtype.Field)
        {
            // A new Field Spell replaces the one in play.
            if (s.Player(player).FieldZone is { } previous)
            {
                engine.Destroy(previous, DestroyReason.Effect);
            }

            Zones.PlaceField(engine, card, player);
            zone = 0;
        }
        else
        {
            zone = card.Loc == Location.SpellTrapZone ? card.ZoneIndex : s.Player(player).FirstFreeSpellTrapZone();
            Zones.PlaceSpellTrap(engine, card, player, zone, Position.FaceUp);
        }

        engine.Emit(new SpellActivated(player, card.Id, card.Def.Id, zone));
        engine.BeginActivation(player, card, engine.EffectsOf(card).First(e => e.Kind == EffectKind.Activation), ActivationContext.None);
    }

    public static string? ValidateActivateTrap(DuelEngine engine, int player, Guid cardId)
    {
        DuelState s = engine.State;
        CardInstance? card = Zones.OnSpellTrapZone(s, player, cardId);
        if (card is null)
        {
            return "the card is not Set on your field";
        }

        if (card.Def.Trap is null)
        {
            return $"{card.Def.Name} is not a Trap";
        }

        if (card.IsFaceUp)
        {
            return $"{card.Def.Name} is already active";
        }

        if (s.Player(player).Has(PlayerRestriction.TrapsNegated))
        {
            return "Trap Cards cannot be activated";
        }

        IEffect? activation = engine.EffectsOf(card).FirstOrDefault(e => e.Kind == EffectKind.Activation);
        if (activation is null)
        {
            return $"{card.Def.Name} has no activation effect";
        }

        return ValidateTiming(s, player, card, activation);
    }

    public static void ActivateTrap(DuelEngine engine, int player, Guid cardId)
    {
        CardInstance card = Zones.OnSpellTrapZone(engine.State, player, cardId)!;
        card.Pos = Position.FaceUp;
        engine.Emit(new TrapActivated(player, card.Id, card.Def.Id, card.ZoneIndex));
        engine.Refresh();
        engine.BeginActivation(player, card, engine.EffectsOf(card).First(e => e.Kind == EffectKind.Activation), ActivationContext.None);
    }

    public static string? ValidateActivateEffect(DuelEngine engine, int player, Guid cardId, int effectIndex)
    {
        DuelState s = engine.State;
        CardInstance? card = Zones.OnField(s, player, cardId);
        if (card is null)
        {
            return "the monster is not on your field";
        }

        if (card.IsFaceDown)
        {
            return "a face-down monster's effects cannot be activated";
        }

        IReadOnlyList<IEffect> effects = engine.EffectsOf(card);
        if (effectIndex < 0 || effectIndex >= effects.Count)
        {
            return $"{card.Def.Name} has no effect {effectIndex}";
        }

        IEffect effect = effects[effectIndex];
        if (effect.Kind is not (EffectKind.Ignition or EffectKind.Quick))
        {
            return $"{card.Def.Name}'s {effect.Kind} effect is not activated by hand";
        }

        if (card.Has(Restriction.EffectsNegated))
        {
            return $"{card.Def.Name}'s effects are negated";
        }

        return ValidateTiming(s, player, card, effect);
    }

    public static void ActivateEffect(DuelEngine engine, int player, Guid cardId, int effectIndex)
    {
        CardInstance card = Zones.OnField(engine.State, player, cardId)!;
        IEffect effect = engine.EffectsOf(card)[effectIndex];
        engine.Emit(new EffectActivated(player, card.Id, card.Def.Id, effect.Id));
        engine.BeginActivation(player, card, effect, ActivationContext.None);
    }

    public static string? ValidateSetSpellTrap(DuelState s, int player, Guid cardId)
    {
        string? error = TurnFlow.ValidateMainPhaseAction(s, player);
        if (error is not null)
        {
            return error;
        }

        CardInstance? card = Zones.InHand(s, player, cardId);
        if (card is null)
        {
            return "the card is not in your hand";
        }

        if (!card.Def.IsSpell && !card.Def.IsTrap)
        {
            return $"{card.Def.Name} is not a Spell or Trap";
        }

        return s.Player(player).SpellTrapCount >= PlayerState.ZoneCount ? "no free Spell & Trap Zone" : null;
    }

    public static void SetSpellTrap(DuelEngine engine, int player, Guid cardId)
    {
        DuelState s = engine.State;
        CardInstance card = Zones.InHand(s, player, cardId)!;
        int zone = s.Player(player).FirstFreeSpellTrapZone();
        Zones.PlaceSpellTrap(engine, card, player, zone, Position.FaceDown);
        card.SetThisTurn = true;
        card.ArrivedThisTurn = true;
        engine.Emit(new SpellTrapSet(player, card.Id, zone));
        TurnFlow.GivePriorityToTurnPlayer(s);
    }

    /// <summary>Resolves the whole chain last in, first out; negated links, and Trap links under a Trap negation, are skipped. Spells and Traps that are spent go to the Graveyard after their link.</summary>
    public static void ResolveAll(DuelEngine engine)
    {
        DuelState s = engine.State;
        while (s.Chain.Count > 0 && !s.IsOver)
        {
            ChainLink link = s.Chain[^1];
            s.Chain.RemoveAt(s.Chain.Count - 1);
            if (!link.Negated && !IsSilenced(s, link))
            {
                link.Effect.Resolve(engine, link);
            }

            engine.Emit(new ChainLinkResolved(link.Index, link.Source.Id, link.Effect.Id));
            engine.Refresh();
            Discharge(engine, link.Source);
        }

        s.Chain.Clear();
    }

    /// <summary>A Trap link resolving while its controller's Traps are negated (Jinzo), or a monster whose effects are negated, does nothing.</summary>
    private static bool IsSilenced(DuelState s, ChainLink link) =>
        (link.Source.Def.IsTrap && s.Player(link.Player).Has(PlayerRestriction.TrapsNegated))
        || (link.Source.IsOnField && link.Source.Has(Restriction.EffectsNegated));

    /// <summary>The timing every activation shares: speed 1 in the turn player's open Main Phase (Ignition effects also under summon priority), speed 2+ on priority with the Set-turn and Damage Step limits, then the card's own condition.</summary>
    private static string? ValidateTiming(DuelState s, int player, CardInstance card, IEffect effect)
    {
        if (effect.Speed == SpellSpeed.One)
        {
            if (player != s.TurnPlayer)
            {
                return "only the turn player may do that";
            }

            if (!TurnFlow.IsMainPhase(s))
            {
                return "only during a Main Phase";
            }

            if (s.Chain.Count > 0)
            {
                return "not while a chain is being built";
            }

            if (s.Window != Window.Open && !(s.Window == Window.Summon && effect.Kind == EffectKind.Ignition))
            {
                return "not while a window is open for responses";
            }
        }
        else
        {
            if (s.Window == Window.DamageAfterCalc && s.Chain.Count == 0)
            {
                return "after damage calculation only Trigger effects activate";
            }

            bool counterTrap = card.Def.Trap?.Subtype == TrapSubtype.Counter;
            if (s.Window == Window.DamageBeforeCalc && !counterTrap && !effect.UsableInDamageStep)
            {
                return "only Counter Traps and ATK/DEF modifiers before damage calculation";
            }

            if (card.Loc == Location.Hand && player != s.TurnPlayer)
            {
                return "Quick-Play Spells are activated from the hand only on your turn";
            }

            if (card.Loc == Location.SpellTrapZone && card.SetThisTurn)
            {
                return $"{card.Def.Name} cannot be activated on the turn it was Set";
            }
        }

        if (!CanChain(s, effect))
        {
            return "spell speed too low to chain";
        }

        if (effect.OncePerTurn && card.Activations(effect.Id) > 0)
        {
            return $"{card.Def.Name}'s effect was already used this turn";
        }

        return effect.CanActivate(s, card, ActivationContext.None) ? null : $"{card.Def.Name} cannot be activated now";
    }

    /// <summary>Where a card goes once its activation resolved: one-shot Spells and Traps to the Graveyard, an Equip Spell that attached to nothing too; Continuous and Field cards, attached equips and monsters stay.</summary>
    private static void Discharge(DuelEngine engine, CardInstance card)
    {
        if (card.Loc != Location.SpellTrapZone || card.IsFaceDown)
        {
            return;
        }

        bool spent = card.Def.Spell?.Subtype is SpellSubtype.Normal or SpellSubtype.Quick or SpellSubtype.Ritual
            || (card.Def.Spell?.Subtype == SpellSubtype.Equip && card.EquippedTo is null)
            || card.Def.Trap?.Subtype is TrapSubtype.Normal or TrapSubtype.Counter;
        if (spent)
        {
            Zones.ToGraveyard(engine, card);
        }
    }
}
