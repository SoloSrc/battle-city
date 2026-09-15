using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Effects;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Rules;

/// <summary>Normal Summon, Set, position changes and Flip Summons (GDD §3.2, systems.md §5.5).</summary>
internal static class SummonRules
{
    public static string? ValidateNormalSummon(DuelEngine engine, int player, Guid cardId, IReadOnlyList<Guid> tributes, bool set)
    {
        ArgumentNullException.ThrowIfNull(tributes);
        DuelState s = engine.State;
        string? error = TurnFlow.ValidateMainPhaseAction(s, player);
        if (error is not null)
        {
            return error;
        }

        if (s.NormalSummonUsed)
        {
            return "only one Normal Summon or Set per turn";
        }

        CardInstance? card = Zones.InHand(s, player, cardId);
        if (card is null)
        {
            return "the card is not in your hand";
        }

        if (card.Def.Kind != CardKind.Monster)
        {
            return $"{card.Def.Name} is not a monster";
        }

        if (!set && s.Player(player).Has(PlayerRestriction.CannotSummon))
        {
            return "you cannot Summon this turn";
        }

        SummonLimit limits = engine.EffectsOf(card).Aggregate(SummonLimit.None, (all, e) => all | e.Limits);
        if (set ? limits.HasFlag(SummonLimit.CannotBeSet) : limits.HasFlag(SummonLimit.CannotBeNormalSummoned))
        {
            return $"{card.Def.Name} cannot be {(set ? "Set" : "Normal Summoned")}";
        }

        int required = card.Def.Monster!.TributesRequired;
        if (tributes.Count != required)
        {
            return $"{card.Def.Name} needs {required} tribute(s), {tributes.Count} given";
        }

        if (tributes.Distinct().Count() != tributes.Count)
        {
            return "a monster cannot be tributed twice";
        }

        foreach (Guid id in tributes)
        {
            CardInstance? tribute = Zones.OnField(s, player, id);
            if (tribute is null)
            {
                return "tributes must be monsters you control";
            }

            if (tribute.Has(Restriction.CannotBeTributed))
            {
                return $"{tribute.Def.Name} cannot be tributed";
            }
        }

        PlayerState p = s.Player(player);
        if (p.MonsterCount - tributes.Count >= PlayerState.ZoneCount)
        {
            return "no free Monster Zone";
        }

        return null;
    }

    public static void NormalSummon(DuelEngine engine, int player, Guid cardId, IReadOnlyList<Guid> tributes, bool set)
    {
        DuelState s = engine.State;
        CardInstance card = Zones.InHand(s, player, cardId)!;
        foreach (Guid id in tributes)
        {
            Zones.ToGraveyard(engine, Zones.OnField(s, player, id)!);
        }

        int zone = s.Player(player).FirstFreeMonsterZone();
        Position position = set ? Position.FaceDownDefense : Position.FaceUpAttack;
        Zones.PlaceMonster(engine, card, player, zone, position);
        card.ArrivedThisTurn = true;
        card.SetThisTurn = set;
        s.NormalSummonUsed = true;
        if (set)
        {
            engine.Emit(new MonsterSet(player, card.Id, zone, tributes));
        }
        else
        {
            engine.Emit(new MonsterSummoned(player, card.Id, card.Def.Id, zone, position, tributes));
            Summoned(engine, card, tributes.Count > 0 ? SummonKind.Tribute : SummonKind.Normal);
        }

        // Ignition-effect priority: the turn player keeps priority after a summon (systems.md §5.5).
        TurnFlow.GivePriorityToTurnPlayer(s);
    }

    public static string? ValidateChangePosition(DuelState s, int player, Guid cardId)
    {
        string? error = TurnFlow.ValidateMainPhaseAction(s, player);
        if (error is not null)
        {
            return error;
        }

        CardInstance? card = Zones.OnField(s, player, cardId);
        if (card is null)
        {
            return "the monster is not on your field";
        }

        if (card.IsFaceDown)
        {
            return "a face-down monster is Flip Summoned instead";
        }

        return ValidatePositionChangeTiming(card);
    }

    public static void ChangePosition(DuelEngine engine, int player, Guid cardId)
    {
        CardInstance card = Zones.OnField(engine.State, player, cardId)!;
        card.ChangedPositionThisTurn = true;
        engine.SwitchPosition(card);
        TurnFlow.GivePriorityToTurnPlayer(engine.State);
    }

    public static string? ValidateFlipSummon(DuelState s, int player, Guid cardId)
    {
        string? error = TurnFlow.ValidateMainPhaseAction(s, player);
        if (error is not null)
        {
            return error;
        }

        CardInstance? card = Zones.OnField(s, player, cardId);
        if (card is null)
        {
            return "the monster is not on your field";
        }

        if (card.IsFaceUp)
        {
            return "only a face-down monster can be Flip Summoned";
        }

        if (s.Player(player).Has(PlayerRestriction.CannotSummon))
        {
            return "you cannot Summon this turn";
        }

        return ValidatePositionChangeTiming(card);
    }

    public static void FlipSummon(DuelEngine engine, int player, Guid cardId)
    {
        CardInstance card = Zones.OnField(engine.State, player, cardId)!;
        card.Pos = Position.FaceUpAttack;
        card.ChangedPositionThisTurn = true;
        card.FlippedThisTurn = true;
        engine.Emit(new MonsterFlipSummoned(player, card.Id, card.Def.Id));
        engine.Refresh();
        Summoned(engine, card, SummonKind.Flip);
        engine.QueueTriggers(card, TriggerWindow.OnFlip, summon: SummonKind.Flip);
        TurnFlow.GivePriorityToTurnPlayer(engine.State);
    }

    /// <summary>A monster arrived face-up: its summon triggers fire with <paramref name="kind"/> and, in an open state, the summon window opens for responses.</summary>
    public static void Summoned(DuelEngine engine, CardInstance card, SummonKind kind)
    {
        engine.State.LastSummon = kind;
        engine.QueueTriggers(card, TriggerWindow.OnSummon, summon: kind);
        if (engine.State.Window == Window.Open)
        {
            TurnFlow.SetWindow(engine, Window.Summon, card.Id);
        }
    }

    /// <summary>Position changes: once per turn, not on the turn the monster arrived, not after it attacked, not under a position lock (systems.md §5.5).</summary>
    private static string? ValidatePositionChangeTiming(CardInstance card)
    {
        if (card.Has(Restriction.CannotChangePosition))
        {
            return $"{card.Def.Name} cannot change its battle position";
        }

        if (card.ArrivedThisTurn)
        {
            return "a monster cannot change position on the turn it was Summoned or Set";
        }

        if (card.ChangedPositionThisTurn)
        {
            return "a monster changes position once per turn";
        }

        if (card.AttackedThisTurn)
        {
            return "a monster that attacked cannot change position this turn";
        }

        return null;
    }
}
