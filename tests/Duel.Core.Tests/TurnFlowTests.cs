using System;
using System.Linq;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;
using Xunit;

namespace BattleCity.Duel.Core.Tests;

public class TurnFlowTests
{
    [Fact]
    public void FirstPlayerDrawsOnTurnOne()
    {
        DuelEngine engine = Scenario.Start(0);

        Assert.Equal(1, engine.State.TurnNumber);
        Assert.Equal(0, engine.State.TurnPlayer);
        Assert.Equal(6, engine.State.Player(0).Hand.Count);
        Assert.Equal(5, engine.State.Player(1).Hand.Count);
        Assert.Equal(Phase.Main1, engine.State.Phase);
        Assert.Equal(0, engine.State.Priority);
        Assert.Contains(engine.Events, e => e is TurnStarted { Player: 0, Turn: 1 });
        Assert.Equal(new[] { Phase.Draw, Phase.Standby, Phase.Main1 }, engine.Events.OfType<PhaseChanged>().Select(e => e.Phase));
    }

    [Fact]
    public void CoinFlipIsSeeded()
    {
        var options = new DuelOptions { Seed = 42, Shuffle = false };
        DuelEngine a = DuelEngine.Start(Scenario.DeckWithTop(), Scenario.DeckWithTop(), options);
        DuelEngine b = DuelEngine.Start(Scenario.DeckWithTop(), Scenario.DeckWithTop(), options);

        Assert.Equal(a.State.TurnPlayer, b.State.TurnPlayer);
        Assert.Contains(a.Events, e => e is DuelStarted { Seed: 42 });
    }

    [Fact]
    public void DeckSizeIsValidated()
    {
        var small = new Deck(Enumerable.Repeat(Cards.Filler, 39).ToList());
        Assert.Throws<ArgumentException>(() => DuelEngine.Start(small, Scenario.DeckWithTop()));
    }

    [Fact]
    public void FirstPlayerCannotEnterBattlePhaseOnTurnOne()
    {
        DuelEngine engine = Scenario.Start(0);

        Assert.DoesNotContain(engine.LegalActions(0), a => a is EnterBattlePhase);
        Assert.Equal("the player who goes first cannot attack in their first turn", engine.Validate(new EnterBattlePhase(0)));
    }

    [Fact]
    public void SecondPlayerCanEnterBattlePhaseOnTheirFirstTurn()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();

