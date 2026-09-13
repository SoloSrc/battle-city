using System;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Rules;

/// <summary>
/// Turns, phases and priority (systems.md §5.5). Priority passes alternate;
/// two consecutive passes resolve the chain, run a pending Damage Step, or
/// advance the phase or step.
/// </summary>
internal static class TurnFlow
{
    public static void StartTurn(DuelEngine engine, int player)
    {
        DuelState s = engine.State;
        s.TurnNumber++;
        s.TurnPlayer = player;
        s.NormalSummonUsed = false;
        s.BattlePhaseUsed = false;
        s.BattleStep = BattleStep.None;
        s.DamageSubstep = DamageSubstep.None;
        s.Attacker = null;
        s.AttackTarget = null;
        foreach (PlayerState p in s.Players)
        {
            foreach (CardInstance card in p.AllCards)
            {
                card.ResetTurnFlags();
            }
        }

        engine.Emit(new TurnStarted(player, s.TurnNumber));
        SetPhase(engine, Phase.Draw);
        // 2005 rule: the first player draws on turn 1; there is no exception (systems.md §5.5).
        engine.Draw(player, 1);
        GivePriorityToTurnPlayer(s);
    }

    public static string? ValidatePass(DuelState s, int player, DuelOptions options)
    {
        if (HandLimitPending(s, options) && player == s.TurnPlayer)
        {
            return $"discard down to {options.HandLimit} cards before ending the turn";
        }

        return null;
    }

    public static void Pass(DuelEngine engine, int player)
    {
        DuelState s = engine.State;
        s.ConsecutivePasses++;
        engine.Emit(new PriorityPassed(player));
        if (s.ConsecutivePasses < 2)
        {
            s.Priority = 1 - player;
            return;
        }

        if (s.Chain.Count > 0)
        {
            ChainResolver.ResolveAll(engine);
        }
        else if (s.Attacker is not null)
        {
            DamageStep.Run(engine);
        }
        else
        {
            Advance(engine);
        }

        if (!s.IsOver)
        {
            GivePriorityToTurnPlayer(s);
        }
    }

    public static string? ValidateDiscard(DuelState s, int player, Guid card, DuelOptions options)
    {
        if (!HandLimitPending(s, options) || player != s.TurnPlayer)
        {
            return "no discard is pending";
        }

        return Zones.InHand(s, player, card) is null ? "the card is not in your hand" : null;
    }

    public static void Discard(DuelEngine engine, int player, Guid cardId)
    {
        CardInstance card = Zones.InHand(engine.State, player, cardId)!;
        engine.Emit(new CardDiscarded(player, card.Id, card.Def.Id));
        Zones.ToGraveyard(engine, card);
        GivePriorityToTurnPlayer(engine.State);
    }

    public static bool HandLimitPending(DuelState s, DuelOptions options) =>
        s.Phase == Phase.End && s.Current.Hand.Count > options.HandLimit;

    public static bool IsMainPhase(DuelState s) => s.Phase is Phase.Main1 or Phase.Main2;

    /// <summary>The turn player may act freely: their turn, a Main Phase, empty chain, no attack in progress.</summary>
    public static string? ValidateMainPhaseAction(DuelState s, int player)
    {
        if (player != s.TurnPlayer)
        {
            return "only the turn player may do that";
        }

        if (!IsMainPhase(s))
        {
            return "only during a Main Phase";
        }

        if (s.Chain.Count > 0)
        {
            return "not while a chain is being built";
        }

        return null;
    }

    public static void GivePriorityToTurnPlayer(DuelState s)
    {
        s.Priority = s.TurnPlayer;
        s.ConsecutivePasses = 0;
    }

    public static void SetPhase(DuelEngine engine, Phase phase)
    {
        engine.State.Phase = phase;
        engine.Emit(new PhaseChanged(phase));
    }

    public static void SetBattleStep(DuelEngine engine, BattleStep step)
    {
        engine.State.BattleStep = step;
        engine.Emit(new BattleStepChanged(step));
    }

    private static void Advance(DuelEngine engine)
    {
        DuelState s = engine.State;
        switch (s.Phase)
        {
            case Phase.Draw:
                SetPhase(engine, Phase.Standby);
                break;
            case Phase.Standby:
                SetPhase(engine, Phase.Main1);
                break;
            case Phase.Main1:
                // Skipping the Battle Phase goes straight to the End Phase; Main Phase 2 only follows a Battle Phase.
                SetPhase(engine, Phase.End);
                break;
            case Phase.Battle:
                AdvanceBattleStep(engine);
                break;
            case Phase.Main2:
                SetPhase(engine, Phase.End);
                break;
            case Phase.End:
                StartTurn(engine, 1 - s.TurnPlayer);
                break;
        }
    }

    private static void AdvanceBattleStep(DuelEngine engine)
    {
        DuelState s = engine.State;
        switch (s.BattleStep)
        {
            case BattleStep.Start:
                SetBattleStep(engine, BattleStep.Battle);
                break;
            case BattleStep.Battle:
                SetBattleStep(engine, BattleStep.End);
                break;
            case BattleStep.End:
                s.BattleStep = BattleStep.None;
                SetPhase(engine, Phase.Main2);
                break;
            default:
                throw new InvalidOperationException($"Cannot advance from battle step {s.BattleStep}.");
        }
    }
}
