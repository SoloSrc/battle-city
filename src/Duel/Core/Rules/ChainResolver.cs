using System;
using System.Linq;
using BattleCity.Duel.Core.Effects;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Rules;

/// <summary>
/// Spell/Trap activation and chain resolution (systems.md §5.5). Tier 1
/// activates Normal Spells on an empty chain; <see cref="CanChain"/> holds
/// the spell speed rule for the responses that arrive with tier 2.
/// </summary>
internal static class ChainResolver
{
    /// <summary>Spell speed rule: a link needs at least the previous link's speed, and Counter Traps only answer with speed 3.</summary>
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
        string? error = TurnFlow.ValidateMainPhaseAction(s, player);
        if (error is not null)
        {
            return error;
        }

        if (s.Attacker is not null)
        {
            return "not while an attack is in progress";
        }

        CardInstance? card = Zones.InHand(s, player, cardId) ?? Zones.OnSpellTrapZone(s, player, cardId);
        if (card is null)
        {
            return "the card is not in your hand or Set on your field";
        }

        if (card.Def.Spell is null)
        {
            return $"{card.Def.Name} is not a Spell";
        }

        if (card.Def.Spell.Subtype != SpellSubtype.Normal)
        {
            return $"{card.Def.Spell.Subtype} Spells arrive with tier 2";
        }

        if (card.Loc == Location.SpellTrapZone && card.IsFaceUp)
        {
            return $"{card.Def.Name} is already active";
        }

        if (card.Loc == Location.Hand && s.Player(player).SpellTrapCount >= PlayerState.ZoneCount)
        {
            return "no free Spell & Trap Zone";
        }

        IEffect? activation = engine.EffectsOf(card).FirstOrDefault(e => e.Kind == EffectKind.Activation);
        if (activation is null)
        {
            return $"{card.Def.Name} has no activation effect";
        }

        if (!activation.CanActivate(s, card))
        {
            return $"{card.Def.Name} cannot be activated now";
        }

        return CanChain(s, activation) ? null : "spell speed too low to chain";
    }

    public static void ActivateSpell(DuelEngine engine, int player, Guid cardId)
    {
        DuelState s = engine.State;
        CardInstance card = Zones.InHand(s, player, cardId) ?? Zones.OnSpellTrapZone(s, player, cardId)!;
        int zone = card.Loc == Location.SpellTrapZone ? card.ZoneIndex : s.Player(player).FirstFreeSpellTrapZone();
        Zones.PlaceSpellTrap(s, card, player, zone, Position.FaceUp);
        engine.Emit(new SpellActivated(player, card.Id, card.Def.Id, zone));

        IEffect activation = engine.EffectsOf(card).First(e => e.Kind == EffectKind.Activation);
        var link = new ChainLink(s.Chain.Count + 1, player, card, activation);
        s.Chain.Add(link);
        engine.Emit(new ChainLinkAdded(link.Index, player, card.Id, activation.Id));

        // The opponent may respond; two passes in a row resolve the chain (TurnFlow.Pass).
        s.ConsecutivePasses = 1;
        s.Priority = 1 - player;
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
        Zones.PlaceSpellTrap(s, card, player, zone, Position.FaceDown);
        card.SetThisTurn = true;
        card.ArrivedThisTurn = true;
        engine.Emit(new SpellTrapSet(player, card.Id, zone));
        TurnFlow.GivePriorityToTurnPlayer(s);
    }

    /// <summary>Resolves the whole chain last in, first out; a Normal Spell goes to the Graveyard after its link resolves.</summary>
    public static void ResolveAll(DuelEngine engine)
    {
        DuelState s = engine.State;
        while (s.Chain.Count > 0 && !s.IsOver)
        {
            ChainLink link = s.Chain[^1];
            s.Chain.RemoveAt(s.Chain.Count - 1);
            link.Effect.Resolve(engine, link);
            engine.Emit(new ChainLinkResolved(link.Index, link.Source.Id, link.Effect.Id));
            if (link.Source.Loc == Location.SpellTrapZone && link.Source.Def.Spell?.Subtype == SpellSubtype.Normal)
            {
                Zones.ToGraveyard(engine, link.Source);
            }
        }

        s.Chain.Clear();
    }
}