        Assert.Equal(2, engine.State.TurnNumber);
        Assert.Contains(engine.LegalActions(0), a => a is EnterBattlePhase);
    }

    [Fact]
    public void PassingMainPhaseOneSkipsBattleAndEndsTheTurn()
    {
        DuelEngine engine = Scenario.Start(0);

        Scenario.Submit(engine, new Pass(0));

        Assert.Equal(2, engine.State.TurnNumber);
        Assert.Equal(1, engine.State.TurnPlayer);
        Assert.Equal(Phase.Main1, engine.State.Phase);
        Assert.Equal(6, engine.State.Player(1).Hand.Count);
        Assert.DoesNotContain(engine.Events, e => e is PhaseChanged { Phase: Phase.Battle } or PhaseChanged { Phase: Phase.Main2 });
    }

    [Fact]
    public void OnlyThePriorityPlayerMaySubmit()
    {
        DuelEngine engine = Scenario.Start(0);
        int events = engine.Events.Count;

        SubmitResult result = engine.Submit(new Pass(1));

        Assert.False(result.Accepted);
        Assert.Equal("player 1 does not have priority", result.Error);
        Assert.Equal(events, engine.Events.Count);
        Assert.Empty(engine.LegalActions(1));
    }

    [Fact]
    public void OneNormalSummonPerTurn()
    {
        DuelEngine engine = Scenario.Start(0, new[] { Cards.GeminiElf, Cards.ArchfiendSoldier });
        CardInstance elf = Scenario.InHand(engine, 0, Cards.GeminiElf);
        CardInstance soldier = Scenario.InHand(engine, 0, Cards.ArchfiendSoldier);

        Scenario.Submit(engine, new NormalSummon(0, elf.Id));
        SubmitResult second = engine.Submit(new NormalSummon(0, soldier.Id));

        Assert.Equal(Location.MonsterZone, elf.Loc);
        Assert.Equal(Position.FaceUpAttack, elf.Pos);
        Assert.Same(elf, engine.State.Player(0).MonsterZones[0]);
        Assert.False(second.Accepted);
        Assert.Equal("only one Normal Summon or Set per turn", second.Error);
        Assert.Equal(0, engine.State.Priority);
    }

    [Fact]
    public void SettingAMonsterUsesTheNormalSummon()
    {
        DuelEngine engine = Scenario.Start(0, new[] { Cards.GeminiElf });
        CardInstance elf = Scenario.InHand(engine, 0, Cards.GeminiElf);

        Scenario.Submit(engine, new SetMonster(0, elf.Id));

        Assert.Equal(Position.FaceDownDefense, elf.Pos);
        Assert.True(elf.SetThisTurn);
        Assert.True(engine.State.NormalSummonUsed);
        Assert.Contains(engine.Events, e => e is MonsterSet { Player: 0 });
    }

    [Fact]
    public void LevelFiveAndSixNeedOneTribute()
    {
        DuelEngine engine = Scenario.Start(0, new[] { Cards.SummonedSkull });
        CardInstance skull = Scenario.InHand(engine, 0, Cards.SummonedSkull);
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);

        SubmitResult withoutTribute = engine.Submit(new NormalSummon(0, skull.Id));
        Scenario.Submit(engine, new NormalSummon(0, skull.Id, new[] { elf.Id }));

        Assert.False(withoutTribute.Accepted);
        Assert.Equal("Summoned Skull needs 1 tribute(s), 0 given", withoutTribute.Error);
        Assert.Equal(Location.Graveyard, elf.Loc);
        Assert.Equal(Location.MonsterZone, skull.Loc);
        Assert.Equal(1, engine.State.Player(0).MonsterCount);
    }

    [Fact]
    public void LevelSevenAndUpNeedTwoTributes()
    {
        DuelEngine engine = Scenario.Start(0, new[] { Cards.Titan });
        CardInstance titan = Scenario.InHand(engine, 0, Cards.Titan);
        CardInstance a = Scenario.Place(engine, 0, Cards.Filler, Position.FaceUpAttack);
        CardInstance b = Scenario.Place(engine, 0, Cards.Filler, Position.FaceUpDefense);

        SubmitResult one = engine.Submit(new NormalSummon(0, titan.Id, new[] { a.Id }));
        SubmitResult same = engine.Submit(new NormalSummon(0, titan.Id, new[] { a.Id, a.Id }));
        Scenario.Submit(engine, new NormalSummon(0, titan.Id, new[] { a.Id, b.Id }));

        Assert.Equal("Titan needs 2 tribute(s), 1 given", one.Error);
        Assert.Equal("a monster cannot be tributed twice", same.Error);
        Assert.Equal(2, engine.State.Player(0).Graveyard.Count);
        Assert.Single(engine.State.Player(0).Monsters);
    }

    [Fact]
    public void LegalActionsListTributeCombinations()
    {
        DuelEngine engine = Scenario.Start(0, new[] { Cards.Titan });
        Scenario.Place(engine, 0, Cards.Filler, Position.FaceUpAttack);
        Scenario.Place(engine, 0, Cards.Filler, Position.FaceUpAttack);
        Scenario.Place(engine, 0, Cards.Filler, Position.FaceUpAttack);

        var summons = engine.LegalActions(0).OfType<NormalSummon>().Where(s => s.Tributes.Count == 2).ToList();

        Assert.Equal(3, summons.Count);
    }

    [Fact]
    public void PositionChangesFollowTheTimingRules()
    {
        DuelEngine engine = Scenario.Start(0, new[] { Cards.GeminiElf });
        CardInstance elf = Scenario.InHand(engine, 0, Cards.GeminiElf);
        Scenario.Submit(engine, new NormalSummon(0, elf.Id));

        SubmitResult sameTurn = engine.Submit(new ChangePosition(0, elf.Id));
        Scenario.Submit(engine, new Pass(0)); // turn 2, player 1
        Scenario.Submit(engine, new Pass(1)); // turn 3, player 0
        Scenario.Submit(engine, new ChangePosition(0, elf.Id));
        SubmitResult twice = engine.Submit(new ChangePosition(0, elf.Id));

        Assert.Equal("a monster cannot change position on the turn it was Summoned or Set", sameTurn.Error);
        Assert.Equal(Position.FaceUpDefense, elf.Pos);
        Assert.Equal("a monster changes position once per turn", twice.Error);
    }

    [Fact]
    public void FlipSummonTurnsASetMonsterFaceUpInAttackPosition()
    {
        DuelEngine engine = Scenario.Start(0, new[] { Cards.GeminiElf });
        CardInstance elf = Scenario.InHand(engine, 0, Cards.GeminiElf);
        Scenario.Submit(engine, new SetMonster(0, elf.Id));

        SubmitResult sameTurn = engine.Submit(new FlipSummon(0, elf.Id));
        Scenario.Submit(engine, new Pass(0));
        Scenario.Submit(engine, new Pass(1));
        Scenario.Submit(engine, new FlipSummon(0, elf.Id));

        Assert.False(sameTurn.Accepted);
        Assert.Equal(Position.FaceUpAttack, elf.Pos);
        Assert.Contains(engine.Events, e => e is MonsterFlipSummoned { Player: 0 });
        Assert.Equal("a monster changes position once per turn", engine.Validate(new ChangePosition(0, elf.Id)));
    }

    [Fact]
    public void PotOfGreedDrawsTwoAndGoesToTheGraveyard()
    {
        DuelEngine engine = Scenario.Start(0, new[] { Cards.PotOfGreed });
        CardInstance pot = Scenario.InHand(engine, 0, Cards.PotOfGreed);

        Scenario.Submit(engine, new ActivateSpell(0, pot.Id));

        Assert.Equal(7, engine.State.Player(0).Hand.Count);
        Assert.Equal(Location.Graveyard, pot.Loc);
        Assert.Empty(engine.State.Chain);
        Assert.Equal(0, engine.State.Priority);
        Assert.Contains(engine.Events, e => e is ChainLinkAdded { Link: 1, EffectId: "pot_of_greed" });
        Assert.Contains(engine.Events, e => e is ChainLinkResolved { Link: 1 });
    }

    [Fact]
    public void ASetNormalSpellCanBeActivatedTheSameTurn()
    {
        DuelEngine engine = Scenario.Start(0, new[] { Cards.PotOfGreed });
        CardInstance pot = Scenario.InHand(engine, 0, Cards.PotOfGreed);

        Scenario.Submit(engine, new SetSpellTrap(0, pot.Id));
        Assert.Equal(Position.FaceDown, pot.Pos);
        Assert.Equal(Location.SpellTrapZone, pot.Loc);
        Assert.Contains(engine.LegalActions(0), a => a is ActivateSpell s && s.Card == pot.Id);

        Scenario.Submit(engine, new ActivateSpell(0, pot.Id));

        Assert.Equal(7, engine.State.Player(0).Hand.Count);
        Assert.Equal(Location.Graveyard, pot.Loc);
    }

    [Fact]
    public void HandLimitForcesDiscardsInTheEndPhase()
    {
        DuelEngine engine = Scenario.Start(0, new[] { Cards.PotOfGreed });
        CardInstance pot = Scenario.InHand(engine, 0, Cards.PotOfGreed);
        Scenario.Submit(engine, new ActivateSpell(0, pot.Id));
        Scenario.Submit(engine, new Pass(0));

        Assert.Equal(Phase.End, engine.State.Phase);
        Assert.Equal(7, engine.State.Player(0).Hand.Count);
        Assert.All(engine.LegalActions(0), a => Assert.IsType<Discard>(a));
        Assert.Equal("discard down to 6 cards before ending the turn", engine.Validate(new Pass(0)));

        Scenario.Submit(engine, new Discard(0, engine.State.Player(0).Hand[0].Id));

        Assert.Equal(2, engine.State.TurnNumber);
        Assert.Equal(6, engine.State.Player(0).Hand.Count);
        Assert.Contains(engine.Events, e => e is CardDiscarded { Player: 0 });
    }

    [Fact]
    public void DrawingFromAnEmptyDeckLosesTheDuel()
    {
        var six = new Deck(Enumerable.Repeat(Cards.Filler, 6).ToList());
        var options = new DuelOptions { Seed = 1, FirstPlayer = 0, Shuffle = false, MinDeckSize = 6 };
        DuelEngine engine = DuelEngine.Start(six, Scenario.DeckWithTop(), options);

        Scenario.Submit(engine, new Pass(0));
        Scenario.Submit(engine, new Pass(1));

        Assert.True(engine.State.IsOver);
        Assert.Equal(DuelOutcome.DeckOut, engine.State.Outcome);
        Assert.Equal(1, engine.State.Winner);
        Assert.Contains(engine.Events, e => e is DuelEnded { Winner: 1, Outcome: DuelOutcome.DeckOut });
        Assert.Empty(engine.LegalActions(0));
    }

    [Fact]
    public void SurrenderEndsTheDuel()
    {
        DuelEngine engine = Scenario.Start(0);

        Scenario.Submit(engine, new Surrender(0));

        Assert.Equal(DuelOutcome.Surrender, engine.State.Outcome);
        Assert.Equal(1, engine.State.Winner);
        Assert.Equal("the duel is over", engine.Validate(new Pass(0)));
    }

    [Fact]
    public void CloneIsIndependent()
    {
        DuelEngine engine = Scenario.Start(0, new[] { Cards.GeminiElf });
        CardInstance elf = Scenario.InHand(engine, 0, Cards.GeminiElf);

        DuelEngine clone = engine.Clone();
        Scenario.Submit(clone, new NormalSummon(0, elf.Id));

        Assert.Equal(Location.Hand, elf.Loc);
        Assert.Equal(0, engine.State.Player(0).MonsterCount);
        Assert.Equal(1, clone.State.Player(0).MonsterCount);
        Assert.DoesNotContain(clone.Events, e => e is DuelStarted);
        Assert.DoesNotContain(engine.Events, e => e is MonsterSummoned);
    }
}
