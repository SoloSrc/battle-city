using System;
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

        if (attacker.AttackedThisTurn)
        {
            return $"{attacker.Def.Name} already attacked this turn";
        }

        PlayerState opponent = s.Opponent(player);
        if (targetId is null)
        {
            return opponent.MonsterCount == 0 ? null : "direct attacks are only possible when the opponent controls no monsters";
        }

        return Zones.OnField(s, 1 - player, targetId.Value) is null ? "the target is not an opponent's monster" : null;
    }

    public static void DeclareAttack(DuelEngine engine, int player, Guid attackerId, Guid? targetId)
    {
        DuelState s = engine.State;
        CardInstance attacker = Zones.OnField(s, player, attackerId)!;
        attacker.AttackedThisTurn = true;
        s.Attacker = attackerId;
        s.AttackTarget = targetId;
        engine.Emit(new AttackDeclared(player, attackerId, targetId));
        // Both players may respond to the declaration, turn player first; two passes on an empty chain enter the Damage Step.
        TurnFlow.SetWindow(engine, Window.AttackDeclared, attackerId);
        TurnFlow.GivePriorityToTurnPlayer(s);
    }
}
