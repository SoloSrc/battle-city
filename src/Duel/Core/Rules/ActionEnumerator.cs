using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Rules;

/// <summary>Builds <see cref="DuelEngine.LegalActions"/>: every candidate is validated by the same rule that <see cref="DuelEngine.Submit"/> uses.</summary>
internal static class ActionEnumerator
{
    public static IReadOnlyList<PlayerCommand> Enumerate(DuelEngine engine, int player)
    {
        DuelState s = engine.State;
        var actions = new List<PlayerCommand>();
        if (s.IsOver || player is < 0 or > 1 || player != s.Priority)
        {
            return actions;
        }

        PlayerState p = s.Player(player);
        if (TurnFlow.HandLimitPending(s, engine.Options) && player == s.TurnPlayer)
        {
            actions.AddRange(p.Hand.Select(c => new Discard(player, c.Id)));
            return actions;
        }

        actions.Add(new Pass(player));
        if (player != s.TurnPlayer || s.Chain.Count > 0 || s.Attacker is not null)
        {
            // Responses (Quick-Play Spells, Traps, Quick effects) arrive with tier 2.
            return actions;
        }

        if (TurnFlow.IsMainPhase(s))
        {
            AddMainPhaseActions(engine, player, actions);
        }
        else if (s.Phase == Phase.Battle && s.BattleStep == BattleStep.Battle)
        {
            AddAttacks(s, player, actions);
        }

        return actions;
    }

    private static void AddMainPhaseActions(DuelEngine engine, int player, List<PlayerCommand> actions)
    {
        DuelState s = engine.State;
        PlayerState p = s.Player(player);
        var monsters = p.Monsters.ToList();

        foreach (CardInstance card in p.Hand)
        {
            if (card.Def.Kind == CardKind.Monster && !s.NormalSummonUsed)
            {
                foreach (IReadOnlyList<Guid> tributes in TributeSets(monsters, card.Def.Monster!.TributesRequired))
                {
                    Add(engine, actions, new NormalSummon(player, card.Id, tributes));
                    Add(engine, actions, new SetMonster(player, card.Id, tributes));
                }
            }

            if (card.Def.IsSpell)
            {
                Add(engine, actions, new ActivateSpell(player, card.Id));
            }

            if (card.Def.IsSpell || card.Def.IsTrap)
            {
                Add(engine, actions, new SetSpellTrap(player, card.Id));
            }
        }

        foreach (CardInstance card in monsters)
        {
            Add(engine, actions, card.IsFaceDown ? new FlipSummon(player, card.Id) : new ChangePosition(player, card.Id));
        }

        foreach (CardInstance card in p.SpellTraps)
        {
            if (card.IsFaceDown && card.Def.IsSpell)
            {
                Add(engine, actions, new ActivateSpell(player, card.Id));
            }
        }

        Add(engine, actions, new EnterBattlePhase(player));
    }

    private static void AddAttacks(DuelState s, int player, List<PlayerCommand> actions)
    {
        var targets = s.Opponent(player).Monsters.ToList();
        foreach (CardInstance attacker in s.Player(player).Monsters)
        {
            if (attacker.Pos != Position.FaceUpAttack || attacker.AttackedThisTurn)
            {
                continue;
            }

            if (targets.Count == 0)
            {
                actions.Add(new DeclareAttack(player, attacker.Id, null));
            }
            else
            {
                actions.AddRange(targets.Select(t => new DeclareAttack(player, attacker.Id, t.Id)));
            }
        }
    }

    /// <summary>Every way to pick <paramref name="count"/> distinct tributes; one empty set when none are needed.</summary>
    private static IEnumerable<IReadOnlyList<Guid>> TributeSets(List<CardInstance> monsters, int count)
    {
        if (count == 0)
        {
            yield return Array.Empty<Guid>();
            yield break;
        }

        if (count == 1)
        {
            foreach (CardInstance m in monsters)
            {
                yield return new[] { m.Id };
            }

            yield break;
        }

        for (int i = 0; i < monsters.Count; i++)
        {
            for (int j = i + 1; j < monsters.Count; j++)
            {
                yield return new[] { monsters[i].Id, monsters[j].Id };
            }
        }
    }

    private static void Add(DuelEngine engine, List<PlayerCommand> actions, PlayerCommand command)
    {
        if (engine.Validate(command) is null)
        {
            actions.Add(command);
        }
    }
}
