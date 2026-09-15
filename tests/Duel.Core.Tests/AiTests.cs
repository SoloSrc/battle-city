using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Ai;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Data;
using BattleCity.Duel.Core.Effects;
using BattleCity.Duel.Core.Model;
using Xunit;

namespace BattleCity.Duel.Core.Tests;

public class AiTests
{
    private static readonly AiProfile _noJitter = AiProfile.Nico with { Jitter = 0.0 };

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    [InlineData(5UL)]
    public void HeuristicAgentsPlayAVanillaDuelToAWinner(ulong seed)
    {
        // Acceptance for issue #25: a scripted vanilla duel with the real tier 1 data runs to a winner.
        CardLibrary library = new CardLoader().LoadDirectory(Cards.DataDirectory);
        var monsters = library.All.Where(c => c.IsVanilla).ToList();
        Deck deck = Deck.Repeat(monsters, (Scenario.DeckSize + monsters.Count - 1) / monsters.Count);
        DuelEngine engine = DuelEngine.Start(deck, deck, new DuelOptions { Seed = seed });

        int steps = DuelRunner.Play(engine, new HeuristicAgent(AiProfile.Nico, seed), new HeuristicAgent(AiProfile.Mara, seed + 100));

        Assert.True(engine.State.IsOver, $"duel still running after {steps} commands");
        Assert.NotNull(engine.State.Winner);
        Assert.Equal(DuelOutcome.LifePoints, engine.State.Outcome);
        Assert.InRange(engine.State.TurnNumber, 3, 60);
    }

    [Fact]
    public void HeuristicSummonsTheStrongestMonster()
    {
        DuelEngine engine = Scenario.Start(0, new[] { Cards.Weakling, Cards.GeminiElf });
        var agent = new HeuristicAgent(_noJitter, 1);

        PlayerCommand choice = agent.Choose(engine, 0, engine.LegalActions(0));

        var summon = Assert.IsType<NormalSummon>(choice);
        Assert.Equal(Scenario.InHand(engine, 0, Cards.GeminiElf).Id, summon.Card);
    }

