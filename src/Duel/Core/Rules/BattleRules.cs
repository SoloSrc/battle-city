using System;
using System.Linq;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Rules;

/// <summary>Battle Phase entry and attack declaration (GDD §3.2, systems.md §5.5).</summary>
internal static class BattleRules
{
    public static string? ValidateEnterBattlePhase(DuelState s, int player)
    {
        if (player != s.TurnPlayer)
        {
            return "only the turn player may enter the Battle Phase";
        }

        if (s.Phase != Phase.Main1)
        {
            return "the Battle Phase follows Main Phase 1";
        }

        if (s.Chain.Count > 0)
        {
            return "not while a chain is being built";
        }

        if (s.Window != Window.Open)
        {
            return "not while a window is open for responses";
        }

        if (s.FirstTurn)
        {
            return "the player who goes first cannot attack in their first turn";
        }

        if (s.BattlePhaseUsed)
        {
            return "the Battle Phase was already conducted this turn";
        }

        return null;
    }

    public static void EnterBattlePhase(DuelEngine engine, int player)
    {
        DuelState s = engine.State;
        s.BattlePhaseUsed = true;
        TurnFlow.SetPhase(engine, Phase.Battle);
        engine.Emit(new BattlePhaseEntered(player));
        TurnFlow.SetBattleStep(engine, BattleStep.Start);
        TurnFlow.GivePriorityToTurnPlayer(s);
    }

    public static string? ValidateAttack(DuelState s, int player, Guid attackerId, Guid? targetId)
    {
        if (player != s.TurnPlayer)
        {
            return "only the turn player may attack";
        }

        if (s.Phase != Phase.Battle || s.BattleStep != BattleStep.Battle)
        {
            return "attacks are declared in the Battle Step";
        }

        if (s.Attacker is not null)
        {
            return "an attack is already in progress";
        }

        if (s.Chain.Count > 0)
        {
            return "not while a chain is being built";
        }

        if (s.Window != Window.Open)
        {
            return "not while a window is open for responses";
        }

        CardInstance? attacker = Zones.OnField(s, player, attackerId);
        if (attacker is null)
        {
            return "the attacker is not on your field";
        }

        if (attacker.Pos != Position.FaceUpAttack)
        {
            return "only a face-up Attack Position monster can attack";
        }

        if (attacker.AttackedThisTurn && !(attacker.Has(Restriction.AttacksEveryMonster) && targetId is { } again && !attacker.AttackTargetsThisTurn.Contains(again)))
        {
            return $"{attacker.Def.Name} already attacked this turn";
        }

        if (attacker.Has(Restriction.CannotAttack))
        {
            return $"{attacker.Def.Name} cannot attack";
        }

        PlayerState opponent = s.Opponent(player);
        if (targetId is null)
        {
            return opponent.MonsterCount == 0 || attacker.Has(Restriction.CanAttackDirectly) ? null : "direct attacks are only possible when the opponent controls no monsters";
        }

        CardInstance? target = Zones.OnField(s, 1 - player, targetId.Value);
        if (target is null)
        {
            return "the target is not an opponent's monster";
        }

        return target.Has(Restriction.CannotBeAttacked) ? $"{target.Def.Name} cannot be attacked" : null;
    }

    /// <summary>
    /// A monster of <paramref name="player"/> that must attack and still can (Berserk Gorilla): its
    /// controller may not pass over the attack. In Main Phase 1 that means the Battle Phase can
    /// still be entered; in the Battle Step that an attack can be declared now. Null when nothing forces an attack.
    /// </summary>
    public static CardInstance? MustAttackWith(DuelState s, int player)
    {
        if (player != s.TurnPlayer || s.Chain.Count > 0 || s.Window != Window.Open)
        {
            return null;
        }

        bool canEnter = s.Phase == Phase.Main1 && ValidateEnterBattlePhase(s, player) is null;
        bool canDeclare = s.Phase == Phase.Battle && s.BattleStep == BattleStep.Battle && s.Attacker is null;
        if (!canEnter && !canDeclare)
        {
            return null;
        }

        return s.Player(player).Monsters.FirstOrDefault(m => m.Has(Restriction.MustAttack) && CanAttackNow(s, m));
    }

    /// <summary>Whether <paramref name="attacker"/> could declare some attack now, ignoring the phase: face-up in Attack Position, not spent, not forbidden, with a legal target or a direct attack.</summary>
    public static bool CanAttackNow(DuelState s, CardInstance attacker)
    {
        if (attacker.Pos != Position.FaceUpAttack || attacker.AttackedThisTurn || attacker.Has(Restriction.CannotAttack))
        {
            return false;
        }

        PlayerState opponent = s.Opponent(attacker.Controller);
        return opponent.MonsterCount == 0 || attacker.Has(Restriction.CanAttackDirectly) || opponent.Monsters.Any(t => !t.Has(Restriction.CannotBeAttacked));
    }

    /// <summary>The Battle Phase ends: monsters flagged <see cref="Restriction.DefenseAfterAttack"/> that attacked switch to Defense Position and stay locked until the end of their controller's next turn (Goblin Attack Force).</summary>
    public static void EndOfBattlePhase(DuelEngine engine)
    {
        DuelState s = engine.State;
        foreach (CardInstance card in s.Players.SelectMany(p => p.Monsters).Where(m => m.Has(Restriction.DefenseAfterAttack) && m.AttackedThisTurn && m.Pos == Position.FaceUpAttack).ToList())
        {
            card.Pos = Position.FaceUpDefense;
            engine.Emit(new PositionChanged(card.Controller, card.Id, Position.FaceUpAttack, Position.FaceUpDefense));
            engine.AddModifier(Modifier.OnCard(ModifierKind.CannotChangePosition, card.Id, card.Id, 0, s.NextTurnOf(card.Controller)));
        }

        engine.Refresh();
    }

    public static void DeclareAttack(DuelEngine engine, int player, Guid attackerId, Guid? targetId)
    {
        DuelState s = engine.State;
        CardInstance attacker = Zones.OnField(s, player, attackerId)!;
        attacker.AttackedThisTurn = true;
        if (targetId is { } target)
        {
            attacker.AttackTargetsThisTurn.Add(target);
        }

        s.Attacker = attackerId;
        s.AttackTarget = targetId;
        engine.Emit(new AttackDeclared(player, attackerId, targetId));
        // Both players may respond to the declaration, turn player first; two passes on an empty chain enter the Damage Step.
        TurnFlow.SetWindow(engine, Window.AttackDeclared, attackerId);
        TurnFlow.GivePriorityToTurnPlayer(s);
    }
}
