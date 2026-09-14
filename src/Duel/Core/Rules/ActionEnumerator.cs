using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Effects;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Rules;

/// <summary>Builds <see cref="DuelEngine.LegalActions"/>: every candidate is validated by the same rule that <see cref="DuelEngine.Submit"/> uses.</summary>
internal static class ActionEnumerator
{
    public static IReadOnlyList<PlayerCommand> Enumerate(DuelEngine engine, int player)
    {
        DuelState s = engine.State;
        var actions = new List<PlayerCommand>();
        if (s.IsOver || player is < 0 or > 1)
        {
            return actions;
        }

        if (s.PendingChoice is { } pending)
        {
            if (pending.Player == player)
            {
                AddAnswers(engine, player, pending.Choice, actions);
            }

            return actions;
        }

        if (player != s.Priority)
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
        AddResponses(engine, player, actions);
        if (player != s.TurnPlayer || s.Chain.Count > 0)
        {
            return actions;
        }

        if (TurnFlow.IsMainPhase(s) && s.Window is Window.Open or Window.Summon)
        {
            // Ignition effects also under summon priority (systems.md §5.5); everything else needs the open Main Phase.
            AddIgnitionEffects(engine, player, actions);
        }

        if (s.Window != Window.Open)
        {
            return actions;
        }

        if (TurnFlow.IsMainPhase(s))
        {
            AddMainPhaseActions(engine, player, actions);
        }
        else if (s.Phase == Phase.Battle && s.BattleStep == BattleStep.Battle)
        {
            AddAttacks(engine, player, actions);
        }

        return actions;
    }

    /// <summary>Every valid answer: each selection of <c>Min</c> to <c>Max</c> options, in option order.</summary>
    private static void AddAnswers(DuelEngine engine, int player, Choice choice, List<PlayerCommand> actions)
    {
        for (int size = choice.Min; size <= Math.Min(choice.Max, choice.Options.Count); size++)
        {
            foreach (IReadOnlyList<Guid> selection in Combinations(choice.Options, size))
            {
                Add(engine, actions, new AnswerChoice(player, selection));
            }
        }
    }

    /// <summary>Speed 2 and 3 activations available to whoever holds priority: Set Traps, Quick-Play Spells and monster Quick effects.</summary>
    private static void AddResponses(DuelEngine engine, int player, List<PlayerCommand> actions)
    {
        DuelState s = engine.State;
        PlayerState p = s.Player(player);
        foreach (CardInstance card in p.SpellTraps)
        {
            if (!card.IsFaceDown)
            {
                continue;
            }

            if (card.Def.IsTrap)
            {
                Add(engine, actions, new ActivateTrap(player, card.Id));
            }
            else if (card.Def.Spell?.Subtype == SpellSubtype.Quick)
            {
                Add(engine, actions, new ActivateSpell(player, card.Id));
            }
        }

        foreach (CardInstance card in p.Hand)
        {
            if (card.Def.Spell?.Subtype == SpellSubtype.Quick)
            {
                Add(engine, actions, new ActivateSpell(player, card.Id));
            }
        }

        foreach (CardInstance card in p.Monsters.Where(m => m.IsFaceUp))
        {
            IReadOnlyList<IEffect> effects = engine.EffectsOf(card);
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].Kind == EffectKind.Quick)
                {
                    Add(engine, actions, new ActivateEffect(player, card.Id, i));
                }
            }
        }
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

            if (card.Def.Spell is { Subtype: not SpellSubtype.Quick })
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
            if (card.IsFaceDown && card.Def.Spell is { Subtype: not SpellSubtype.Quick })
            {
                Add(engine, actions, new ActivateSpell(player, card.Id));
            }
        }

        Add(engine, actions, new EnterBattlePhase(player));
    }

    private static void AddIgnitionEffects(DuelEngine engine, int player, List<PlayerCommand> actions)
    {
        foreach (CardInstance card in engine.State.Player(player).Monsters.Where(m => m.IsFaceUp))
        {
            IReadOnlyList<IEffect> effects = engine.EffectsOf(card);
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].Kind == EffectKind.Ignition)
                {
                    Add(engine, actions, new ActivateEffect(player, card.Id, i));
                }
            }
        }
    }

    private static void AddAttacks(DuelEngine engine, int player, List<PlayerCommand> actions)
    {
        DuelState s = engine.State;
        var targets = s.Opponent(player).Monsters.ToList();
        foreach (CardInstance attacker in s.Player(player).Monsters)
        {
            if (attacker.Pos != Position.FaceUpAttack || attacker.AttackedThisTurn)
            {
                continue;
            }

            if (targets.Count == 0 || attacker.Has(Restriction.CanAttackDirectly))
            {
                Add(engine, actions, new DeclareAttack(player, attacker.Id, null));
            }

            foreach (CardInstance target in targets)
            {
                Add(engine, actions, new DeclareAttack(player, attacker.Id, target.Id));
            }
        }
    }

    /// <summary>Every way to pick <paramref name="count"/> distinct tributes; one empty set when none are needed.</summary>
    private static IEnumerable<IReadOnlyList<Guid>> TributeSets(List<CardInstance> monsters, int count) =>
        Combinations(monsters.Select(m => m.Id).ToList(), count);

    /// <summary>Every selection of <paramref name="size"/> distinct items, in item order; one empty selection for size 0.</summary>
    private static IEnumerable<IReadOnlyList<Guid>> Combinations(IReadOnlyList<Guid> items, int size)
    {
        if (size == 0)
        {
            yield return Array.Empty<Guid>();
            yield break;
        }

        for (int i = 0; i + size <= items.Count; i++)
        {
            foreach (IReadOnlyList<Guid> rest in Combinations(items.Skip(i + 1).ToList(), size - 1))
            {
                var selection = new List<Guid>(size) { items[i] };
                selection.AddRange(rest);
                yield return selection;
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
