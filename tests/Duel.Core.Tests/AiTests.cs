using System.Linq;
using BattleCity.Duel.Core.Ai;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Data;
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
}
