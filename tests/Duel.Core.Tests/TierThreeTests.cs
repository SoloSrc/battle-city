using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BattleCity.Duel.Core.Ai;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Effects.Cards;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;
using Xunit;

namespace BattleCity.Duel.Core.Tests;

/// <summary>Issue #57: one scenario per tier 3 card (systems.md §5.6), played through the real definitions in <c>data/cards/</c>.</summary>
public class TierThreeTests
{
    private static readonly Func<DuelOptions, DuelOptions> _noHandLimit = o => o with { HandLimit = 20 };

    private static readonly CardDefinition _darkMagician = CardDefinition.Vanilla("dark_magician", "Dark Magician", "Spellcaster", MonsterAttribute.Dark, 7, 2500, 2100);

    private static CardDefinition Real(string id) => Cards.Real(id);

    // ----- Continuous monsters -----

    [Fact]
    public void EnragedBattleOxGivesBeastsPiercingDamage()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance ox = Scenario.Place(engine, 0, Real("enraged_battle_ox"), Position.FaceUpAttack);
        CardInstance weakling = Scenario.Place(engine, 0, Cards.Weakling, Position.FaceUpAttack);
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance first = Scenario.Place(engine, 1, Cards.Filler, Position.FaceUpDefense);
        CardInstance second = Scenario.Place(engine, 1, Cards.Filler, Position.FaceUpDefense);

