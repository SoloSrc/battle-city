using System;
using System.Linq;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;
using Xunit;

namespace BattleCity.Duel.Core.Tests;

/// <summary>Issue #55: the modifier registry, equips, control changes, banishing, tokens, counters, once-per-turn effects, life point costs, random discards, Special Summons, Spirits and position locks, one mechanism per test.</summary>
public class MechanicsTests
{
    private static readonly Func<DuelOptions, DuelOptions> _noHandLimit = o => o with { HandLimit = 20 };

    [Fact]
    public void ContinuousModifiersFollowTheirSource()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance knight = Scenario.Place(engine, 0, Cards.Knight, Position.FaceUpAttack);
        CardInstance squire = Scenario.Place(engine, 0, Cards.Squire, Position.FaceUpAttack);
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);

        Assert.Equal(1400 + TestEffects.WarriorBoost.Amount, squire.Atk);
        Assert.Equal(1200 + TestEffects.WarriorBoost.Amount, knight.Atk);
        Assert.Equal(1900, elf.Atk);
        Assert.True(knight.Has(Restriction.CannotBeAttacked));
        Assert.Empty(engine.State.Modifiers);

        // The boost is recomputed from the field: it goes with its source and comes back with a new one.
        engine.Destroy(knight, DestroyReason.Effect);
        Assert.Equal(1400, squire.Atk);
        Assert.False(knight.Has(Restriction.CannotBeAttacked));

        // A face-down source contributes nothing; a lone source can be attacked.
        engine.Destroy(squire, DestroyReason.Effect);
        engine.Destroy(elf, DestroyReason.Effect);
        CardInstance second = Scenario.Place(engine, 0, Cards.Knight, Position.FaceDownDefense);
        Assert.Equal(1200, second.Atk);
        second.Pos = Position.FaceUpAttack;
        engine.Refresh();
        Assert.Equal(1200 + TestEffects.WarriorBoost.Amount, second.Atk);
        Assert.False(second.Has(Restriction.CannotBeAttacked));
    }

    [Fact]
    public void TimedModifiersExpireAtTheEndPhase()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Boost });
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance boost = Scenario.InHand(engine, 0, Cards.Boost);

        Scenario.Submit(engine, new ActivateSpell(0, boost.Id));
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);
        Modifier modifier = Assert.Single(engine.State.Modifiers);
        Assert.Equal(elf.Id, modifier.Card);
        Assert.Equal(engine.State.TurnNumber, modifier.ExpiresAfterTurn);
        Assert.Equal(1900 + TestEffects.BoostSpell.Amount, elf.Atk);

        Scenario.Submit(engine, new Pass(0)); // Main Phase 1 → End Phase
        Assert.Empty(engine.State.Modifiers);
        Assert.Equal(1900, elf.Atk);
        Assert.Contains(engine.Events, e => e is ModifierRemoved r && r.Modifier == modifier);
    }

    [Fact]
    public void ModifiersOnACardGoWhenItLeavesTheField()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        engine.AddModifier(Modifier.OnCard(ModifierKind.Atk, elf.Id, elf.Id, 500));
        Assert.Equal(2400, elf.Atk);

        engine.ReturnToHand(elf);
        Assert.Empty(engine.State.Modifiers);
        Assert.Equal(1900, elf.Atk);
        Assert.Equal(0, elf.AtkBonus);

        // A modifier for a card that is not on the field is dropped on arrival.
        engine.AddModifier(Modifier.OnCard(ModifierKind.Atk, elf.Id, elf.Id, 500));
        Assert.Empty(engine.State.Modifiers);
    }

    [Fact]
    public void EquipsAttachAndAreDestroyedWhenTheMonsterLeaves()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Axe });
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance axe = Scenario.InHand(engine, 0, Cards.Axe);

        Scenario.Submit(engine, new ActivateSpell(0, axe.Id));
        Assert.Equal(ChoiceKind.Target, engine.State.PendingChoice!.Kind);
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.SpellTrapZone, axe.Loc);
        Assert.Equal(elf.Id, axe.EquippedTo);
        Assert.Equal(1900 + TestEffects.EquipBoost.Amount, elf.Atk);
        Assert.Contains(engine.Events, e => e is CardEquipped c && c.Equip == axe.Id && c.Target == elf.Id);

        engine.Destroy(elf, DestroyReason.Effect);
        Assert.Equal(Location.Graveyard, axe.Loc);
        Assert.Null(axe.EquippedTo);
        Assert.Contains(engine.Events, e => e is SpellTrapDestroyed d && d.Card == axe.Id);
    }

    [Fact]
    public void AMonsterOutlivesItsEquip()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Axe });
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance axe = Scenario.InHand(engine, 0, Cards.Axe);
        Scenario.Submit(engine, new ActivateSpell(0, axe.Id));
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);

        engine.Destroy(axe, DestroyReason.Effect);
        Assert.Equal(Location.MonsterZone, elf.Loc);
        Assert.Equal(1900, elf.Atk);
    }

    [Fact]
    public void AnEquipWhoseTargetWasFlippedFaceDownGoesToTheGraveyard()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Axe });
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance book = Scenario.Set(engine, 1, Cards.BookOfMoon);
        CardInstance axe = Scenario.InHand(engine, 0, Cards.Axe);

        Scenario.Submit(engine, new ActivateSpell(0, axe.Id));
        Scenario.Answer(engine, elf.Id);
        Assert.Equal(1, engine.State.Priority);
        Scenario.Submit(engine, new ActivateSpell(1, book.Id));
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(Position.FaceDownDefense, elf.Pos);
        Assert.Equal(Location.Graveyard, axe.Loc);
        Assert.Equal(Location.Graveyard, book.Loc);
    }

    [Fact]
    public void FlippingAMonsterFaceDownDestroysItsEquips()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance axe = Scenario.Set(engine, 0, Cards.Axe);
        axe.Pos = Position.FaceUp;
        Assert.True(engine.Equip(axe, elf));
        Assert.Equal(2900, elf.Atk);

        engine.FlipFaceDown(elf);
        Assert.Equal(Position.FaceDownDefense, elf.Pos);
        Assert.Equal(Location.Graveyard, axe.Loc);
        Assert.Equal(1900, elf.Atk);
        Assert.Contains(engine.Events, e => e is MonsterFlippedFaceDown f && f.Card == elf.Id);
    }

    [Fact]
    public void TemporaryControlReturnsAtTheEndPhase()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Controller });
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance controller = Scenario.InHand(engine, 0, Cards.Controller);

        Scenario.Submit(engine, new ActivateSpell(0, controller.Id));
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(0, elf.Controller);
        Assert.Equal(1, elf.Owner);
        Assert.Same(elf, engine.State.Player(0).MonsterZones[elf.ZoneIndex]);
        Assert.Equal(0, engine.State.Player(1).MonsterCount);
        Assert.Contains(engine.Events, e => e is ControlChanged c && c.Card == elf.Id && c.From == 1 && c.To == 0 && c.ReturnsAfterTurn == 2);

        Scenario.Submit(engine, new Pass(0)); // End Phase
        Assert.Equal(1, elf.Controller);
        Assert.Same(elf, engine.State.Player(1).MonsterZones[elf.ZoneIndex]);
        Assert.Null(elf.ControlReturnsAfterTurn);
    }

    [Fact]
    public void StolenMonsterReturnsWhenTheEquipLeaves()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Snatch });
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance snatch = Scenario.InHand(engine, 0, Cards.Snatch);

        Scenario.Submit(engine, new ActivateSpell(0, snatch.Id));
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(0, elf.Controller);
        Assert.Equal(elf.Id, snatch.EquippedTo);
        Assert.Equal(Location.SpellTrapZone, snatch.Loc);

        engine.Destroy(snatch, DestroyReason.Effect);
        Assert.Equal(1, elf.Controller);
        Assert.Same(elf, engine.State.Player(1).MonsterZones[elf.ZoneIndex]);
        Assert.Equal(0, engine.State.Player(0).MonsterCount);
    }

    [Fact]
    public void ControlAlwaysReturnsWhenTheMonsterLeavesAndNeedsAFreeZone()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        Assert.True(engine.ChangeControl(elf, 0, engine.State.TurnNumber));
        engine.Destroy(elf, DestroyReason.Effect);
        Assert.Equal(1, elf.Controller);
        Assert.Contains(elf, engine.State.Player(1).Graveyard);

        for (int i = 0; i < PlayerState.ZoneCount; i++)
        {
            Scenario.Place(engine, 0, Cards.Filler, Position.FaceUpAttack);
        }

        CardInstance second = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        Assert.False(engine.ChangeControl(second, 0));
        Assert.Equal(1, second.Controller);
    }

    [Fact]
    public void BanishedCardsGoToTheBanishedPile()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Banisher });
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Cards.Banisher).Id));
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Banished, elf.Loc);
        Assert.Contains(elf, engine.State.Player(1).Banished);
        Assert.Equal(0, engine.State.Player(1).MonsterCount);
        Assert.Contains(engine.Events, e => e is CardBanished b && b.Card == elf.Id && b.From == Location.MonsterZone);
    }

    [Fact]
    public void TokensExistOnlyOnTheField()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.SummonedSkull, Cards.Goats });
        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Cards.Goats).Id));
        Scenario.ResolveChain(engine);

        var tokens = engine.State.Player(0).Monsters.ToList();
        Assert.Equal(4, tokens.Count);
        Assert.All(tokens, t => Assert.True(t.IsToken && t.Pos == Position.FaceUpDefense && t.Has(Restriction.CannotBeTributed)));
        Assert.Equal(4, engine.Events.Count(e => e is TokenCreated));

        CardInstance skull = Scenario.InHand(engine, 0, Cards.SummonedSkull);
        Assert.Equal("Sheep Token cannot be tributed", engine.Validate(new NormalSummon(0, skull.Id, new[] { tokens[0].Id })));

        engine.Destroy(tokens[0], DestroyReason.Effect);
        engine.ReturnToHand(tokens[1]);
        engine.Banish(tokens[2]);
        Assert.Equal(1, engine.State.Player(0).MonsterCount);
        PlayerState p = engine.State.Player(0);
        Assert.DoesNotContain(p.Graveyard.Concat(p.Hand).Concat(p.Banished), c => c.IsToken);
        Assert.Null(engine.State.Find(tokens[0].Id));
        Assert.Equal(3, engine.Events.Count(e => e is TokenRemoved));
        Assert.DoesNotContain(engine.State.Modifiers, m => m.Card == tokens[0].Id);
    }

    [Fact]
    public void CountersChangeAndClearWhenTheCardLeaves()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance breaker = Scenario.Place(engine, 0, Cards.Breaker, Position.FaceUpAttack);
        CardInstance trap = Scenario.Set(engine, 1, Cards.AttackTrap);

        Scenario.Submit(engine, new ActivateEffect(0, breaker.Id, 0));
        Scenario.ResolveChain(engine);
        Assert.Equal(1, breaker.Counter(TestEffects.CounterCharge.Counter));
        Assert.Equal(1600 + TestEffects.CounterCharge.AtkPerCounter, breaker.Atk);
        Assert.Contains(engine.Events, e => e is CounterChanged c && c.Card == breaker.Id && c.Count == 1);

        // The second effect pays the counter as its cost before the target is asked.
        Scenario.Submit(engine, new ActivateEffect(0, breaker.Id, 1));
        Assert.Equal(0, breaker.Counter(TestEffects.CounterCharge.Counter));
        Assert.Equal(ChoiceKind.Target, engine.State.PendingChoice!.Kind);
        Scenario.Answer(engine, trap.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(Location.Graveyard, trap.Loc);
        Assert.Equal(1600, breaker.Atk);
        Assert.Equal("Breaker cannot be activated now", engine.Validate(new ActivateEffect(0, breaker.Id, 1)));

        engine.AddCounter(breaker, TestEffects.CounterCharge.Counter, 2);
        engine.Destroy(breaker, DestroyReason.Effect);
        Assert.Empty(breaker.Counters);
    }

    [Fact]
    public void OncePerTurnEffectsResetWithTheTurn()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(configure: _noHandLimit);
        CardInstance breaker = Scenario.Place(engine, 0, Cards.Breaker, Position.FaceUpAttack);

        Scenario.Submit(engine, new ActivateEffect(0, breaker.Id, 0));
        Scenario.ResolveChain(engine);
        Assert.Equal("Breaker's effect was already used this turn", engine.Validate(new ActivateEffect(0, breaker.Id, 0)));
        Assert.DoesNotContain(engine.LegalActions(0), a => a is ActivateEffect);

        Scenario.PassUntil(engine, s => s.TurnNumber == 4 && s.Phase == Phase.Main1);
        Assert.Null(engine.Validate(new ActivateEffect(0, breaker.Id, 0)));
        Scenario.Submit(engine, new ActivateEffect(0, breaker.Id, 0));
        Scenario.ResolveChain(engine);
        Assert.Equal(2, breaker.Counter(TestEffects.CounterCharge.Counter));
    }

    [Fact]
    public void LifePointsArePaidAsACost()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.LifeDraw, Cards.LifeDraw });
        CardInstance first = engine.State.Player(0).Hand.First(c => c.Def == Cards.LifeDraw);
        int handBefore = engine.State.Player(0).Hand.Count;

        Scenario.Submit(engine, new ActivateSpell(0, first.Id));
        Assert.Equal(8000 - TestEffects.LifeCostDraw.Cost, engine.State.Player(0).LifePoints);
        Assert.Contains(engine.Events, e => e is LifePointsPaid p && p.Player == 0 && p.Amount == TestEffects.LifeCostDraw.Cost);
        Scenario.ResolveChain(engine);
        Assert.Equal(handBefore, engine.State.Player(0).Hand.Count); // −1 activated, +1 drawn

        CardInstance second = Scenario.InHand(engine, 0, Cards.LifeDraw);
        engine.State.Player(0).LifePoints = TestEffects.LifeCostDraw.Cost - 1;
        Assert.Equal("Life Draw cannot be activated now", engine.Validate(new ActivateSpell(0, second.Id)));

        // Paying the last life point is allowed and loses the duel.
        engine.State.Player(0).LifePoints = TestEffects.LifeCostDraw.Cost;
        Scenario.Submit(engine, new ActivateSpell(0, second.Id));
        Assert.True(engine.State.IsOver);
        Assert.Equal(1, engine.State.Winner);
        Assert.Equal(DuelOutcome.LifePoints, engine.State.Outcome);
    }

    [Fact]
    public void RandomDiscardsComeFromTheSeed()
    {
        string Run()
        {
            DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Duo }, new[] { Cards.GeminiElf, Cards.ArchfiendSoldier, Cards.SummonedSkull, Cards.Weakling, Cards.Wall, Cards.Titan });
            int before = engine.State.Player(1).Hand.Count;
            Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Cards.Duo).Id));
            Scenario.ResolveChain(engine);
            Assert.Equal(before - 1, engine.State.Player(1).Hand.Count);
            CardDiscarded discarded = engine.Events.OfType<CardDiscarded>().Single();
            Assert.Equal(1, discarded.Player);
            Assert.Equal(Location.Graveyard, engine.State.Find(discarded.Card)!.Loc);
            return discarded.CardId;
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void SpecialSummonsOpenTheSummonWindowAndFireSummonTriggers()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Reborn });
        CardInstance drawer = Scenario.InGraveyard(engine, 0, Cards.Drawer);

        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Cards.Reborn).Id));
        Scenario.Answer(engine, drawer.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.MonsterZone, drawer.Loc);
        Assert.Equal(Position.FaceUpAttack, drawer.Pos);
        Assert.True(drawer.ArrivedThisTurn);
        Assert.Contains(engine.Events, e => e is MonsterSpecialSummoned s && s.Card == drawer.Id && s.From == Location.Graveyard);
        Assert.Contains(engine.Events, e => e is WindowChanged { Window: Window.Summon } w && w.Card == drawer.Id);
        Assert.Equal(ChoiceKind.OptionalTrigger, engine.State.PendingChoice!.Kind);
        Scenario.Answer(engine); // decline the draw; nobody responds to the summon and the window closes
        Assert.Equal(Window.Open, engine.State.Window);
        Assert.Equal("a monster cannot change position on the turn it was Summoned or Set", engine.Validate(new ChangePosition(0, drawer.Id)));
    }

    [Fact]
    public void SpecialSummonsRefuseSpiritsAndFullFields()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance spirit = Scenario.InGraveyard(engine, 0, Cards.Spirit);
        Assert.False(engine.SpecialSummon(spirit, 0, Position.FaceUpAttack));
        Assert.Equal(Location.Graveyard, spirit.Loc);

        CardInstance elf = Scenario.InGraveyard(engine, 0, Cards.GeminiElf);
        for (int i = 0; i < PlayerState.ZoneCount; i++)
        {
            Scenario.Place(engine, 0, Cards.Filler, Position.FaceUpAttack);
        }

        Assert.False(engine.SpecialSummon(elf, 0, Position.FaceUpAttack));
        Assert.Equal(Location.Graveyard, elf.Loc);
    }

    [Fact]
    public void FusionMonstersComeFromTheFusionDeckAndGoToTheGraveyard()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.FusionCall }, fusion0: new[] { Cards.FusionBeast });
        CardInstance beast = Assert.Single(engine.State.Player(0).FusionDeck);
        Assert.Equal(Location.FusionDeck, beast.Loc);

        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Cards.FusionCall).Id));
        Scenario.ResolveChain(engine);
        Assert.Equal(Location.MonsterZone, beast.Loc);
        Assert.Empty(engine.State.Player(0).FusionDeck);
        Assert.Contains(engine.Events, e => e is MonsterSpecialSummoned s && s.Card == beast.Id && s.From == Location.FusionDeck);

        engine.Destroy(beast, DestroyReason.Effect);
        Assert.Contains(beast, engine.State.Player(0).Graveyard);
    }

    [Fact]
    public void SpiritsReturnToTheHandAtTheEndPhaseOfTheTurnTheyWereSummoned()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Spirit });
        CardInstance spirit = Scenario.InHand(engine, 0, Cards.Spirit);
        Scenario.Submit(engine, new NormalSummon(0, spirit.Id));
        Assert.Equal(Location.MonsterZone, spirit.Loc);

        Scenario.Submit(engine, new Pass(0)); // End Phase
        Assert.Equal(Location.Hand, spirit.Loc);
        Assert.Contains(engine.Events, e => e is SpiritReturned r && r.Card == spirit.Id);
    }

    [Fact]
    public void ASetSpiritStaysUntilItIsFlipped()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Spirit }, configure: _noHandLimit);
        CardInstance spirit = Scenario.InHand(engine, 0, Cards.Spirit);
        Scenario.Submit(engine, new SetMonster(0, spirit.Id));
        Scenario.Submit(engine, new Pass(0)); // End Phase: face-down, it stays
        Assert.Equal(Location.MonsterZone, spirit.Loc);

        Scenario.PassUntil(engine, s => s.TurnNumber == 4 && s.Phase == Phase.Main1);
        Scenario.Submit(engine, new FlipSummon(0, spirit.Id));
        Assert.True(spirit.FlippedThisTurn);
        Scenario.Submit(engine, new Pass(0)); // End Phase: flipped this turn, it returns
        Assert.Equal(Location.Hand, spirit.Loc);
    }

    [Fact]
    public void PositionLocksLastUntilTheEndOfTheNextTurn()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Lock }, configure: _noHandLimit);
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Cards.Lock).Id));
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);

        Modifier modifier = Assert.Single(engine.State.Modifiers);
        Assert.Equal(4, modifier.ExpiresAfterTurn); // "until the end of your next turn": player 0's next turn is turn 4
        Assert.True(elf.Has(Restriction.CannotChangePosition));

        Scenario.PassUntil(engine, s => s.TurnNumber == 3 && s.Phase == Phase.Main1);
        Assert.Equal("Gemini Elf cannot change its battle position", engine.Validate(new ChangePosition(1, elf.Id)));

        Scenario.PassUntil(engine, s => s.TurnNumber == 5 && s.Phase == Phase.Main1);
        Assert.Empty(engine.State.Modifiers);
        Assert.Null(engine.Validate(new ChangePosition(1, elf.Id)));
    }

    [Fact]
    public void PiercingDamageIsTheDifferenceOverDefense()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance piercer = Scenario.Place(engine, 0, Cards.Piercer, Position.FaceUpAttack);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpDefense);
        Assert.True(piercer.Has(Restriction.Piercing));

        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, piercer, weakling);
        Assert.Equal(Location.Graveyard, weakling.Loc);
        Assert.Equal(8000 - (1900 - 1000), engine.State.Player(1).LifePoints);
    }

    [Fact]
    public void AShieldStopsBattleDamageAndBattleDestructionForTheTurn()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);
        CardInstance shield = Scenario.Set(engine, 1, Cards.Shield);

        Scenario.EnterBattle(engine);
        Scenario.Submit(engine, new Pass(1)); // the live Shield keeps player 1 from being passed for in the Start Step
        Scenario.Attack(engine, elf, weakling);
        Assert.Equal(Window.AttackDeclared, engine.State.Window);
        Scenario.Submit(engine, new ActivateTrap(1, shield.Id));
        Scenario.ResolveChain(engine);
        Assert.True(engine.State.Player(1).Has(PlayerRestriction.NoBattleDamage));
        Assert.True(weakling.Has(Restriction.CannotBeDestroyedByBattle));

        Scenario.PassUntil(engine, s => s.Window == Window.Open);
        Assert.Equal(Location.MonsterZone, weakling.Loc);
        Assert.Equal(8000, engine.State.Player(1).LifePoints);
        Assert.Equal(Location.Graveyard, shield.Loc);

        Scenario.PassUntil(engine, s => s.TurnNumber == 3);
        Assert.False(engine.State.Player(1).Has(PlayerRestriction.NoBattleDamage));
    }

    [Fact]
    public void ATrapLockBlocksActivationsAndSilencesTrapLinks()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Reborn });
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance jinzo = Scenario.InGraveyard(engine, 0, Cards.Jinzo);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);
        CardInstance shield = Scenario.Set(engine, 1, Cards.Shield);

        Scenario.EnterBattle(engine);
        Scenario.Submit(engine, new Pass(0)); // the live Reborn and Shield keep both players from being passed for
        Scenario.Submit(engine, new Pass(1));
        Scenario.Attack(engine, elf, weakling);
        Scenario.Submit(engine, new Pass(0));
        Scenario.Submit(engine, new ActivateTrap(1, shield.Id));

        // Jinzo is revived on top of the Shield: the Shield's link resolves to nothing.
        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Cards.Reborn).Id));
        Scenario.Answer(engine, jinzo.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(Location.MonsterZone, jinzo.Loc);
        Assert.True(engine.State.Player(1).Has(PlayerRestriction.TrapsNegated));
        Assert.Empty(engine.State.Modifiers);
        Assert.Equal(Location.Graveyard, shield.Loc);
        Scenario.PassUntil(engine, s => s.Window == Window.Open);
        Assert.Equal(Location.Graveyard, weakling.Loc);
        Assert.Equal(8000 - 400, engine.State.Player(1).LifePoints);

        CardInstance second = Scenario.Set(engine, 1, Cards.AttackTrap);
        Scenario.Attack(engine, jinzo, null);
        Assert.Equal(Window.Open, engine.State.Window); // player 1 had nothing to respond with: the attack went straight through
        Assert.Equal(Position.FaceDown, second.Pos);
        Assert.DoesNotContain(engine.Events, e => e is TrapActivated t && t.Card == second.Id);
        Assert.Equal(8000 - 400 - 2400, engine.State.Player(1).LifePoints);

        Scenario.PassUntil(engine, s => s.TurnNumber == 3 && s.Phase == Phase.Main1);
        Assert.Equal("Trap Cards cannot be activated", engine.Validate(new ActivateTrap(1, second.Id)));
    }

    [Fact]
    public void AReaperAttacksDirectlyAndSurvivesBattle()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance first = Scenario.Place(engine, 0, Cards.Reaper, Position.FaceUpAttack);
        CardInstance second = Scenario.Place(engine, 0, Cards.Reaper, Position.FaceUpAttack);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);

        Scenario.EnterBattle(engine);
        Assert.Contains(engine.LegalActions(0), a => a is DeclareAttack { Target: null } d && d.Attacker == first.Id);
        Scenario.Attack(engine, first, null);
        Assert.Equal(8000 - 300, engine.State.Player(1).LifePoints);

        Scenario.Attack(engine, second, weakling);
        Assert.Equal(Location.MonsterZone, second.Loc);
        Assert.Equal(8000 - (1500 - 300), engine.State.Player(0).LifePoints);
    }

    [Fact]
    public void MonstersThatCannotAttackOrBeAttackedAreNotOffered()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance knight = Scenario.Place(engine, 1, Cards.Knight, Position.FaceUpAttack);
        CardInstance squire = Scenario.Place(engine, 1, Cards.Squire, Position.FaceUpAttack);

        Scenario.EnterBattle(engine);
        Assert.Equal("Knight cannot be attacked", engine.Validate(new DeclareAttack(0, elf.Id, knight.Id)));
        Assert.Null(engine.Validate(new DeclareAttack(0, elf.Id, squire.Id)));

        engine.AddModifier(Modifier.OnCard(ModifierKind.CannotAttack, elf.Id, elf.Id));
        Assert.Equal("Gemini Elf cannot attack", engine.Validate(new DeclareAttack(0, elf.Id, squire.Id)));
        Assert.DoesNotContain(engine.LegalActions(0), a => a is DeclareAttack);
    }

    [Fact]
    public void ABurialDestroysItsMonsterWhenItLeaves()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Burial });
        CardInstance elf = Scenario.InGraveyard(engine, 0, Cards.GeminiElf);
        CardInstance burial = Scenario.InHand(engine, 0, Cards.Burial);

        Scenario.Submit(engine, new ActivateSpell(0, burial.Id));
        Assert.Equal(8000 - TestEffects.Burial.Cost, engine.State.Player(0).LifePoints);
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(Location.MonsterZone, elf.Loc);
        Assert.Equal(elf.Id, burial.EquippedTo);

        engine.Destroy(burial, DestroyReason.Effect);
        Assert.Equal(Location.Graveyard, elf.Loc);
        Assert.Equal(Location.Graveyard, burial.Loc);
        Assert.Contains(engine.Events, e => e is MonsterDestroyed { Reason: DestroyReason.Effect } d && d.Card == elf.Id);
    }

    [Fact]
    public void ClonesCopyTheComputedValuesAndTheRegistry()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance knight = Scenario.Place(engine, 0, Cards.Knight, Position.FaceUpAttack);
        engine.AddModifier(Modifier.OnCard(ModifierKind.Atk, knight.Id, knight.Id, 100, engine.State.TurnNumber));

        DuelEngine clone = engine.Clone();
        CardInstance copy = clone.State.Find(knight.Id)!;
        Assert.NotSame(knight, copy);
        Assert.Equal(knight.Atk, copy.Atk);
        Assert.Equal(engine.State.Modifiers, clone.State.Modifiers);

        clone.Destroy(copy, DestroyReason.Effect);
        Assert.Single(engine.State.Modifiers);
        Assert.Empty(clone.State.Modifiers);
    }
}
