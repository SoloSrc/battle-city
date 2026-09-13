using System;
using System.Collections.Generic;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Rules;

/// <summary>
/// The Damage Step and its sub-steps (systems.md §5.5): StartDamage,
/// BeforeCalc (face-down targets flip, ATK/DEF modifiers), Calc, AfterCalc
/// (flip effects, battle-destruction triggers), EndDamage. Tier 1 has no
/// responses, so the sub-steps run back to back and only emit events.
/// </summary>
internal static class DamageStep
{
    public static void Run(DuelEngine engine)
    {
        DuelState s = engine.State;
        CardInstance? attacker = s.Find(s.Attacker!.Value);
        CardInstance? target = s.AttackTarget is null ? null : s.Find(s.AttackTarget.Value);
        s.Attacker = null;
        s.AttackTarget = null;

        // A replay (attacker or target left the field during the Battle Step) ends the attack; tier 2 removal makes this reachable.
        if (attacker is null || attacker.Loc != Location.MonsterZone || (target is not null && target.Loc != Location.MonsterZone))
        {
            return;
        }

        TurnFlow.SetBattleStep(engine, BattleStep.Damage);
        SetSubstep(engine, DamageSubstep.StartDamage);

        SetSubstep(engine, DamageSubstep.BeforeCalc);
        if (target is { IsFaceDown: true })
        {
            target.Pos = Position.FaceUpDefense;
            engine.Emit(new MonsterFlipped(target.Controller, target.Id, target.Def.Id));
        }

        SetSubstep(engine, DamageSubstep.Calc);
        List<CardInstance> destroyed = Calculate(engine, attacker, target);

        SetSubstep(engine, DamageSubstep.AfterCalc);
        foreach (CardInstance card in destroyed)
        {
            engine.Emit(new MonsterDestroyed(card.Controller, card.Id, card.Def.Id, DestroyReason.Battle));
            Zones.ToGraveyard(engine, card);
        }

        SetSubstep(engine, DamageSubstep.EndDamage);
        s.DamageSubstep = DamageSubstep.None;
        if (!s.IsOver)
        {
            TurnFlow.SetBattleStep(engine, BattleStep.Battle);
        }
    }

    /// <summary>Damage calculation (GDD §3.2). Returns the monsters destroyed by battle.</summary>
    private static List<CardInstance> Calculate(DuelEngine engine, CardInstance attacker, CardInstance? target)
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

    private static void SetSubstep(DuelEngine engine, DamageSubstep substep)
    {
        engine.State.DamageSubstep = substep;
        engine.Emit(new DamageSubstepChanged(substep));
    }
}
