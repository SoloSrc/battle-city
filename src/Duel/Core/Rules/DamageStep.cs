using System;
using System.Collections.Generic;
using System.Linq;
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
        if (target is { IsFaceDown: true } && attacker.Has(Restriction.DestroysFaceDownTargets))
        {
            // Mystic Swordsman LV2: the face-down target is destroyed without being flipped; no damage calculation follows.
            engine.Destroy(target, DestroyReason.Effect);
            target = null;
        }

        SetSubstep(engine, DamageSubstep.BeforeCalc);
        if (target is { IsFaceDown: true })
        {
            target.Pos = Position.FaceUpDefense;
            target.FlippedThisTurn = true;
            s.BattleFlipped = target.Id;
            engine.Emit(new MonsterFlipped(target.Controller, target.Id, target.Def.Id));
            engine.Refresh();
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
        var damagers = new List<CardInstance>();
        bool fought = attacker is { Loc: Location.MonsterZone } && (target is null || target.Loc == Location.MonsterZone);
        if (fought)
        {
            destroyed = Resolve(engine, attacker!, target, damagers);
        }

        SetSubstep(engine, DamageSubstep.AfterCalc);
        // A monster destroyed by Dark Balter loses every trigger; one destroyed by a lone Blade Knight only its Flip Effect.
        var silenced = new HashSet<Guid>();
        bool flipNegated = false;
        foreach (CardInstance card in destroyed.Where(c => !c.Has(Restriction.CannotBeDestroyedByBattle)))
        {
            CardInstance? other = card == attacker ? target : attacker;
            bool negated = other is not null && other.Has(Restriction.NegatesEffectsOfDestroyed);
            if (negated)
            {
                silenced.Add(card.Id);
            }

            if (card.Id == s.BattleFlipped && (negated || (other is not null && other.Has(Restriction.NegatesFlipEffectsOfDestroyed))))
            {
                flipNegated = true;
            }

            engine.Destroy(card, DestroyReason.Battle, other?.Id, negated);
        }

        if (s.BattleFlipped is { } flipped && s.Find(flipped) is { } flippedCard && !flipNegated)
        {
            engine.QueueTriggers(flippedCard, TriggerWindow.OnFlip);
        }

        if (fought && !s.IsOver)
        {
            if (target is not null)
            {
                foreach ((CardInstance card, CardInstance other) in new[] { (attacker!, target), (target, attacker!) })
                {
                    if (!silenced.Contains(card.Id))
                    {
                        engine.QueueTriggers(card, TriggerWindow.OnBattle, battled: other.Id);
                    }
                }
            }

            foreach (CardInstance card in damagers.Where(c => !silenced.Contains(c.Id)))
            {
                engine.QueueTriggers(card, TriggerWindow.OnBattleDamage, battled: card == attacker ? target?.Id : attacker!.Id);
            }
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

    /// <summary>Damage calculation (GDD §3.2): a piercing attacker inflicts the difference over a Defense Position target. Returns the monsters battle would destroy; <paramref name="damagers"/> collects the monsters that inflicted battle damage.</summary>
    private static List<CardInstance> Resolve(DuelEngine engine, CardInstance attacker, CardInstance? target, List<CardInstance> damagers)
    {
        var destroyed = new List<CardInstance>();
        int attackerPlayer = attacker.Controller;
        int atk = attacker.Atk;

        if (target is null)
        {
            if (engine.Damage(1 - attackerPlayer, atk, attacker.Id))
            {
                damagers.Add(attacker);
            }

            return destroyed;
        }

        int defenderPlayer = target.Controller;
        if (target.IsInAttackPosition)
        {
            int diff = atk - target.Atk;
            if (diff > 0)
            {
                destroyed.Add(target);
                if (engine.Damage(defenderPlayer, diff, attacker.Id))
                {
                    damagers.Add(attacker);
                }
            }
            else if (diff < 0)
            {
                destroyed.Add(attacker);
                if (engine.Damage(attackerPlayer, -diff, target.Id))
                {
                    damagers.Add(target);
                }
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
                if (attacker.Has(Restriction.Piercing) && engine.Damage(defenderPlayer, diff, attacker.Id))
                {
                    damagers.Add(attacker);
                }
            }
            else if (diff < 0 && engine.Damage(attackerPlayer, -diff, target.Id))
            {
                damagers.Add(target);
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
