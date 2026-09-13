using System;
using System.Linq;
using BattleCity.Duel.Core.Effects;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Rules;

/// <summary>
/// Card movement between locations. Every move goes through here so the
/// location bookkeeping stays consistent: a card leaving the field takes
/// its equips with it, runs its effects' leave hooks, drops its modifiers
/// and, if it is a token, leaves the duel; every move recomputes the
/// modifiers.
/// </summary>
internal static class Zones
{
    /// <summary>Removes a card from wherever it is; the caller places it somewhere else. Returns false when the card was a token and is gone.</summary>
    public static bool Detach(DuelEngine engine, CardInstance card)
    {
        DuelState state = engine.State;
        bool leavingField = card.IsOnField;
        Location from = card.Loc;
        Guid? equippedTo = card.EquippedTo;
        PlayerState holder = state.Player(leavingField ? card.Controller : card.Owner);
        switch (card.Loc)
        {
            case Location.Deck:
                holder.Deck.Remove(card);
                break;
            case Location.Hand:
                holder.Hand.Remove(card);
                break;
            case Location.Graveyard:
                holder.Graveyard.Remove(card);
                break;
            case Location.Banished:
                holder.Banished.Remove(card);
                break;
            case Location.FusionDeck:
                holder.FusionDeck.Remove(card);
                break;
            case Location.MonsterZone:
                holder.MonsterZones[card.ZoneIndex] = null;
                break;
            case Location.SpellTrapZone:
                holder.SpellTrapZones[card.ZoneIndex] = null;
                break;
            case Location.FieldZone:
                holder.FieldZone = null;
                break;
        }

        if (!leavingField)
        {
            return true;
        }

        // Leaving the field: the card is off the field before anything else runs (a hook must not find it there),
        // then the equips attached to it are destroyed, its own leave hooks run and its modifiers go.
        card.Loc = Location.Banished;
        card.ResetFieldState();
        Modifiers.RemoveFor(engine, card);
        foreach (CardInstance equip in state.Players.SelectMany(p => p.SpellTraps).Where(e => e.EquippedTo == card.Id).ToList())
        {
            engine.Destroy(equip, DestroyReason.Effect);
        }

        foreach (IEffect effect in engine.EffectsOf(card))
        {
            effect.OnLeftField(engine, card, from, equippedTo);
        }

        if (card.IsToken)
        {
            engine.Emit(new TokenRemoved(card.Owner, card.Id, card.Def.Id));
            engine.Refresh();
            return false;
        }

        return true;
    }

    public static void ToGraveyard(DuelEngine engine, CardInstance card)
    {
        Location from = card.Loc;
        if (!Detach(engine, card))
        {
            return;
        }

        card.Loc = Location.Graveyard;
        engine.State.Player(card.Owner).Graveyard.Add(card);
        engine.Emit(new CardSentToGraveyard(card.Owner, card.Id, card.Def.Id, from));
        engine.Refresh();
        engine.QueueTriggers(card, TriggerWindow.OnSentToGrave, from);
    }

    public static void ToBanished(DuelEngine engine, CardInstance card)
    {
        Location from = card.Loc;
        if (!Detach(engine, card))
        {
            return;
        }

        card.Loc = Location.Banished;
        engine.State.Player(card.Owner).Banished.Add(card);
        engine.Emit(new CardBanished(card.Owner, card.Id, card.Def.Id, from));
        engine.Refresh();
    }

    public static void ToHand(DuelEngine engine, CardInstance card)
    {
        Location from = card.Loc;
        if (!Detach(engine, card))
        {
            return;
        }

        card.Loc = Location.Hand;
        engine.State.Player(card.Owner).Hand.Add(card);
        engine.Emit(new CardReturnedToHand(card.Owner, card.Id, card.Def.Id, from));
        engine.Refresh();
    }

    public static void ToDeck(DuelEngine engine, CardInstance card, bool top)
    {
        Location from = card.Loc;
        if (!Detach(engine, card))
        {
            return;
        }

        card.Loc = Location.Deck;
        PlayerState owner = engine.State.Player(card.Owner);
        if (top)
        {
            owner.Deck.Add(card);
        }
        else
        {
            owner.Deck.Insert(0, card);
        }

        engine.Emit(new CardReturnedToDeck(card.Owner, card.Id, card.Def.Id, from, top));
        engine.Refresh();
    }

    public static void PlaceMonster(DuelEngine engine, CardInstance card, int player, int zone, Position position)
    {
        Detach(engine, card);
        card.Controller = player;
        card.Loc = Location.MonsterZone;
        card.ZoneIndex = zone;
        card.Pos = position;
        engine.State.Player(player).MonsterZones[zone] = card;
        engine.Refresh();
    }

    public static void PlaceSpellTrap(DuelEngine engine, CardInstance card, int player, int zone, Position position)
    {
        Detach(engine, card);
        card.Controller = player;
        card.Loc = Location.SpellTrapZone;
        card.ZoneIndex = zone;
        card.Pos = position;
        engine.State.Player(player).SpellTrapZones[zone] = card;
        engine.Refresh();
    }

    public static void PlaceField(DuelEngine engine, CardInstance card, int player)
    {
        Detach(engine, card);
        card.Controller = player;
        card.Loc = Location.FieldZone;
        card.ZoneIndex = 0;
        card.Pos = Position.FaceUp;
        engine.State.Player(player).FieldZone = card;
        engine.Refresh();
    }

    /// <summary>Moves a monster to another player's side without leaving the field (control change); false when they have no free zone.</summary>
    public static bool MoveToSide(DuelEngine engine, CardInstance card, int player)
    {
        DuelState state = engine.State;
        int zone = state.Player(player).FirstFreeMonsterZone();
        if (zone < 0)
        {
            return false;
        }

        state.Player(card.Controller).MonsterZones[card.ZoneIndex] = null;
        card.Controller = player;
        card.ZoneIndex = zone;
        state.Player(player).MonsterZones[zone] = card;
        engine.Refresh();
        return true;
    }

    /// <summary>A card in <paramref name="player"/>'s hand, or null.</summary>
    public static CardInstance? InHand(DuelState state, int player, Guid id) =>
        state.Player(player).Hand.Find(c => c.Id == id);

    /// <summary>A monster on <paramref name="player"/>'s field, or null.</summary>
    public static CardInstance? OnField(DuelState state, int player, Guid id)
    {
        foreach (CardInstance? c in state.Player(player).MonsterZones)
        {
            if (c is not null && c.Id == id)
            {
                return c;
            }
        }

        return null;
    }

    /// <summary>A Spell/Trap on <paramref name="player"/>'s field, or null.</summary>
    public static CardInstance? OnSpellTrapZone(DuelState state, int player, Guid id)
    {
        foreach (CardInstance? c in state.Player(player).SpellTrapZones)
        {
            if (c is not null && c.Id == id)
            {
                return c;
            }
        }

        return null;
    }
}
