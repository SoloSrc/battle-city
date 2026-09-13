using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Rules;

/// <summary>Normal Summon, Set, position changes and Flip Summons (GDD §3.2, systems.md §5.5).</summary>
internal static class SummonRules
{
    public static string? ValidateNormalSummon(DuelState s, int player, Guid cardId, IReadOnlyList<Guid> tributes, bool set)
    {
        ArgumentNullException.ThrowIfNull(tributes);
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
            if (Zones.OnField(s, player, id) is null)
            {
                return "tributes must be monsters you control";
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
        Zones.PlaceMonster(s, card, player, zone, position);
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
        Position from = card.Pos;
        card.Pos = from == Position.FaceUpAttack ? Position.FaceUpDefense : Position.FaceUpAttack;
        card.ChangedPositionThisTurn = true;
        engine.Emit(new PositionChanged(player, card.Id, from, card.Pos));
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

        return ValidatePositionChangeTiming(card);
    }

    public static void FlipSummon(DuelEngine engine, int player, Guid cardId)
    {
        CardInstance card = Zones.OnField(engine.State, player, cardId)!;
        card.Pos = Position.FaceUpAttack;
        card.ChangedPositionThisTurn = true;
        engine.Emit(new MonsterFlipSummoned(player, card.Id, card.Def.Id));
        TurnFlow.GivePriorityToTurnPlayer(engine.State);
    }

    /// <summary>Position changes: once per turn, not on the turn the monster arrived, not after it attacked (systems.md §5.5).</summary>
    private static string? ValidatePositionChangeTiming(CardInstance card)
    {
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
