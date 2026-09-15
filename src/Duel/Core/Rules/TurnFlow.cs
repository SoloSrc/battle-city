using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Effects;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Rules;

/// <summary>
/// Turns, phases, priority and windows (systems.md §5.5). Priority passes
/// alternate; two consecutive passes resolve the chain or, on an empty
/// chain, close the current <see cref="Window"/>: an open window advances
/// the phase or step, a summon window returns to the Main Phase, the attack
/// windows move the Damage Step along.
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
        s.BattleFlipped = null;
        s.Window = Window.Open;
        s.WindowCard = null;
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

        return BattleRules.MustAttackWith(s, player) is { } attacker ? $"{attacker.Def.Name} must attack" : null;
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

        GivePriorityToTurnPlayer(s);
        if (s.Chain.Count > 0)
        {
            ChainResolver.ResolveAll(engine);
            return;
        }

        switch (s.Window)
        {
            case Window.Open:
                Advance(engine);
                break;
            case Window.Summon:
                SetWindow(engine, Window.Open, null);
                break;
            case Window.AttackDeclared:
                DamageStep.Begin(engine);
                break;
            case Window.DamageBeforeCalc:
                DamageStep.Calculate(engine);
                break;
            case Window.DamageAfterCalc:
                DamageStep.End(engine);
                break;
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
        s.Phase == Phase.End && s.Window == Window.Open && s.Chain.Count == 0 && s.Current.Hand.Count > options.HandLimit;

    public static bool IsMainPhase(DuelState s) => s.Phase is Phase.Main1 or Phase.Main2;

    /// <summary>The turn player may act freely: their turn, a Main Phase, empty chain, no window waiting for responses.</summary>
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

        if (s.Window != Window.Open)
        {
            return "not while a window is open for responses";
        }

        return null;
    }

    public static void GivePriorityToTurnPlayer(DuelState s)
    {
        s.Priority = s.TurnPlayer;
        s.ConsecutivePasses = 0;
    }

    public static void SetWindow(DuelEngine engine, Window window, Guid? card)
    {
        engine.State.Window = window;
        engine.State.WindowCard = card;
        engine.Emit(new WindowChanged(window, card));
    }

    /// <summary>
    /// Enters a phase. The End Phase first ends what lasts "until the end of
    /// the turn": timed modifiers expire, temporary control returns, Spirit
    /// monsters Summoned or flipped this turn go back to the hand and cards
    /// whose turns ran out (Swords of Revealing Light) are destroyed; then the
    /// Standby and End Phases fire the triggers of every face-up card on the
    /// field and every card in a Graveyard (Sinister Serpent), turn player's
    /// side first.
    /// </summary>
    public static void SetPhase(DuelEngine engine, Phase phase)
    {
        DuelState s = engine.State;
        s.Phase = phase;
        engine.Emit(new PhaseChanged(phase));
        if (phase == Phase.End)
        {
            EndOfTurn(engine);
        }

        TriggerWindow? window = phase switch
        {
            Phase.Standby => TriggerWindow.Standby,
            Phase.End => TriggerWindow.EndPhase,
            _ => null,
        };
        if (window is null)
        {
            return;
        }

        foreach (PlayerState p in new[] { s.Current, s.Opponent(s.TurnPlayer) })
        {
            foreach (CardInstance card in p.Monsters.Concat(p.SpellTraps).Where(c => c.IsFaceUp).Concat(p.Graveyard).ToList())
            {
                engine.QueueTriggers(card, window.Value);
            }
        }
    }

    private static void EndOfTurn(DuelEngine engine)
    {
        DuelState s = engine.State;
        Modifiers.Expire(engine);
        foreach (CardInstance card in s.Players.SelectMany(p => p.Monsters).Where(m => m.ControlReturnsAfterTurn is { } t && t <= s.TurnNumber).ToList())
        {
            engine.ChangeControl(card, card.Owner);
        }

        foreach (CardInstance card in s.Players.SelectMany(p => p.Monsters).Where(IsSpiritGoingHome).ToList())
        {
            engine.Emit(new SpiritReturned(card.Controller, card.Id, card.Def.Id));
            engine.ReturnToHand(card);
        }

        foreach (CardInstance card in s.Players.SelectMany(p => p.SpellTraps).Where(c => c.LeavesAfterTurn is { } t && t <= s.TurnNumber).ToList())
        {
            engine.Destroy(card, DestroyReason.Effect);
        }

        engine.Refresh();
    }

    /// <summary>A face-up Spirit monster Normal Summoned or flipped face-up this turn returns to the hand at the End Phase.</summary>
    private static bool IsSpiritGoingHome(CardInstance card) =>
        card.IsFaceUp && card.Def.Monster?.Category == MonsterCategory.Spirit && (card.ArrivedThisTurn || card.FlippedThisTurn);

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
                BattleRules.EndOfBattlePhase(engine);
                s.BattleStep = BattleStep.None;
                SetPhase(engine, Phase.Main2);
                break;
            default:
                throw new InvalidOperationException($"Cannot advance from battle step {s.BattleStep}.");
        }
    }
}