        Assert.True(ox.Has(Restriction.Piercing));
        Assert.True(weakling.Has(Restriction.Piercing));
        Assert.False(elf.Has(Restriction.Piercing));

        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, weakling, first);
        Assert.Equal(8000 - (1500 - 800), engine.State.Player(1).LifePoints);
        Scenario.Attack(engine, elf, second);
        Assert.Equal(8000 - (1500 - 800), engine.State.Player(1).LifePoints);
    }

    [Fact]
    public void JinzoLocksTraps()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        Scenario.Place(engine, 0, Real("jinzo"), Position.FaceUpAttack);
        CardInstance trap = Scenario.Set(engine, 1, Real("sakuretsu_armor"));
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);

        Assert.True(engine.State.Player(1).Has(PlayerRestriction.TrapsNegated));
        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, elf, null);

        // Player 1 could not respond: the attack went straight through.
        Assert.Equal(8000 - 1900, engine.State.Player(1).LifePoints);
        Assert.Equal(Position.FaceDown, trap.Pos);
        Scenario.PassUntil(engine, s => s.TurnNumber == 3 && s.Phase == Phase.Main1);
        Assert.Equal("Trap Cards cannot be activated", engine.Validate(new ActivateTrap(1, trap.Id)));
    }

    [Fact]
    public void BladeKnightGainsAtkWithASmallHandAndSilencesFlipEffectsWhenAlone()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(configure: _noHandLimit);
        CardInstance knight = Scenario.Place(engine, 0, Real("blade_knight"), Position.FaceUpAttack);
        Assert.Equal(1600, knight.Atk);
        engine.State.Player(0).Hand.RemoveRange(1, engine.State.Player(0).Hand.Count - 1);
        engine.Refresh();
        Assert.Equal(1600 + BladeKnightEffect.Amount, knight.Atk);
        Assert.True(knight.Has(Restriction.NegatesFlipEffectsOfDestroyed));

        CardInstance flipper = Scenario.Place(engine, 1, Cards.Flipper, Position.FaceDownDefense);
        int handBefore = engine.State.Player(1).Hand.Count;
        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, knight, flipper);
        Assert.Equal(Location.Graveyard, flipper.Loc);
        Assert.Equal(handBefore, engine.State.Player(1).Hand.Count);
        Assert.DoesNotContain(engine.Events, e => e is EffectActivated { EffectId: TestEffects.FlipDraw.EffectId });

        // With a second monster the Flip Effect goes through.
        Scenario.Place(engine, 0, Cards.Filler, Position.FaceUpDefense);
        Assert.False(knight.Has(Restriction.NegatesFlipEffectsOfDestroyed));
        Scenario.PassUntil(engine, s => s.TurnNumber == 4 && s.Phase == Phase.Main1);
        CardInstance second = Scenario.Place(engine, 1, Cards.Flipper, Position.FaceDownDefense);
        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, knight, second);
        Assert.Equal(Location.Graveyard, second.Loc);
        Assert.Contains(engine.Events, e => e is EffectActivated { EffectId: TestEffects.FlipDraw.EffectId });
    }

    [Fact]
    public void CommandKnightBoostsWarriorsAndHidesBehindThem()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance knight = Scenario.Place(engine, 0, Real("command_knight"), Position.FaceUpAttack);
        Assert.Equal(1200 + CommandKnightEffect.Amount, knight.Atk);
        Assert.False(knight.Has(Restriction.CannotBeAttacked));

        CardInstance squire = Scenario.Place(engine, 0, Cards.Squire, Position.FaceUpAttack);
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        Assert.Equal(1400 + CommandKnightEffect.Amount, squire.Atk);
        Assert.Equal(1900, elf.Atk);
        Assert.True(knight.Has(Restriction.CannotBeAttacked));

        engine.Destroy(squire, DestroyReason.Effect);
        engine.Destroy(elf, DestroyReason.Effect);
        Assert.False(knight.Has(Restriction.CannotBeAttacked));
    }

    [Fact]
    public void MaraudingCaptainSummonsFromTheHandAndProtectsOtherWarriors()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("marauding_captain"), Cards.Squire, Cards.SummonedSkull });
        CardInstance captain = Scenario.InHand(engine, 0, Real("marauding_captain"));
        CardInstance squire = Scenario.InHand(engine, 0, Cards.Squire);
        CardInstance skull = Scenario.InHand(engine, 0, Cards.SummonedSkull);

        Scenario.Submit(engine, new NormalSummon(0, captain.Id));
        PendingChoice ask = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.OptionalTrigger, ask.Kind);
        Scenario.Answer(engine, captain.Id);
        Scenario.ResolveChain(engine);
        PendingChoice pick = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Resolution, pick.Kind);
        Assert.Contains(squire.Id, pick.Choice.Options);
        Assert.DoesNotContain(skull.Id, pick.Choice.Options);
        Scenario.Answer(engine, squire.Id);

        Assert.Equal(Location.MonsterZone, squire.Loc);
        Assert.Equal(Position.FaceUpAttack, squire.Pos);
        Assert.True(squire.Has(Restriction.CannotBeAttacked));
        Assert.False(captain.Has(Restriction.CannotBeAttacked));
    }

    [Fact]
    public void AsuraPriestAttacksEveryMonsterOnce()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance asura = Scenario.Place(engine, 0, Real("asura_priest"), Position.FaceUpAttack);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);
        CardInstance filler = Scenario.Place(engine, 1, Cards.Filler, Position.FaceUpAttack);

        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, asura, weakling);
        Assert.Equal(Location.Graveyard, weakling.Loc);
        Assert.Contains(engine.LegalActions(0), a => a is DeclareAttack d && d.Target == filler.Id);
        Assert.Equal("Asura Priest already attacked this turn", engine.Validate(new DeclareAttack(0, asura.Id, null)));
        Scenario.Attack(engine, asura, filler);
        Assert.Equal(Location.Graveyard, filler.Loc);
        Assert.Equal("Asura Priest already attacked this turn", engine.Validate(new DeclareAttack(0, asura.Id, null)));
        Assert.Equal(8000 - 200 - 500, engine.State.Player(1).LifePoints);
    }

    [Fact]
    public void MysticSwordsmanCannotBeSetAndDestroysFaceDownMonstersUnflipped()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("mystic_swordsman_lv2") });
        CardInstance swordsman = Scenario.InHand(engine, 0, Real("mystic_swordsman_lv2"));
        Assert.Equal("Mystic Swordsman LV2 cannot be Set", engine.Validate(new SetMonster(0, swordsman.Id)));
        Assert.DoesNotContain(engine.LegalActions(0), a => a is SetMonster s && s.Card == swordsman.Id);
        Assert.Null(engine.Validate(new NormalSummon(0, swordsman.Id)));

        CardInstance attacker = Scenario.Place(engine, 0, Real("mystic_swordsman_lv2"), Position.FaceUpAttack);
        CardInstance flipper = Scenario.Place(engine, 1, Cards.Flipper, Position.FaceDownDefense);
        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, attacker, flipper);

        Assert.Equal(Location.Graveyard, flipper.Loc);
        Assert.DoesNotContain(engine.Events, e => e is MonsterFlipped f && f.Card == flipper.Id);
        Assert.DoesNotContain(engine.Events, e => e is EffectActivated { EffectId: TestEffects.FlipDraw.EffectId });
        Assert.Contains(engine.Events, e => e is MonsterDestroyed d && d.Card == flipper.Id && d.Reason == DestroyReason.Effect);
        Assert.Equal(8000, engine.State.Player(0).LifePoints);
        Assert.Equal(8000, engine.State.Player(1).LifePoints);
    }

    [Fact]
    public void ReaperOnTheNightmareAttacksDirectlySurvivesBattleAndDiesWhenTargeted()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("book_of_moon") });
        CardInstance reaper = Scenario.Place(engine, 0, Real("reaper_on_the_nightmare"), Position.FaceUpAttack);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);
        int handBefore = engine.State.Player(1).Hand.Count;

        Scenario.EnterBattle(engine);
        Scenario.Submit(engine, new Pass(0)); // the Book of Moon in hand keeps player 0 from being passed for in the Start Step
        Scenario.Attack(engine, reaper, null);
        Scenario.PassUntil(engine, s => s.Window == Window.Open); // the Book of Moon in hand keeps the attack windows from being passed for
        Assert.Equal(8000 - 800, engine.State.Player(1).LifePoints);
        Assert.Equal(handBefore - 1, engine.State.Player(1).Hand.Count);
        Assert.Contains(engine.Events, e => e is EffectActivated { EffectId: ReaperOnTheNightmareEffect.EffectId });

        Scenario.PassUntil(engine, s => s.TurnNumber == 4 && s.Phase == Phase.Main1);
        Scenario.EnterBattle(engine);
        Scenario.Submit(engine, new Pass(0));
        Scenario.Attack(engine, reaper, weakling);
        Scenario.PassUntil(engine, s => s.Window == Window.Open);
        Assert.Equal(Location.MonsterZone, reaper.Loc);
        Assert.Equal(8000 - 700, engine.State.Player(0).LifePoints);

        Scenario.PassUntil(engine, s => s.Phase == Phase.Main2);
        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Real("book_of_moon")).Id));
        Scenario.Answer(engine, reaper.Id);
        Assert.Equal(Location.Graveyard, reaper.Loc);
        Assert.Contains(engine.Events, e => e is MonsterDestroyed d && d.Card == reaper.Id);
    }

    [Fact]
    public void DarkBalterNegatesANormalSpellForLifePointsAndSilencesWhatItDestroys()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.PotOfGreed });
        CardInstance balter = Scenario.Place(engine, 1, Real("dark_balter_the_terrible"), Position.FaceUpAttack);
        CardInstance pot = Scenario.InHand(engine, 0, Cards.PotOfGreed);
        int handBefore = engine.State.Player(0).Hand.Count;

        Scenario.Submit(engine, new ActivateSpell(0, pot.Id));
        Assert.Equal(1, engine.State.Priority);
        Scenario.Submit(engine, new ActivateEffect(1, balter.Id, 0));
        Assert.Equal(8000 - DarkBalterEffect.Cost, engine.State.Player(1).LifePoints);
        Scenario.ResolveChain(engine);

        Assert.Equal(handBefore - 1, engine.State.Player(0).Hand.Count);
        Assert.Equal(Location.Graveyard, pot.Loc);
        Assert.Contains(engine.Events, e => e is ChainLinkNegated n && n.Card == pot.Id);
        Assert.Empty(engine.State.Chain);

        // Sangan destroyed by Dark Balter searches nothing.
        DuelEngine battle = Scenario.AtPlayerZeroTurnTwo();
        CardInstance attacker = Scenario.Place(battle, 0, Real("dark_balter_the_terrible"), Position.FaceUpAttack);
        CardInstance sangan = Scenario.Place(battle, 1, Real("sangan"), Position.FaceUpAttack);
        Scenario.InDeck(battle, 1, Cards.Weakling);
        Scenario.EnterBattle(battle);
        Scenario.Attack(battle, attacker, sangan);
        Assert.Equal(Location.Graveyard, sangan.Loc);
        Assert.Null(battle.State.PendingChoice);
        Assert.DoesNotContain(battle.Events, e => e is EffectActivated { EffectId: SanganEffect.EffectId });
    }

    // ----- Battle triggers -----

    [Fact]
    public void DonZaloogDiscardsOrMillsAfterBattleDamage()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance don = Scenario.Place(engine, 0, Real("don_zaloog"), Position.FaceUpAttack);
        PlayerState opponent = engine.State.Player(1);
        int handBefore = opponent.Hand.Count;
        int deckBefore = opponent.Deck.Count;

        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, don, null);
        PendingChoice ask = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.OptionalTrigger, ask.Kind);
        Scenario.Answer(engine, don.Id);
        Scenario.ResolveChain(engine);
        PendingChoice mode = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Resolution, mode.Kind);
        Assert.Equal(new[] { don.Id, opponent.Deck[^1].Id }, mode.Choice.Options);
        Scenario.Answer(engine, don.Id);
        Assert.Equal(handBefore - 1, opponent.Hand.Count);
        Assert.Equal(deckBefore, opponent.Deck.Count);

        Scenario.PassUntil(engine, s => s.TurnNumber == 4 && s.Phase == Phase.Main1);
        deckBefore = opponent.Deck.Count;
        handBefore = opponent.Hand.Count;
        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, don, null);
        Scenario.Answer(engine, don.Id);
        Scenario.ResolveChain(engine);
        Scenario.Answer(engine, opponent.Deck[^1].Id);
        Assert.Equal(deckBefore - DonZaloogEffect.MillCount, opponent.Deck.Count);
        Assert.Equal(handBefore, opponent.Hand.Count);
    }

    [Fact]
    public void DDWarriorLadyBanishesBothAfterBattleEvenWhenSheLoses()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance lady = Scenario.Place(engine, 0, Real("dd_warrior_lady"), Position.FaceUpAttack);
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);

        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, lady, elf);
        Assert.Equal(8000 - 400, engine.State.Player(0).LifePoints);
        Assert.Equal(Location.Graveyard, lady.Loc);
        PendingChoice ask = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.OptionalTrigger, ask.Kind);
        Assert.Equal(0, ask.Player);
        Scenario.Answer(engine, lady.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Banished, lady.Loc);
        Assert.Equal(Location.Banished, elf.Loc);
    }

    [Fact]
    public void DDAssailantTakesItsDestroyerAlong()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance assailant = Scenario.Place(engine, 0, Real("dd_assailant"), Position.FaceUpAttack);
        CardInstance skull = Scenario.Place(engine, 1, Cards.SummonedSkull, Position.FaceUpAttack);

        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, assailant, skull);

        Assert.Equal(Location.Banished, assailant.Loc);
        Assert.Equal(Location.Banished, skull.Loc);
        Assert.Contains(engine.Events, e => e is EffectActivated { EffectId: DDAssailantEffect.EffectId });
    }

    [Fact]
    public void AirknightParshathPiercesAndDraws()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance airknight = Scenario.Place(engine, 0, Real("airknight_parshath"), Position.FaceUpAttack);
        CardInstance filler = Scenario.Place(engine, 1, Cards.Filler, Position.FaceUpDefense);
        int handBefore = engine.State.Player(0).Hand.Count;

        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, airknight, filler);

        Assert.Equal(8000 - (1900 - 800), engine.State.Player(1).LifePoints);
        Assert.Equal(handBefore + 1, engine.State.Player(0).Hand.Count);
        Assert.Equal(Location.Graveyard, filler.Loc);
    }

    [Fact]
    public void KycooBanishesFromTheOpponentsGraveyardAndLocksTheirBanishing()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance kycoo = Scenario.Place(engine, 0, Real("kycoo_the_ghost_destroyer"), Position.FaceUpAttack);
        CardInstance first = Scenario.InGraveyard(engine, 1, Cards.GeminiElf);
        CardInstance second = Scenario.InGraveyard(engine, 1, Cards.Weakling);
        CardInstance third = Scenario.InGraveyard(engine, 1, Cards.Wall);
        Assert.True(engine.State.Player(1).Has(PlayerRestriction.CannotBanishFromGraveyard));
        Assert.False(engine.State.Player(0).Has(PlayerRestriction.CannotBanishFromGraveyard));

        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, kycoo, null);
        Scenario.Answer(engine, kycoo.Id);
        Scenario.ResolveChain(engine);
        PendingChoice pick = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal((1, 2), (pick.Choice.Min, pick.Choice.Max));
        Scenario.Answer(engine, first.Id, third.Id);

        Assert.Equal(Location.Banished, first.Loc);
        Assert.Equal(Location.Graveyard, second.Loc);
        Assert.Equal(Location.Banished, third.Loc);
    }

    [Theory]
    [InlineData("mystic_tomato", "sangan")]
    [InlineData("shining_angel", "mystical_elf")]
    public void RecruitersSummonAReplacementFromTheDeck(string recruiterId, string replacementId)
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance recruiter = Scenario.Place(engine, 0, Real(recruiterId), Position.FaceUpAttack);
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance replacement = Scenario.InDeck(engine, 0, Real(replacementId));
        Scenario.InDeck(engine, 0, Cards.Titan);

        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, recruiter, elf);
        Assert.Equal(Location.Graveyard, recruiter.Loc);
        PendingChoice ask = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.OptionalTrigger, ask.Kind);
        Scenario.Answer(engine, recruiter.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.MonsterZone, replacement.Loc);
        Assert.Equal(Position.FaceUpAttack, replacement.Pos);
        Assert.Contains(engine.Events, e => e is MonsterSpecialSummoned s && s.Card == replacement.Id && s.From == Location.Deck);
        Assert.Contains(engine.Events, e => e is DeckShuffled { Player: 0 });
    }

    // ----- Counters, summons and the Graveyard -----

    [Fact]
    public void BreakerGetsACounterWhenNormalSummonedAndSpendsItOnASpellOrTrap()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("breaker_the_magical_warrior") });
        CardInstance trap = Scenario.Set(engine, 1, Cards.AttackTrap);
        CardInstance breaker = Scenario.InHand(engine, 0, Real("breaker_the_magical_warrior"));

        Scenario.Submit(engine, new NormalSummon(0, breaker.Id));
        Scenario.ResolveChain(engine);
        Assert.Equal(1, breaker.Counter(BreakerEffect.Counter));
        Assert.Equal(1600 + BreakerEffect.AtkPerCounter, breaker.Atk);

        Scenario.Submit(engine, new ActivateEffect(0, breaker.Id, 1));
        Assert.Equal(0, breaker.Counter(BreakerEffect.Counter));
        Scenario.Answer(engine, trap.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Graveyard, trap.Loc);
        Assert.Equal(1600, breaker.Atk);
        Assert.Equal("Breaker the Magical Warrior cannot be activated now", engine.Validate(new ActivateEffect(0, breaker.Id, 1)));
    }

    [Fact]
    public void ChaosSorcererIsSummonedFromTheHandByBanishingLightAndDark()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("chaos_sorcerer") });
        CardInstance sorcerer = Scenario.InHand(engine, 0, Real("chaos_sorcerer"));
        CardInstance light = Scenario.InGraveyard(engine, 0, Real("mystical_elf"));
        CardInstance dark = Scenario.InGraveyard(engine, 0, Real("sangan"));
        CardInstance tribute = Scenario.Place(engine, 0, Cards.Filler, Position.FaceUpAttack);
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);

        Assert.Equal("Chaos Sorcerer cannot be Normal Summoned", engine.Validate(new NormalSummon(0, sorcerer.Id, new[] { tribute.Id })));
        Assert.Equal("Chaos Sorcerer cannot be Set", engine.Validate(new SetMonster(0, sorcerer.Id, new[] { tribute.Id })));
        Assert.Contains(engine.LegalActions(0), a => a is ActivateEffect e && e.Card == sorcerer.Id);

        Scenario.Submit(engine, new ActivateEffect(0, sorcerer.Id, 0));
        PendingChoice lightCost = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Cost, lightCost.Kind);
        Assert.Equal(new[] { light.Id }, lightCost.Choice.Options);
        Scenario.Answer(engine, light.Id);
        Assert.Equal(new[] { dark.Id }, engine.State.PendingChoice!.Choice.Options);
        Scenario.Answer(engine, dark.Id);

        Assert.Equal(Location.MonsterZone, sorcerer.Loc);
        Assert.Equal(Position.FaceUpAttack, sorcerer.Pos);
        Assert.Equal(Location.Banished, light.Loc);
        Assert.Equal(Location.Banished, dark.Loc);
        Assert.Empty(engine.State.Chain);
        Assert.DoesNotContain(engine.Events, e => e is ChainLinkAdded a && a.Card == sorcerer.Id);
        Assert.Equal(SummonKind.Special, engine.State.LastSummon);
        Scenario.PassUntil(engine, s => s.Window == Window.Open);

        // Once per turn: banish a face-up monster and give up attacking.
        Scenario.Submit(engine, new ActivateEffect(0, sorcerer.Id, 1));
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(Location.Banished, elf.Loc);
        Assert.True(sorcerer.Has(Restriction.CannotAttack));
        Assert.Equal("Chaos Sorcerer's effect was already used this turn", engine.Validate(new ActivateEffect(0, sorcerer.Id, 1)));
    }

    [Fact]
    public void KycooStopsChaosSorcerersSummon()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("chaos_sorcerer") });
        CardInstance sorcerer = Scenario.InHand(engine, 0, Real("chaos_sorcerer"));
        Scenario.InGraveyard(engine, 0, Real("mystical_elf"));
        Scenario.InGraveyard(engine, 0, Real("sangan"));
        Scenario.Place(engine, 1, Real("kycoo_the_ghost_destroyer"), Position.FaceUpAttack);

        Assert.Equal("Chaos Sorcerer cannot be activated now", engine.Validate(new ActivateEffect(0, sorcerer.Id, 0)));
    }

    [Fact]
    public void SkilledDarkMagicianCountsSpellsAndTradesThreeCountersForADarkMagician()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.PotOfGreed, Cards.PotOfGreed, Cards.PotOfGreed, Cards.PotOfGreed }, configure: _noHandLimit);
        CardInstance magician = Scenario.Place(engine, 0, Real("skilled_dark_magician"), Position.FaceUpAttack);
        CardInstance darkMagician = Scenario.InDeck(engine, 0, _darkMagician);
        Assert.Equal("Skilled Dark Magician cannot be activated now", engine.Validate(new ActivateEffect(0, magician.Id, 1)));

        for (int i = 1; i <= 4; i++)
        {
            Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Cards.PotOfGreed).Id));
            Scenario.ResolveChain(engine);
            Assert.Equal(Math.Min(i, SkilledDarkMagicianCounterEffect.MaximumCounters), magician.Counter(SkilledDarkMagicianCounterEffect.Counter));
        }

        Scenario.Submit(engine, new ActivateEffect(0, magician.Id, 1));
        Assert.Equal(Location.Graveyard, magician.Loc);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.MonsterZone, darkMagician.Loc);
        Assert.Contains(engine.Events, e => e is MonsterSpecialSummoned s && s.Card == darkMagician.Id && s.From == Location.Deck);
    }

    [Fact]
    public void TribeInfectingVirusDiscardsAndWipesOneType()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance virus = Scenario.Place(engine, 0, Real("tribe_infecting_virus"), Position.FaceUpAttack);
        CardInstance squire = Scenario.Place(engine, 0, Cards.Squire, Position.FaceUpAttack);
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance celtic = Scenario.Place(engine, 1, Real("celtic_guardian"), Position.FaceUpAttack);
        CardInstance hidden = Scenario.Place(engine, 1, Cards.Squire, Position.FaceDownDefense);
        int handBefore = engine.State.Player(0).Hand.Count;

        Scenario.Submit(engine, new ActivateEffect(0, virus.Id, 0));
        PendingChoice discard = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Cost, discard.Kind);
        Scenario.Answer(engine, discard.Choice.Options[0]);
        PendingChoice type = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(new[] { virus.Id, squire.Id, elf.Id }, type.Choice.Options);
        Scenario.Answer(engine, squire.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(handBefore - 1, engine.State.Player(0).Hand.Count);
        Assert.Equal(Location.Graveyard, squire.Loc);
        Assert.Equal(Location.Graveyard, celtic.Loc);
        Assert.Equal(Location.MonsterZone, elf.Loc);
        Assert.Equal(Location.MonsterZone, hidden.Loc);
        Assert.Equal(Location.MonsterZone, virus.Loc);
    }

    [Fact]
    public void SinisterSerpentComesBackInItsOwnersStandbyPhase()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(configure: _noHandLimit);
        CardInstance serpent = Scenario.InGraveyard(engine, 0, Real("sinister_serpent"));

        Scenario.PassUntil(engine, s => s.PendingChoice is not null);
        Assert.Equal(4, engine.State.TurnNumber);
        Assert.Equal(Phase.Standby, engine.State.Phase);
        PendingChoice ask = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.OptionalTrigger, ask.Kind);
        Assert.Equal(serpent.Id, ask.Source);
        Scenario.Answer(engine, serpent.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Hand, serpent.Loc);
    }

    [Fact]
    public void TsukuyomiFlipsAMonsterFaceDownWhenSummonedAndGoesHome()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("tsukuyomi") });
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance tsukuyomi = Scenario.InHand(engine, 0, Real("tsukuyomi"));

        Scenario.Submit(engine, new NormalSummon(0, tsukuyomi.Id));
        PendingChoice target = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Target, target.Kind);
        Assert.Equal(new[] { tsukuyomi.Id, elf.Id }, target.Choice.Options);
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(Position.FaceDownDefense, elf.Pos);

        Scenario.PassUntil(engine, s => s.TurnNumber == 3);
        Assert.Equal(Location.Hand, tsukuyomi.Loc);
        Assert.Contains(engine.Events, e => e is SpiritReturned r && r.Card == tsukuyomi.Id);
    }

    [Fact]
    public void TsukuyomiFiresOnceWhenFlipSummonedAndWhenFlippedByBattle()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance tsukuyomi = Scenario.Place(engine, 0, Real("tsukuyomi"), Position.FaceDownDefense);
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);

        Scenario.Submit(engine, new FlipSummon(0, tsukuyomi.Id));
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(Position.FaceDownDefense, elf.Pos);
        Assert.Equal(1, engine.Events.Count(e => e is EffectActivated { EffectId: TsukuyomiEffect.EffectId }));

        DuelEngine battle = Scenario.AtPlayerZeroTurnTwo();
        CardInstance hidden = Scenario.Place(battle, 1, Real("tsukuyomi"), Position.FaceDownDefense);
        CardInstance filler = Scenario.Place(battle, 0, Cards.Filler, Position.FaceUpAttack);
        Scenario.EnterBattle(battle);
        Scenario.Attack(battle, filler, hidden);
        Assert.Equal(8000 - 200, battle.State.Player(0).LifePoints);
        PendingChoice target = Assert.IsType<PendingChoice>(battle.State.PendingChoice);
        Assert.Equal(1, target.Player);
        Scenario.Answer(battle, filler.Id);
        Scenario.ResolveChain(battle);
        Assert.Equal(Position.FaceDownDefense, filler.Pos);
        Assert.Equal(1, battle.Events.Count(e => e is EffectActivated { EffectId: TsukuyomiEffect.EffectId }));
    }

    // ----- Spells -----

    [Fact]
    public void DelinquentDuoTakesARandomCardThenOneOfTheOpponentsChoice()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("delinquent_duo") });
        PlayerState opponent = engine.State.Player(1);
        int handBefore = opponent.Hand.Count;

        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Real("delinquent_duo")).Id));
        Assert.Equal(8000 - DelinquentDuoEffect.Cost, engine.State.Player(0).LifePoints);
        Scenario.ResolveChain(engine);

        Assert.Equal(handBefore - 1, opponent.Hand.Count);
        PendingChoice pick = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Resolution, pick.Kind);
        Assert.Equal(1, pick.Player);
        Assert.Equal(opponent.Hand.Select(c => c.Id), pick.Choice.Options);
        Assert.Empty(engine.LegalActions(0));
        CardInstance chosen = opponent.Hand[0];
        Scenario.Answer(engine, chosen.Id);

        Assert.Equal(Location.Graveyard, chosen.Loc);
        Assert.Equal(handBefore - 2, opponent.Hand.Count);
        Assert.Equal(2, engine.Events.Count(e => e is CardDiscarded { Player: 1 }));
    }

    [Fact]
    public void PrematureBurialRevivesForLifePointsAndTakesTheMonsterWhenItLeaves()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("premature_burial") });
        CardInstance elf = Scenario.InGraveyard(engine, 0, Cards.GeminiElf);
        CardInstance burial = Scenario.InHand(engine, 0, Real("premature_burial"));

        Scenario.Submit(engine, new ActivateSpell(0, burial.Id));
        Assert.Equal(8000 - PrematureBurialEffect.Cost, engine.State.Player(0).LifePoints);
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(Location.MonsterZone, elf.Loc);
        Assert.Equal(elf.Id, burial.EquippedTo);

        engine.Destroy(burial, DestroyReason.Effect);
        Assert.Equal(Location.Graveyard, elf.Loc);
    }

    [Fact]
    public void PrematureBurialLeavesAMonsterFlippedFaceDownAlone()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("premature_burial") });
        CardInstance elf = Scenario.InGraveyard(engine, 0, Cards.GeminiElf);
        CardInstance burial = Scenario.InHand(engine, 0, Real("premature_burial"));
        Scenario.Submit(engine, new ActivateSpell(0, burial.Id));
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);

        engine.FlipFaceDown(elf);

        Assert.Equal(Location.Graveyard, burial.Loc);
        Assert.Equal(Location.MonsterZone, elf.Loc);
        Assert.Equal(Position.FaceDownDefense, elf.Pos);
    }

    [Fact]
    public void SnatchStealTakesAMonsterAndPaysTheOpponentEachStandbyPhase()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("snatch_steal") }, configure: _noHandLimit);
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance snatch = Scenario.InHand(engine, 0, Real("snatch_steal"));

        Scenario.Submit(engine, new ActivateSpell(0, snatch.Id));
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(0, elf.Controller);
        Assert.Equal(elf.Id, snatch.EquippedTo);

        Scenario.PassUntil(engine, s => s.TurnNumber == 3 && s.Phase == Phase.Main1);
        Assert.Equal(8000 + SnatchStealUpkeepEffect.Amount, engine.State.Player(1).LifePoints);
        Assert.Equal(8000, engine.State.Player(0).LifePoints);
        Scenario.PassUntil(engine, s => s.TurnNumber == 4 && s.Phase == Phase.Main1);
        Assert.Equal(8000 + SnatchStealUpkeepEffect.Amount, engine.State.Player(1).LifePoints);

        engine.Destroy(snatch, DestroyReason.Effect);
        Assert.Equal(1, elf.Controller);
        Assert.Same(elf, engine.State.Player(1).MonsterZones[elf.ZoneIndex]);
    }

    [Fact]
    public void NoblemanOfCrossoutBanishesAFaceDownMonsterAndEveryCopyOfAFlipMonster()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("nobleman_of_crossout"), Real("nobleman_of_crossout") });
        CardInstance magician = Scenario.Place(engine, 1, Real("magician_of_faith"), Position.FaceDownDefense);
        CardInstance theirCopy = Scenario.InDeck(engine, 1, Real("magician_of_faith"));
        CardInstance myCopy = Scenario.InDeck(engine, 0, Real("magician_of_faith"));
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceDownDefense);

        Scenario.Submit(engine, new ActivateSpell(0, engine.State.Player(0).Hand.First(c => c.Def == Real("nobleman_of_crossout")).Id));
        PendingChoice target = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(new[] { magician.Id, elf.Id }, target.Choice.Options);
        Scenario.Answer(engine, magician.Id);
        Scenario.ResolveChain(engine);
        Assert.All(new[] { magician, theirCopy, myCopy }, c => Assert.Equal(Location.Banished, c.Loc));
        Assert.Equal(2, engine.Events.Count(e => e is DeckShuffled));

        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Real("nobleman_of_crossout")).Id));
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(Location.Banished, elf.Loc);
        Assert.Equal(2, engine.Events.Count(e => e is DeckShuffled));
    }

    [Fact]
    public void EnemyControllerSwitchesAPositionOrTributesForControl()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("enemy_controller"), Real("enemy_controller") });
        CardInstance filler = Scenario.Place(engine, 0, Cards.Filler, Position.FaceUpAttack);
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);

        Scenario.Submit(engine, new ActivateSpell(0, engine.State.Player(0).Hand.First(c => c.Def == Real("enemy_controller")).Id));
        PendingChoice mode = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Cost, mode.Kind);
        Assert.Equal((0, 1), (mode.Choice.Min, mode.Choice.Max));
        Scenario.Answer(engine);
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(Position.FaceUpDefense, elf.Pos);
        Assert.Equal(1, elf.Controller);

        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Real("enemy_controller")).Id));
        Scenario.Answer(engine, filler.Id);
        Assert.Equal(Location.Graveyard, filler.Loc);
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(0, elf.Controller);
        Assert.Equal(engine.State.TurnNumber, elf.ControlReturnsAfterTurn);

        Scenario.PassUntil(engine, s => s.Phase == Phase.End);
        Assert.Equal(1, elf.Controller);
    }

    [Fact]
    public void ScapegoatSummonsFourTokensAndForbidsOtherSummonsThisTurn()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("scapegoat"), Cards.GeminiElf, Cards.SummonedSkull }, configure: _noHandLimit);
        CardInstance elf = Scenario.InHand(engine, 0, Cards.GeminiElf);

        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Real("scapegoat")).Id));
        Scenario.ResolveChain(engine);

        var tokens = engine.State.Player(0).Monsters.ToList();
        Assert.Equal(ScapegoatEffect.TokenCount, tokens.Count);
        Assert.All(tokens, t => Assert.True(t.IsToken && t.Pos == Position.FaceUpDefense && t.Has(Restriction.CannotBeTributed)));
        Assert.True(engine.State.Player(0).Has(PlayerRestriction.CannotSummon));
        Assert.Equal("you cannot Summon this turn", engine.Validate(new NormalSummon(0, elf.Id)));
        Assert.Null(engine.Validate(new SetMonster(0, elf.Id)));

        Scenario.PassUntil(engine, s => s.TurnNumber == 4 && s.Phase == Phase.Main1);
        Assert.False(engine.State.Player(0).Has(PlayerRestriction.CannotSummon));
        Assert.Null(engine.Validate(new NormalSummon(0, elf.Id)));
        Assert.Equal($"{tokens[0].Def.Name} cannot be tributed", engine.Validate(new NormalSummon(0, Scenario.InHand(engine, 0, Cards.SummonedSkull).Id, new[] { tokens[0].Id })));
    }

    [Fact]
    public void MetamorphosisTradesAMonsterForAFusionOfTheSameLevel()
    {
        CardDefinition balter = Real("dark_balter_the_terrible");
        CardDefinition reaper = Real("reaper_on_the_nightmare");
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("metamorphosis") }, fusion0: new[] { balter, reaper });
        CardInstance airknight = Scenario.Place(engine, 0, Real("airknight_parshath"), Position.FaceUpAttack);
        Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);

        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Real("metamorphosis")).Id));
        PendingChoice tribute = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Cost, tribute.Kind);
        Assert.Equal(new[] { airknight.Id }, tribute.Choice.Options);
        Scenario.Answer(engine, airknight.Id);
        Assert.Equal(Location.Graveyard, airknight.Loc);
        Scenario.ResolveChain(engine);
        PendingChoice pick = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Resolution, pick.Kind);
        CardInstance chosen = engine.State.Player(0).FusionDeck.First(f => f.Def == balter);
        Assert.Contains(chosen.Id, pick.Choice.Options);
        Scenario.Answer(engine, chosen.Id);

        Assert.Equal(Location.MonsterZone, chosen.Loc);
        Assert.Contains(engine.Events, e => e is MonsterSpecialSummoned s && s.Card == chosen.Id && s.From == Location.FusionDeck);
        Assert.Single(engine.State.Player(0).FusionDeck);
    }

    [Fact]
    public void SwordsOfRevealingLightFlipsAndHoldsTheOpponentForThreeTurns()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("swords_of_revealing_light") }, configure: _noHandLimit);
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance flipper = Scenario.Place(engine, 1, Cards.Flipper, Position.FaceDownDefense);
        CardInstance swords = Scenario.InHand(engine, 0, Real("swords_of_revealing_light"));
        int handBefore = engine.State.Player(1).Hand.Count;

        Scenario.Submit(engine, new ActivateSpell(0, swords.Id));
        Scenario.ResolveChain(engine);

        Assert.Equal(Position.FaceUpDefense, flipper.Pos);
        Assert.Equal(handBefore + 1, engine.State.Player(1).Hand.Count);
        Assert.Equal(Location.SpellTrapZone, swords.Loc);
        Assert.True(swords.IsFaceUp);
        Assert.Equal(engine.State.TurnNumber + SwordsOfRevealingLightEffect.Duration, swords.LeavesAfterTurn);
        Assert.True(elf.Has(Restriction.CannotAttack));

        Scenario.PassUntil(engine, s => s.TurnNumber == 3 && s.Phase == Phase.Main1);
        Scenario.EnterBattle(engine);
        Assert.Equal("Gemini Elf cannot attack", engine.Validate(new DeclareAttack(1, elf.Id, null)));

        Scenario.PassUntil(engine, s => s.TurnNumber == 7 && s.Phase == Phase.Main1);
        Assert.Equal(Location.SpellTrapZone, swords.Loc);
        Scenario.PassUntil(engine, s => s.TurnNumber == 8);
        Assert.Equal(Location.Graveyard, swords.Loc);
        Assert.False(elf.Has(Restriction.CannotAttack));
        Assert.Contains(engine.Events, e => e is SpellTrapDestroyed d && d.Card == swords.Id);
    }

    [Fact]
    public void CreatureSwapExchangesTwoMonstersAndLocksTheirPositions()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("creature_swap") });
        CardInstance weakling = Scenario.Place(engine, 0, Cards.Weakling, Position.FaceUpAttack);
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance wall = Scenario.Place(engine, 1, Cards.Wall, Position.FaceUpDefense);

        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Real("creature_swap")).Id));
        Scenario.ResolveChain(engine);
        PendingChoice mine = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(0, mine.Player);
        Assert.Equal(new[] { weakling.Id, elf.Id }, mine.Choice.Options);
        Scenario.Answer(engine, weakling.Id);

        Assert.Null(engine.State.PendingChoice);
        Assert.Equal(1, weakling.Controller);
        Assert.Equal(0, wall.Controller);
        Assert.Same(wall, engine.State.Player(0).MonsterZones[wall.ZoneIndex]);
        Assert.Same(weakling, engine.State.Player(1).MonsterZones[weakling.ZoneIndex]);
        Assert.True(wall.Has(Restriction.CannotChangePosition));
        Assert.Equal("Wall cannot change its battle position", engine.Validate(new ChangePosition(0, wall.Id)));
        Assert.Equal(2, engine.Events.Count(e => e is ControlChanged));

        Scenario.PassUntil(engine, s => s.TurnNumber == 3);
        Assert.Equal(1, weakling.Controller);
        Assert.False(wall.Has(Restriction.CannotChangePosition));
    }

    // ----- Traps -----

    [Fact]
    public void RingOfDestructionBurnsBothPlayersForTheMonstersAtk()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.GeminiElf });
        CardInstance ring = Scenario.Set(engine, 1, Real("ring_of_destruction"));
        CardInstance elf = Scenario.InHand(engine, 0, Cards.GeminiElf);

        Scenario.Submit(engine, new NormalSummon(0, elf.Id));
        Assert.Equal(1, engine.State.Priority);
        Scenario.Submit(engine, new ActivateTrap(1, ring.Id));
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Graveyard, elf.Loc);
        Assert.All(engine.State.Players, p => Assert.Equal(8000 - 1900, p.LifePoints));
        Assert.Equal(2, engine.Events.Count(e => e is EffectDamage { Amount: 1900 }));
    }

    [Fact]
    public void CallOfTheHauntedRevivesAndTheTwoLeaveTogether()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance call = Scenario.Set(engine, 0, Real("call_of_the_haunted"));
        CardInstance elf = Scenario.InGraveyard(engine, 0, Cards.GeminiElf);

        Scenario.Submit(engine, new ActivateTrap(0, call.Id));
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(Location.MonsterZone, elf.Loc);
        Assert.Equal(Position.FaceUpAttack, elf.Pos);
        Assert.Equal(Location.SpellTrapZone, call.Loc);
        Assert.Equal(elf.Id, call.EquippedTo);

        engine.Destroy(elf, DestroyReason.Effect);
        Assert.Equal(Location.Graveyard, call.Loc);

        CardInstance second = Scenario.Set(engine, 0, Real("call_of_the_haunted"));
        Scenario.Submit(engine, new ActivateTrap(0, second.Id));
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(Location.MonsterZone, elf.Loc);
        engine.Destroy(second, DestroyReason.Effect);
        Assert.Equal(Location.Graveyard, elf.Loc);
    }

    [Fact]
    public void CallOfTheHauntedLeavesAMonsterFlippedFaceDownAlone()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance call = Scenario.Set(engine, 0, Real("call_of_the_haunted"));
        CardInstance elf = Scenario.InGraveyard(engine, 0, Cards.GeminiElf);
        Scenario.Submit(engine, new ActivateTrap(0, call.Id));
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);

        engine.FlipFaceDown(elf);

        Assert.Equal(Location.Graveyard, call.Loc);
        Assert.Equal(Location.MonsterZone, elf.Loc);
    }

    [Fact]
    public void BottomlessTrapHoleBanishesBigSummonsOnly()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.GeminiElf, Cards.Filler }, configure: _noHandLimit);
        CardInstance hole = Scenario.Set(engine, 1, Real("bottomless_trap_hole"));
        CardInstance elf = Scenario.InHand(engine, 0, Cards.GeminiElf);

        Scenario.Submit(engine, new NormalSummon(0, elf.Id));
        Assert.Equal(1, engine.State.Priority);
        Scenario.Submit(engine, new ActivateTrap(1, hole.Id));
        Scenario.ResolveChain(engine);
        Assert.Equal(Location.Banished, elf.Loc);
        Assert.Contains(engine.Events, e => e is MonsterDestroyed d && d.Card == elf.Id);
        Assert.Equal(Location.Graveyard, hole.Loc);

        CardInstance second = Scenario.Set(engine, 1, Real("bottomless_trap_hole"));
        Scenario.PassUntil(engine, s => s.TurnNumber == 4 && s.Phase == Phase.Main1);
        Scenario.Submit(engine, new NormalSummon(0, Scenario.InHand(engine, 0, Cards.Filler).Id));
        Assert.Equal(Window.Open, engine.State.Window);
        Assert.Equal(Location.SpellTrapZone, second.Loc);
    }

    [Fact]
    public void WabokuStopsBattleDamageAndBattleDestructionForTheTurn()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);
        CardInstance waboku = Scenario.Set(engine, 1, Real("waboku"));

        Scenario.EnterBattle(engine);
        Scenario.Submit(engine, new Pass(1)); // the live Waboku keeps player 1 from being passed for in the Start Step
        Scenario.Attack(engine, elf, weakling);
        Scenario.Submit(engine, new ActivateTrap(1, waboku.Id));
        Scenario.ResolveChain(engine);
        Scenario.PassUntil(engine, s => s.Window == Window.Open);

        Assert.Equal(Location.MonsterZone, weakling.Loc);
        Assert.Equal(8000, engine.State.Player(1).LifePoints);
        Assert.Equal(Location.Graveyard, waboku.Loc);
        Scenario.PassUntil(engine, s => s.TurnNumber == 3);
        Assert.False(engine.State.Player(1).Has(PlayerRestriction.NoBattleDamage));
    }

    // ----- Acceptance: the three tier 3 lists are playable by the heuristic agents -----

    [Theory]
    [InlineData("rookie_beatdown", "rookie_beatdown", 1UL)]
    [InlineData("rookie_beatdown", "rookie_beatdown", 2UL)]
    [InlineData("beatdown", "beatdown", 1UL)]
    [InlineData("beatdown", "beatdown", 2UL)]
    [InlineData("warrior_toolbox", "warrior_toolbox", 1UL)]
    [InlineData("warrior_toolbox", "warrior_toolbox", 2UL)]
    [InlineData("beatdown", "warrior_toolbox", 3UL)]
    [InlineData("warrior_toolbox", "rookie_beatdown", 4UL)]
    public void HeuristicAgentsPlayTheRealListsToAWinner(string deck0, string deck1, ulong seed)
    {
        DuelEngine engine = DuelEngine.Start(RealDeck(deck0), RealDeck(deck1), new DuelOptions { Seed = seed });

        int steps = DuelRunner.Play(engine, new HeuristicAgent(AiProfile.Nico, seed), new HeuristicAgent(AiProfile.Mara, seed + 100));

        Assert.True(engine.State.IsOver, $"duel still running after {steps} commands");
        Assert.NotNull(engine.State.Winner);
        Assert.InRange(engine.State.TurnNumber, 3, 120);
    }

    /// <summary>A list from <c>data/decks/</c> with its tier 4 cards (Cyber Jar, issue #58) dropped and the slots filled with its own vanilla monsters.</summary>
    internal static Deck RealDeck(string id)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Cards.DecksDirectory, id + ".json")));
        var main = new List<CardDefinition>();
        foreach (JsonProperty entry in doc.RootElement.GetProperty("main").EnumerateObject())
        {
            CardDefinition card = Cards.Real(entry.Name);
            if (card.Tier <= 3)
            {
                main.AddRange(Enumerable.Repeat(card, entry.Value.GetInt32()));
            }
        }

        var filler = main.Where(c => c.IsVanilla).Distinct().ToList();
        for (int i = 0; main.Count < Scenario.DeckSize; i++)
        {
            main.Add(filler[i % filler.Count]);
        }

        var fusion = doc.RootElement.GetProperty("fusion").EnumerateObject()
            .Select(entry => (Card: Cards.Real(entry.Name), Count: entry.Value.GetInt32()))
            .Where(f => f.Card.Tier <= 3)
            .SelectMany(f => Enumerable.Repeat(f.Card, f.Count))
            .ToList();
        return new Deck(main, fusion);
    }
}
