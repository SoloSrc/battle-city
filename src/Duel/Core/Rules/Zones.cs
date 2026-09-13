using System;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Rules;

/// <summary>Card movement between locations. Every move goes through here so the location bookkeeping stays consistent.</summary>
internal static class Zones
{
    /// <summary>Removes a card from wherever it is; the caller places it somewhere else.</summary>
    public static void Detach(DuelState state, CardInstance card)
    {
        PlayerState holder = state.Player(card.Loc is Location.MonsterZone or Location.SpellTrapZone or Location.FieldZone ? card.Controller : card.Owner);
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
    }

    public static void ToGraveyard(DuelEngine engine, CardInstance card)
    {
        Location from = card.Loc;
        Detach(engine.State, card);
        card.ResetFieldState();
        card.Loc = Location.Graveyard;
        engine.State.Player(card.Owner).Graveyard.Add(card);
        engine.Emit(new CardSentToGraveyard(card.Owner, card.Id, card.Def.Id, from));
    }

    public static void PlaceMonster(DuelState state, CardInstance card, int player, int zone, Position position)
    {
        Detach(state, card);
        card.Controller = player;
        card.Loc = Location.MonsterZone;
        card.ZoneIndex = zone;
        card.Pos = position;
        state.Player(player).MonsterZones[zone] = card;
    }

    public static void PlaceSpellTrap(DuelState state, CardInstance card, int player, int zone, Position position)
    {
        Detach(state, card);
        card.Controller = player;
        card.Loc = Location.SpellTrapZone;
        card.ZoneIndex = zone;
        card.Pos = position;
        state.Player(player).SpellTrapZones[zone] = card;
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
