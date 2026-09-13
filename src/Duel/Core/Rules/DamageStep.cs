using System.Collections.Generic;
using BattleCity.Duel.Core.Effects;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Rules;

/// <summary>
/// The Damage Step as a state machine driven by passes (systems.md §5.5):
/// <see cref="Begin"/> runs StartDamage and BeforeCalc (a face-down target
/// flips) and opens the <see cref="Window.DamageBeforeCalc"/> window;
/// <see cref="Calculate"/> runs damage calculation, destroys, fires flip and
/// battle triggers and opens <see cref="Window.DamageAfterCalc"/>;
/// <see cref="End"/> runs EndDamage and returns to the Battle Step.
/// </summary>
internal static class DamageStep
{
    public static void Begin(DuelEngine engine)
    {
        DuelState s = engine.State;
        CardInstance? attacker = s.Find(s.Attacker!.Value);
        CardInstance? target = s.AttackTarget is null ? null : s.Find(s.AttackTarget.Value);

        // The attacker or its target left the field while the declaration was open: the attack ends without a replay (docs/decisions.md).
        if (attacker is null || attacker.Loc != Location.MonsterZone || (target is not null && target.Loc != Location.MonsterZone))
        {
            engine.Emit(new AttackCancelled(s.Attacker.Value));
            Finish(engine);
            return;
        }

        TurnFlow.SetBattleStep(engine, BattleStep.Damage);
        SetSubstep(engine, DamageSubstep.StartDamage);
        SetSubstep(engine, DamageSubstep.BeforeCalc);
        if (target is { IsFaceDown: true })
        {
            target.Pos = Position.FaceUpDefense;
            s.BattleFlipped = target.Id;
            engine.Emit(new MonsterFlipped(target.Controller, target.Id, target.Def.Id));
        }

        TurnFlow.SetWindow(engine, Window.DamageBeforeCalc, null);
    }

    public static void Calculate(DuelEngine engine)
    {
        DuelState s = engine.State;
        CardInstance? attacker = s.Find(s.Attacker!.Value);
        CardInstance? target = s.AttackTarget is null ? null : s.Find(s.AttackTarget.Value);

        SetSubstep(engine, DamageSubstep.Calc);
        var destroyed = new List<CardInstance>();
        if (attacker is { Loc: Location.MonsterZone } && (target is null || target.Loc == Location.MonsterZone))
        {
            destroyed = Resolve(engine, attacker, target);
        }

        SetSubstep(engine, DamageSubstep.AfterCalc);
        foreach (CardInstance card in destroyed)
        {
            engine.Destroy(card, DestroyReason.Battle);
        }

        if (s.BattleFlipped is { } flipped && s.Find(flipped) is { } flippedCard)
        {
            engine.QueueTriggers(flippedCard, TriggerWindow.OnFlip);
        }

        if (s.IsOver)
        {
            Finish(engine);
            return;
        }

        TurnFlow.SetWindow(engine, Window.DamageAfterCalc, null);
    }

    public static void End(DuelEngine engine)
    {
        SetSubstep(engine, DamageSubstep.EndDamage);
        Finish(engine);
    }

    /// <summary>Damage calculation (GDD §3.2). Returns the monsters destroyed by battle.</summary>
    private static List<CardInstance> Resolve(DuelEngine engine, CardInstance attacker, CardInstance? target)
    {
        var destroyed = new List<CardInstance>();
        int attackerPlayer = attacker.Controller;
        int atk = attacker.Atk;

        if (target is null)
        {
            engine.Damage(1 - attackerPlayer, atk, attacker.Id);
            return destroyed;
        }

        int defenderPlayer = target.Controller;
        if (target.IsInAttackPosition)
        {
            int diff = atk - target.Atk;
            if (diff > 0)
            {
                destroyed.Add(target);
                engine.Damage(defenderPlayer, diff, attacker.Id);
            }
            else if (diff < 0)
            {
                destroyed.Add(attacker);
                engine.Damage(attackerPlayer, -diff, target.Id);
            }
            else
            {
                destroyed.Add(attacker);
                destroyed.Add(target);
            }
        }
        else
        {
            int diff = atk - target.DefValue;
            if (diff > 0)
            {
                destroyed.Add(target);
            }
            else if (diff < 0)
            {
                engine.Damage(attackerPlayer, -diff, target.Id);
            }
        }

        return destroyed;
    }

    private static void Finish(DuelEngine engine)
    {
        DuelState s = engine.State;
        s.DamageSubstep = DamageSubstep.None;
        s.Attacker = null;
        s.AttackTarget = null;
        s.BattleFlipped = null;
        TurnFlow.SetWindow(engine, Window.Open, null);
        if (!s.IsOver)
        {
            TurnFlow.SetBattleStep(engine, BattleStep.Battle);
        }
    }

    private static void SetSubstep(DuelEngine engine, DamageSubstep substep)
    {
        engine.State.DamageSubstep = substep;
        engine.Emit(new DamageSubstepChanged(substep));
    }
}