    [Fact]
    public void HeuristicEntersTheBattlePhaseAndAttacksWhenItWins()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);
        var agent = new HeuristicAgent(_noJitter, 1);

        // Summoning nothing: the hand is fillers weaker than the elf, but the agent still prefers the battle over ending the turn.
        PlayerCommand first = agent.Choose(engine, 0, engine.LegalActions(0));
        Assert.True(first is NormalSummon or EnterBattlePhase, first.ToString());
        Scenario.Submit(engine, first);
        if (first is NormalSummon)
        {
            Scenario.Submit(engine, agent.Choose(engine, 0, engine.LegalActions(0)));
        }

        Assert.Equal(BattleStep.Battle, engine.State.BattleStep);
        var attack = Assert.IsType<DeclareAttack>(agent.Choose(engine, 0, engine.LegalActions(0)));
        Assert.Equal(elf.Id, attack.Attacker);
        Assert.Equal(weakling.Id, attack.Target);
    }

    [Fact]
    public void HeuristicDoesNotAttackIntoAStrongerMonster()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        Scenario.Place(engine, 0, Cards.Weakling, Position.FaceUpAttack);
        Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        Scenario.EnterBattle(engine);
        var agent = new HeuristicAgent(_noJitter, 1);

        PlayerCommand choice = agent.Choose(engine, 0, engine.LegalActions(0));

        Assert.IsType<Pass>(choice);
    }

    [Fact]
    public void EvaluatorScoresAWonDuelHighest()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        engine.State.Player(1).LifePoints = 100;
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        Scenario.EnterBattle(engine);
        double before = HeuristicAgent.Evaluate(engine.State, 0, AiProfile.Mara);

        Scenario.Attack(engine, elf, null);

        Assert.True(HeuristicAgent.Evaluate(engine.State, 0, AiProfile.Mara) > before);
        Assert.Equal(1000.0, HeuristicAgent.Evaluate(engine.State, 0, AiProfile.Mara));
        Assert.Equal(-1000.0, HeuristicAgent.Evaluate(engine.State, 1, AiProfile.Mara));
    }

    [Fact]
    public void HeuristicActivatesATrapThatSavesItsMonster()
    {
        // Issue #59 response table: Sakuretsu-style trap against an attack that would destroy the agent's monster.
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);
        CardInstance trap = Scenario.Set(engine, 1, Cards.AttackTrap);
        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, elf, weakling);
        // The turn player has nothing but Pass in the attack window, so the engine passed for them.
        Assert.Equal(1, engine.State.Priority);
        var agent = new HeuristicAgent(AiProfile.Mara with { Jitter = 0.0 }, 1);

        PlayerCommand choice = agent.Choose(engine, 1, engine.LegalActions(1));

        var activation = Assert.IsType<ActivateTrap>(choice);
        Assert.Equal(trap.Id, activation.Card);
    }

    [Fact]
    public void HeuristicActivatesATrapThatPreventsADirectAttack()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance trap = Scenario.Set(engine, 1, Cards.AttackTrap);
        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, elf, null);
        Assert.Equal(1, engine.State.Priority);
        var agent = new HeuristicAgent(AiProfile.ArcadeOwner with { Jitter = 0.0 }, 1);

        PlayerCommand choice = agent.Choose(engine, 1, engine.LegalActions(1));

        Assert.Equal(trap.Id, Assert.IsType<ActivateTrap>(choice).Card);
    }

    [Fact]
    public void HeuristicKeepsItsTrapWhenTheAttackAlreadyLoses()
    {
        // The attacker dies in battle anyway: the trap saves nothing and prevents no damage, so it stays Set.
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance weakling = Scenario.Place(engine, 0, Cards.Weakling, Position.FaceUpAttack);
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        Scenario.Set(engine, 1, Cards.AttackTrap);
        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, weakling, elf);
        Assert.Equal(1, engine.State.Priority);
        var agent = new HeuristicAgent(AiProfile.Mara with { Jitter = 0.0 }, 1);

        PlayerCommand choice = agent.Choose(engine, 1, engine.LegalActions(1));

        Assert.IsType<Pass>(choice);
    }

    [Fact]
    public void HeuristicNegatesTheOpponentsDraw()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(top0: new[] { Cards.PotOfGreed });
        CardInstance counter = Scenario.Set(engine, 1, Cards.NegateTrap);
        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Cards.PotOfGreed).Id));
        Assert.Equal(1, engine.State.Priority);
        var agent = new HeuristicAgent(AiProfile.Mara with { Jitter = 0.0 }, 1);

        PlayerCommand choice = agent.Choose(engine, 1, engine.LegalActions(1));

        Assert.Equal(counter.Id, Assert.IsType<ActivateTrap>(choice).Card);
    }

    [Fact]
    public void HeuristicPaysCostsWithTheWeakestCardAndTargetsTheStrongestMonster()
    {
        // Destroyer: discard 1 card, destroy 1 monster the opponent controls (issue #59: costs by value, targets by the evaluator).
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(top0: new[] { Cards.SummonedSkull, Cards.Titan, Cards.Weakling, Cards.Filler, Cards.Filler, Cards.Filler });
        CardInstance destroyer = Scenario.Place(engine, 0, Cards.Destroyer, Position.FaceUpAttack);
        Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        var agent = new HeuristicAgent(AiProfile.Mara with { Jitter = 0.0 }, 1);
        Scenario.Submit(engine, new ActivateEffect(0, destroyer.Id, 0));

        Assert.Equal(ChoiceKind.Cost, engine.State.PendingChoice!.Kind);
        var cost = Assert.IsType<AnswerChoice>(agent.Choose(engine, 0, engine.LegalActions(0)));
        Assert.Equal(Cards.Filler, engine.State.Find(Assert.Single(cost.Selected))!.Def);
        Scenario.Submit(engine, cost);

        Assert.Equal(ChoiceKind.Target, engine.State.PendingChoice!.Kind);
        var target = Assert.IsType<AnswerChoice>(agent.Choose(engine, 0, engine.LegalActions(0)));
        Assert.Equal(elf.Id, Assert.Single(target.Selected));
    }

    [Fact]
    public void HeuristicActivatesAnIgnitionEffectWorthItsCost()
    {
        // From the open Main Phase the rollout answers the cost and target prompts itself, so the activation is scored on its best outcome.
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(top0: new[] { Cards.Filler, Cards.Filler, Cards.Filler, Cards.Filler, Cards.Filler, Cards.Filler });
        CardInstance destroyer = Scenario.Place(engine, 0, Cards.Destroyer, Position.FaceUpAttack);
        Scenario.Place(engine, 1, Cards.Titan, Position.FaceUpAttack);
        var agent = new HeuristicAgent(AiProfile.Mara with { Jitter = 0.0 }, 1);

        PlayerCommand choice = agent.Choose(engine, 0, engine.LegalActions(0));

        var activation = Assert.IsType<ActivateEffect>(choice);
        Assert.Equal(destroyer.Id, activation.Card);
    }

    [Fact]
    public void HeuristicTakesAnOptionalTriggerThatDraws()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(top0: new[] { Cards.Drawer });
        CardInstance drawer = Scenario.InHand(engine, 0, Cards.Drawer);
        Scenario.Submit(engine, new NormalSummon(0, drawer.Id));
        Assert.Equal(ChoiceKind.OptionalTrigger, engine.State.PendingChoice!.Kind);
        var agent = new HeuristicAgent(AiProfile.Nico with { Jitter = 0.0 }, 1);

        var answer = Assert.IsType<AnswerChoice>(agent.Choose(engine, 0, engine.LegalActions(0)));

        Assert.Equal(drawer.Id, Assert.Single(answer.Selected));
    }

    [Fact]
    public void HeuristicSetsTrapsButBluffsNormalSpellsOnlyWhenTheProfileSaysSo()
    {
        // Hand: a trap and a Normal Spell with no legal activation. Setting the trap is always worth it; Setting the spell is a decoy.
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(top0: new[] { Cards.AttackTrap, Cards.Banisher, Cards.Weakling, Cards.Weakling, Cards.Weakling, Cards.Weakling });
        Scenario.Place(engine, 0, Cards.Titan, Position.FaceUpAttack);
        var honest = new HeuristicAgent(AiProfile.Nico with { Jitter = 0.0, BluffSet = 0.0 }, 1);
        var bluffer = new HeuristicAgent(AiProfile.Nico with { Jitter = 0.0, BluffSet = 1.0 }, 1);
        CardInstance trap = Scenario.InHand(engine, 0, Cards.AttackTrap);
        CardInstance spell = Scenario.InHand(engine, 0, Cards.Banisher);
        Scenario.Submit(engine, new NormalSummon(0, Scenario.InHand(engine, 0, Cards.Weakling).Id));
        Scenario.PassUntil(engine, s => s.Window == Window.Open);

        var set = Assert.IsType<SetSpellTrap>(honest.Choose(engine, 0, engine.LegalActions(0)));
        Assert.Equal(trap.Id, set.Card);
        Scenario.Submit(engine, set);

        // Nothing else to do but attack: the honest agent keeps the spell in hand, the bluffer Sets it.
        Assert.IsType<EnterBattlePhase>(honest.Choose(engine, 0, engine.LegalActions(0)));
        var decoy = Assert.IsType<SetSpellTrap>(bluffer.Choose(engine, 0, engine.LegalActions(0)));
        Assert.Equal(spell.Id, decoy.Card);
    }

    [Fact]
    public void StaticPolicyPaysCostsLowAndTargetsHigh()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(top0: new[] { Cards.SummonedSkull, Cards.Weakling, Cards.PotOfGreed });
        CardInstance skull = Scenario.InHand(engine, 0, Cards.SummonedSkull);
        CardInstance weakling = Scenario.InHand(engine, 0, Cards.Weakling);
        CardInstance pot = Scenario.InHand(engine, 0, Cards.PotOfGreed);
        IReadOnlyList<Guid> hand = new[] { skull.Id, weakling.Id, pot.Id };
        CardInstance titan = Scenario.Place(engine, 1, Cards.Titan, Position.FaceUpAttack);
        CardInstance wall = Scenario.Place(engine, 1, Cards.Wall, Position.FaceUpDefense);

        AnswerChoice cost = HeuristicAgent.StaticAnswer(engine.State, new PendingChoice(0, ChoiceKind.Cost, null, new Choice("Discard 1 card", hand, 1, 1)));
        AnswerChoice target = HeuristicAgent.StaticAnswer(engine.State, new PendingChoice(0, ChoiceKind.Target, null, new Choice("Destroy 1 monster", new[] { titan.Id, wall.Id }, 1, 1)));
        AnswerChoice theirs = HeuristicAgent.StaticAnswer(engine.State, new PendingChoice(0, ChoiceKind.Resolution, null, new Choice("Banish up to 2", new[] { titan.Id, wall.Id }, 1, 2)));

        Assert.Equal(weakling.Id, Assert.Single(cost.Selected));
        Assert.Equal(titan.Id, Assert.Single(target.Selected));
        Assert.Equal(2, theirs.Selected.Count);
    }

    [Fact]
    public void EvaluatorDoesNotPeekAtTheOpponentsFaceDownMonster()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        Scenario.Place(engine, 1, Cards.Wall, Position.FaceDownDefense);
        double withWall = HeuristicAgent.Evaluate(engine.State, 0, AiProfile.Mara);
        engine.State.Player(1).MonsterZones[0] = null;
        Scenario.Place(engine, 1, Cards.Weakling, Position.FaceDownDefense);

        Assert.Equal(withWall, HeuristicAgent.Evaluate(engine.State, 0, AiProfile.Mara));
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void HeuristicBeatsRandomPlayWithTheSameDeck(ulong seed)
    {
        // Issue #59: the heuristic agent wins against random play (the harness in tools/duel_sim measures the rate over many seeds).
        Deck deck = TierThreeTests.RealDeck("starter");
        DuelEngine engine = DuelEngine.Start(deck, deck, new DuelOptions { Seed = seed });

        DuelRunner.Play(engine, new HeuristicAgent(AiProfile.Mara, seed), new RandomAgent(seed + 100));

        Assert.True(engine.State.IsOver);
        Assert.Equal(0, engine.State.Winner);
    }
}
