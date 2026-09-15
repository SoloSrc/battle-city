using System;
using System.IO;
using System.Linq;
using BattleCity.Duel.Core.Ai;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Effects.Cards;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;
using Xunit;

namespace BattleCity.Duel.Core.Tests;

/// <summary>Issue #58: the four tier 4 cards (systems.md §5.6) and the whole pool accepted by the engine.</summary>
public class TierFourTests
{
    private static readonly Func<DuelOptions, DuelOptions> _noHandLimit = o => o with { HandLimit = 20 };

    private static CardDefinition Real(string id) => Cards.Real(id);

    [Fact]
    public void SpiritReaperSurvivesBattleDiesWhenTargetedAndDiscardsOnADirectHit()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("book_of_moon") }, configure: _noHandLimit);
        CardInstance reaper = Scenario.Place(engine, 0, Real("spirit_reaper"), Position.FaceUpAttack);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);
        Assert.False(reaper.Has(Restriction.CanAttackDirectly));

        Scenario.EnterBattle(engine);
        Scenario.Submit(engine, new Pass(0)); // the Book of Moon in hand keeps player 0 from being passed for
        Assert.Equal("direct attacks are only possible when the opponent controls no monsters", engine.Validate(new DeclareAttack(0, reaper.Id, null)));
        Scenario.Attack(engine, reaper, weakling);
        Scenario.PassUntil(engine, s => s.Window == Window.Open);
        Assert.Equal(Location.MonsterZone, reaper.Loc);
        Assert.Equal(8000 - 1200, engine.State.Player(0).LifePoints);

        engine.Destroy(weakling, DestroyReason.Effect);
        Scenario.PassUntil(engine, s => s.TurnNumber == 4 && s.Phase == Phase.Main1);
        int handBefore = engine.State.Player(1).Hand.Count;
        Scenario.EnterBattle(engine);
        Scenario.Submit(engine, new Pass(0));
        Scenario.Attack(engine, reaper, null);
        Scenario.PassUntil(engine, s => s.Window == Window.Open);
        Assert.Equal(8000 - 300, engine.State.Player(1).LifePoints);
        Assert.Equal(handBefore - 1, engine.State.Player(1).Hand.Count);

        Scenario.PassUntil(engine, s => s.Phase == Phase.Main2);
        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Real("book_of_moon")).Id));
        Scenario.Answer(engine, reaper.Id);
        Assert.Equal(Location.Graveyard, reaper.Loc);
    }

    [Fact]
    public void BlackLusterSoldierIsChaosSummonedAndBanishesOrAttacksTwice()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("black_luster_soldier_envoy_of_the_beginning") }, configure: _noHandLimit);
        CardInstance soldier = Scenario.InHand(engine, 0, Real("black_luster_soldier_envoy_of_the_beginning"));
        CardInstance light = Scenario.InGraveyard(engine, 0, Real("mystical_elf"));
        CardInstance dark = Scenario.InGraveyard(engine, 0, Real("sangan"));
        CardInstance hidden = Scenario.Place(engine, 1, Cards.Wall, Position.FaceDownDefense);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);
        CardInstance filler = Scenario.Place(engine, 1, Cards.Filler, Position.FaceUpAttack);

        Assert.Equal("Black Luster Soldier - Envoy of the Beginning cannot be Normal Summoned", engine.Validate(new NormalSummon(0, soldier.Id, new[] { hidden.Id, weakling.Id })));
        Scenario.Submit(engine, new ActivateEffect(0, soldier.Id, 0));
        Scenario.Answer(engine, light.Id);
        Scenario.Answer(engine, dark.Id);
        Assert.Equal(Location.MonsterZone, soldier.Loc);
        Assert.Equal(3000, soldier.Atk);
        Scenario.PassUntil(engine, s => s.Window == Window.Open);

        // Banish a face-down monster; the Soldier sits out the Battle Phase.
        Scenario.Submit(engine, new ActivateEffect(0, soldier.Id, 1));
        Scenario.Answer(engine, hidden.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(Location.Banished, hidden.Loc);
        Assert.True(soldier.Has(Restriction.CannotAttack));

        // Next turn: destroy a monster by battle, then attack once more; the banish is spent for the turn.
        Scenario.PassUntil(engine, s => s.TurnNumber == 4 && s.Phase == Phase.Main1);
        Assert.False(soldier.Has(Restriction.CannotAttack));
        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, soldier, weakling);
        Assert.Equal(Location.Graveyard, weakling.Loc);
        PendingChoice ask = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.OptionalTrigger, ask.Kind);
        Scenario.Answer(engine, soldier.Id);
        Scenario.ResolveChain(engine);
        Scenario.PassUntil(engine, s => s.Window == Window.Open);
        Assert.False(soldier.AttackedThisTurn);
        Scenario.Attack(engine, soldier, filler);
        Assert.Equal(Location.Graveyard, filler.Loc);
        Assert.Null(engine.State.PendingChoice);
        Assert.Equal("Black Luster Soldier - Envoy of the Beginning already attacked this turn", engine.Validate(new DeclareAttack(0, soldier.Id, null)));
        Scenario.PassUntil(engine, s => s.Phase == Phase.Main2);
        Scenario.Place(engine, 1, Cards.Wall, Position.FaceUpDefense);
        Assert.Equal("Black Luster Soldier - Envoy of the Beginning cannot be activated now", engine.Validate(new ActivateEffect(0, soldier.Id, 1)));
    }

    [Fact]
    public void ThousandEyesRestrictLocksTheFieldAndAbsorbsAMonsterThatDiesInItsPlace()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(configure: _noHandLimit);
        CardInstance restrict = Scenario.Place(engine, 0, Real("thousand_eyes_restrict"), Position.FaceUpAttack);
        CardInstance squire = Scenario.Place(engine, 0, Cards.Squire, Position.FaceUpAttack);
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        Assert.All(new[] { squire, elf }, m => Assert.True(m.Has(Restriction.CannotAttack) && m.Has(Restriction.CannotChangePosition)));
        Assert.False(restrict.Has(Restriction.CannotAttack));
        Assert.Equal("Squire cannot change its battle position", engine.Validate(new ChangePosition(0, squire.Id)));

        Scenario.Submit(engine, new ActivateEffect(0, restrict.Id, 1));
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.SpellTrapZone, elf.Loc);
        Assert.Equal(0, elf.Controller);
        Assert.Equal(restrict.Id, elf.EquippedTo);
        Assert.Contains(elf, engine.State.Player(0).SpellTraps);
        Assert.Equal(0, engine.State.Player(1).MonsterCount);
        Assert.Equal((1900, 900), (restrict.Atk, restrict.DefValue));
        Assert.Contains(engine.Events, e => e is MonsterAbsorbed a && a.Card == elf.Id && a.Target == restrict.Id);
        Assert.Equal("Thousand-Eyes Restrict's effect was already used this turn", engine.Validate(new ActivateEffect(0, restrict.Id, 1)));

        // Battle destruction is replaced by the absorbed monster.
        engine.Destroy(restrict, DestroyReason.Battle);
        Assert.Equal(Location.MonsterZone, restrict.Loc);
        Assert.Equal(Location.Graveyard, elf.Loc);
        Assert.Equal((0, 0), (restrict.Atk, restrict.DefValue));

        // Leaving takes the absorbed monster along.
        Scenario.PassUntil(engine, s => s.TurnNumber == 4 && s.Phase == Phase.Main1);
        Scenario.Submit(engine, new ActivateEffect(0, restrict.Id, 1));
        Scenario.Answer(engine, squire.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(Location.SpellTrapZone, squire.Loc);
        engine.Destroy(restrict, DestroyReason.Effect);
        Assert.Equal(Location.Graveyard, restrict.Loc);
        Assert.Equal(Location.Graveyard, squire.Loc);
    }

    [Fact]
    public void AnAbsorbedMonsterContributesNothing()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance restrict = Scenario.Place(engine, 0, Real("thousand_eyes_restrict"), Position.FaceUpAttack);
        CardInstance jinzo = Scenario.Place(engine, 1, Real("jinzo"), Position.FaceUpAttack);
        Assert.True(engine.State.Player(0).Has(PlayerRestriction.TrapsNegated));

        Scenario.Submit(engine, new ActivateEffect(0, restrict.Id, 1));
        Scenario.Answer(engine, jinzo.Id);
        Scenario.ResolveChain(engine);

        Assert.True(DuelEngine.IsAbsorbed(jinzo));
        Assert.False(engine.State.Player(0).Has(PlayerRestriction.TrapsNegated));
        Assert.Equal(2400, restrict.Atk);
    }

    [Fact]
    public void CyberJarWipesTheFieldAndDealsFiveToEachPlayer()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(configure: _noHandLimit);
        CardInstance jar = Scenario.Place(engine, 0, Real("cyber_jar"), Position.FaceDownDefense);
        CardInstance mine = Scenario.Place(engine, 0, Cards.Titan, Position.FaceUpAttack);
        CardInstance theirs = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        PlayerState p0 = engine.State.Player(0);
        PlayerState p1 = engine.State.Player(1);
        var top0 = new[] { Cards.GeminiElf, Cards.SummonedSkull, Cards.PotOfGreed, Cards.Titan, Cards.Weakling }.Select(d => new CardInstance(Guid.NewGuid(), d, 0)).ToList();
        foreach (CardInstance card in Enumerable.Reverse(top0))
        {
            p0.Deck.Add(card);
        }

        int hand0 = p0.Hand.Count;
        int hand1 = p1.Hand.Count;
        int deck1 = p1.Deck.Count;

        Scenario.Submit(engine, new FlipSummon(0, jar.Id));
        Scenario.ResolveChain(engine);

        Assert.All(new[] { jar, mine, theirs }, c => Assert.Equal(Location.Graveyard, c.Loc));
        Assert.Equal(new[] { top0[0], top0[4] }, p0.Monsters);
        Assert.All(p0.Monsters, m => Assert.Equal(Position.FaceUpAttack, m.Pos));
        Assert.Equal(hand0 + 3, p0.Hand.Count);
        Assert.All(new[] { top0[1], top0[2], top0[3] }, c => Assert.Equal(Location.Hand, c.Loc));
        Assert.Equal(deck1 - CyberJarEffect.RevealCount, p1.Deck.Count);
        Assert.Equal(CyberJarEffect.RevealCount, p1.MonsterCount + p1.Hand.Count - hand1);
        Assert.Equal(2, engine.Events.Count(e => e is CardsRevealed));
    }

    // ----- Acceptance: the whole pool -----

    [Theory]
    [InlineData("beatdown")]
    [InlineData("goat_control")]
    [InlineData("rookie_beatdown")]
    [InlineData("starter")]
    [InlineData("warrior_toolbox")]
    public void EveryDeckIsAcceptedByTheEngine(string id)
    {
        Deck deck = TierThreeTests.RealDeck(id);

        DuelEngine engine = DuelEngine.Start(deck, deck, new DuelOptions { Seed = 1 });

        Assert.Equal(Scenario.DeckSize, engine.State.Player(0).AllCards.Count(c => c.Def.Kind != CardKind.Fusion));
    }

    [Fact]
    public void TheDeckFilesUseNoFiller()
    {
        foreach (string path in Directory.EnumerateFiles(Cards.DecksDirectory, "*.json"))
        {
            Deck deck = TierThreeTests.RealDeck(Path.GetFileNameWithoutExtension(path));
            Assert.Equal(Scenario.DeckSize, deck.Main.Count);
        }
    }

    [Theory]
    [InlineData("goat_control", "goat_control", 1UL)]
    [InlineData("goat_control", "goat_control", 2UL)]
    [InlineData("goat_control", "goat_control", 3UL)]
    [InlineData("goat_control", "beatdown", 4UL)]
    [InlineData("warrior_toolbox", "goat_control", 5UL)]
    public void HeuristicAgentsPlayGoatControlToAWinner(string deck0, string deck1, ulong seed)
    {
        DuelEngine engine = DuelEngine.Start(TierThreeTests.RealDeck(deck0), TierThreeTests.RealDeck(deck1), new DuelOptions { Seed = seed });

        int steps = DuelRunner.Play(engine, new HeuristicAgent(AiProfile.ArcadeOwner, seed), new HeuristicAgent(AiProfile.Mara, seed + 100));

        Assert.True(engine.State.IsOver, $"duel still running after {steps} commands");
        Assert.NotNull(engine.State.Winner);
        Assert.InRange(engine.State.TurnNumber, 3, 120);
    }
}
