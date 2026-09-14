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

/// <summary>Issue #56: one scenario per tier 2 card (systems.md §5.6), played through the real definitions in <c>data/cards/</c>.</summary>
public class TierTwoTests
{
    private static readonly Func<DuelOptions, DuelOptions> _noHandLimit = o => o with { HandLimit = 20 };

    private static CardDefinition Real(string id) => Cards.Real(id);

    // ----- Draw and discard -----

    [Fact]
    public void GracefulCharityDrawsThreeThenAsksForTwoDiscards()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("graceful_charity"), Cards.Weakling, Cards.Wall, Cards.Titan });
        PlayerState p = engine.State.Player(0);
        int handBefore = p.Hand.Count;
        CardInstance charity = Scenario.InHand(engine, 0, Real("graceful_charity"));

        Scenario.Submit(engine, new ActivateSpell(0, charity.Id));
        Scenario.ResolveChain(engine);

        // The chain link stopped mid-resolution: 3 cards drawn, the discard is asked, nothing else may happen.
        PendingChoice choice = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Resolution, choice.Kind);
        Assert.Equal(0, choice.Player);
        Assert.Equal(charity.Id, choice.Source);
        Assert.Equal((2, 2), (choice.Choice.Min, choice.Choice.Max));
        Assert.Equal(handBefore - 1 + 3, p.Hand.Count);
        Assert.Same(charity, engine.State.ResolvingLink!.Source);
        Assert.Equal(Location.SpellTrapZone, charity.Loc);
        Assert.All(engine.LegalActions(0), a => Assert.IsType<AnswerChoice>(a));
        Assert.Empty(engine.LegalActions(1));

        CardInstance weakling = Scenario.InHand(engine, 0, Cards.Weakling);
        CardInstance wall = Scenario.InHand(engine, 0, Cards.Wall);
        Scenario.Answer(engine, weakling.Id, wall.Id);

        Assert.Null(engine.State.ResolvingLink);
        Assert.Null(engine.State.PendingChoice);
        Assert.Equal(handBefore, p.Hand.Count);
        Assert.Equal(new[] { weakling, wall, charity }, p.Graveyard.TakeLast(3));
        Assert.Contains(engine.Events, e => e is ChainLinkResolved r && r.Card == charity.Id);
        Assert.Equal(Phase.Main1, engine.State.Phase);
        Assert.Equal(0, engine.State.Priority);
    }

    [Fact]
    public void GracefulCharityNeedsThreeCardsInTheDeck()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("graceful_charity") });
        CardInstance charity = Scenario.InHand(engine, 0, Real("graceful_charity"));
        engine.State.Player(0).Deck.RemoveRange(0, engine.State.Player(0).Deck.Count - 2);

        Assert.Equal("Graceful Charity cannot be activated now", engine.Validate(new ActivateSpell(0, charity.Id)));
    }

    [Fact]
    public void MorphingJarDiscardsBothHandsAndDrawsFive()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("morphing_jar") }, configure: _noHandLimit);
        CardInstance jar = Scenario.InHand(engine, 0, Real("morphing_jar"));
        Scenario.Submit(engine, new SetMonster(0, jar.Id));
        Scenario.PassUntil(engine, s => s.TurnNumber == 4 && s.Phase == Phase.Main1);
        var hands = engine.State.Players.Select(p => p.Hand.ToList()).ToList();

        Scenario.Submit(engine, new FlipSummon(0, jar.Id));
        Scenario.ResolveChain(engine);

        Assert.All(engine.State.Players, p => Assert.Equal(5, p.Hand.Count));
        Assert.All(hands[0], c => Assert.Equal(Location.Graveyard, c.Loc));
        Assert.All(hands[1], c => Assert.Equal(Location.Graveyard, c.Loc));
        Assert.Equal(hands[0].Count + hands[1].Count, engine.Events.Count(e => e is CardDiscarded));
        Assert.Equal(Location.MonsterZone, jar.Loc);
    }

    // ----- Destroy -----

    [Fact]
    public void FissureDestroysTheWeakestFaceUpMonsterAndAsksOnATie()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("fissure"), Real("fissure") });
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);
        CardInstance hidden = Scenario.Place(engine, 1, Cards.Filler, Position.FaceDownDefense);

        Scenario.Submit(engine, new ActivateSpell(0, engine.State.Player(0).Hand.First(c => c.Def == Real("fissure")).Id));
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Graveyard, weakling.Loc);
        Assert.Equal(Location.MonsterZone, elf.Loc);
        Assert.Equal(Location.MonsterZone, hidden.Loc);

        // Two candidates tie: the activating player picks.
        CardInstance second = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Real("fissure")).Id));
        Scenario.ResolveChain(engine);
        PendingChoice tie = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Resolution, tie.Kind);
        Assert.Equal(new[] { elf.Id, second.Id }, tie.Choice.Options);
        Scenario.Answer(engine, second.Id);
        Assert.Equal(Location.Graveyard, second.Loc);
        Assert.Equal(Location.MonsterZone, elf.Loc);
    }

    [Fact]
    public void SmashingGroundDestroysTheHighestDefFaceUpMonster()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("smashing_ground") });
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance wall = Scenario.Place(engine, 1, Cards.Wall, Position.FaceUpAttack);

        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Real("smashing_ground")).Id));
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Graveyard, wall.Loc);
        Assert.Equal(Location.MonsterZone, elf.Loc);
    }

    [Fact]
    public void HeavyStormDestroysEverySpellAndTrapOnTheField()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("heavy_storm") });
        CardInstance ownTrap = Scenario.Set(engine, 0, Cards.AttackTrap);
        CardInstance theirTrap = Scenario.Set(engine, 1, Cards.AttackTrap);
        CardInstance theirSpell = Scenario.Set(engine, 1, Cards.PotOfGreed);
        CardInstance storm = Scenario.InHand(engine, 0, Real("heavy_storm"));

        Scenario.Submit(engine, new ActivateSpell(0, storm.Id));
        Scenario.ResolveChain(engine);

        Assert.All(new[] { ownTrap, theirTrap, theirSpell, storm }, c => Assert.Equal(Location.Graveyard, c.Loc));
        Assert.Equal(3, engine.Events.Count(e => e is SpellTrapDestroyed));
        Assert.Equal(0, engine.State.Players.Sum(p => p.SpellTrapCount));
    }

    [Fact]
    public void HeavyStormNeedsAnotherSpellOrTrapOnTheField()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("heavy_storm") });

        Assert.Equal("Heavy Storm cannot be activated now", engine.Validate(new ActivateSpell(0, Scenario.InHand(engine, 0, Real("heavy_storm")).Id)));
    }

    [Fact]
    public void MysticalSpaceTyphoonTargetsOneSpellOrTrap()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("mystical_space_typhoon") });
        CardInstance trap = Scenario.Set(engine, 1, Cards.AttackTrap);
        CardInstance own = Scenario.Set(engine, 0, Cards.PotOfGreed);
        CardInstance typhoon = Scenario.InHand(engine, 0, Real("mystical_space_typhoon"));

        Scenario.Submit(engine, new ActivateSpell(0, typhoon.Id));
        PendingChoice target = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Target, target.Kind);
        Assert.Equal(new[] { own.Id, trap.Id }, target.Choice.Options);
        Scenario.Answer(engine, trap.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Graveyard, trap.Loc);
        Assert.Equal(Location.SpellTrapZone, own.Loc);
        Assert.Equal(Location.Graveyard, typhoon.Loc);
    }

    [Fact]
    public void MysticalSpaceTyphoonChainsToAnEquipAndTheEquipFizzles()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("axe_of_despair") });
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance typhoon = Scenario.Set(engine, 1, Real("mystical_space_typhoon"));
        CardInstance axe = Scenario.InHand(engine, 0, Real("axe_of_despair"));

        Scenario.Submit(engine, new ActivateSpell(0, axe.Id));
        Scenario.Answer(engine, elf.Id);
        Assert.Equal(1, engine.State.Priority);
        Scenario.Submit(engine, new ActivateSpell(1, typhoon.Id));
        Scenario.Answer(engine, axe.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Graveyard, axe.Loc);
        Assert.Equal(Location.Graveyard, typhoon.Loc);
        Assert.Equal(1900, elf.Atk);
    }

    [Fact]
    public void LightningVortexDiscardsAsACostAndDestroysFaceUpMonsters()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("lightning_vortex"), Cards.GeminiElf });
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance hidden = Scenario.Place(engine, 1, Cards.Wall, Position.FaceDownDefense);
        CardInstance vortex = Scenario.InHand(engine, 0, Real("lightning_vortex"));
        CardInstance discard = Scenario.InHand(engine, 0, Cards.GeminiElf);

        Scenario.Submit(engine, new ActivateSpell(0, vortex.Id));
        PendingChoice cost = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Cost, cost.Kind);
        Assert.DoesNotContain(vortex.Id, cost.Choice.Options);
        Scenario.Answer(engine, discard.Id);
        Assert.Equal(Location.Graveyard, discard.Loc);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Graveyard, elf.Loc);
        Assert.Equal(Location.MonsterZone, hidden.Loc);
    }

    [Fact]
    public void DustTornadoDestroysAnOpponentsCardThenOffersASet()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.PotOfGreed }, new[] { Cards.AttackTrap });
        CardInstance tornado = Scenario.Set(engine, 1, Real("dust_tornado"));
        CardInstance pot = Scenario.InHand(engine, 0, Cards.PotOfGreed);
        CardInstance inHand = Scenario.InHand(engine, 1, Cards.AttackTrap);
        Scenario.Submit(engine, new SetSpellTrap(0, pot.Id));

        Scenario.Submit(engine, new Pass(0));
        Assert.Equal(1, engine.State.Priority);
        Scenario.Submit(engine, new ActivateTrap(1, tornado.Id));
        Scenario.Answer(engine, pot.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Graveyard, pot.Loc);
        PendingChoice set = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Resolution, set.Kind);
        Assert.Equal(1, set.Player);
        Assert.Equal((0, 1), (set.Choice.Min, set.Choice.Max));
        Assert.Equal(new[] { inHand.Id }, set.Choice.Options);
        Scenario.Answer(engine, inHand.Id);

        Assert.Equal(Location.SpellTrapZone, inHand.Loc);
        Assert.True(inHand.IsFaceDown && inHand.SetThisTurn);
        Assert.Equal(Location.Graveyard, tornado.Loc);
        Assert.Contains(engine.Events, e => e is SpellTrapSet s && s.Card == inHand.Id);
    }

    [Fact]
    public void MirrorForceDestroysEveryAttackPositionMonsterOfTheAttacker()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance weakling = Scenario.Place(engine, 0, Cards.Weakling, Position.FaceUpAttack);
        CardInstance wall = Scenario.Place(engine, 0, Cards.Wall, Position.FaceUpDefense);
        CardInstance mirror = Scenario.Set(engine, 1, Real("mirror_force"));

        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, elf, null);
        Assert.Equal(1, engine.State.Priority);
        Scenario.Submit(engine, new ActivateTrap(1, mirror.Id));
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Graveyard, elf.Loc);
        Assert.Equal(Location.Graveyard, weakling.Loc);
        Assert.Equal(Location.MonsterZone, wall.Loc);
        Assert.Equal(8000, engine.State.Player(1).LifePoints);
        Assert.Contains(engine.Events, e => e is AttackCancelled a && a.Attacker == elf.Id);
        Assert.Equal(BattleStep.Battle, engine.State.BattleStep);
    }

    [Fact]
    public void SakuretsuArmorDestroysTheAttacker()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance bystander = Scenario.Place(engine, 0, Cards.Weakling, Position.FaceUpAttack);
        CardInstance armor = Scenario.Set(engine, 1, Real("sakuretsu_armor"));

        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, elf, null);
        Scenario.Submit(engine, new ActivateTrap(1, armor.Id));
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Graveyard, elf.Loc);
        Assert.Equal(Location.MonsterZone, bystander.Loc);
        Assert.Equal(8000, engine.State.Player(1).LifePoints);
    }

    [Fact]
    public void WidespreadRuinDestroysTheStrongestAttackPositionMonsterNotTheAttacker()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance weakling = Scenario.Place(engine, 0, Cards.Weakling, Position.FaceUpAttack);
        CardInstance ruin = Scenario.Set(engine, 1, Real("widespread_ruin"));

        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, weakling, null);
        Scenario.Submit(engine, new ActivateTrap(1, ruin.Id));
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Graveyard, elf.Loc);
        Assert.Equal(Location.MonsterZone, weakling.Loc);
        // The attack itself goes ahead.
        Assert.Equal(8000 - 1500, engine.State.Player(1).LifePoints);
    }

    [Fact]
    public void TorrentialTributeAnswersASummonAndClearsTheField()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.GeminiElf });
        CardInstance mine = Scenario.Place(engine, 0, Cards.Weakling, Position.FaceUpAttack);
        CardInstance theirs = Scenario.Place(engine, 1, Cards.Wall, Position.FaceDownDefense);
        CardInstance torrential = Scenario.Set(engine, 1, Real("torrential_tribute"));
        CardInstance elf = Scenario.InHand(engine, 0, Cards.GeminiElf);

        Scenario.Submit(engine, new NormalSummon(0, elf.Id));
        Assert.Equal(Window.Summon, engine.State.Window);
        Assert.Equal(1, engine.State.Priority);
        Scenario.Submit(engine, new ActivateTrap(1, torrential.Id));
        Scenario.ResolveChain(engine);

        Assert.All(new[] { mine, theirs, elf }, c => Assert.Equal(Location.Graveyard, c.Loc));
        Assert.Equal(Location.Graveyard, torrential.Loc);
    }

    [Fact]
    public void TrapHoleAnswersNormalAndFlipSummonsOfBigMonstersOnly()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.GeminiElf, Real("morphing_jar"), Cards.Reborn }, configure: _noHandLimit);
        CardInstance hole = Scenario.Set(engine, 1, Real("trap_hole"));
        CardInstance elf = Scenario.InHand(engine, 0, Cards.GeminiElf);

        Scenario.Submit(engine, new NormalSummon(0, elf.Id));
        Assert.Equal(SummonKind.Normal, engine.State.LastSummon);
        Assert.Equal(1, engine.State.Priority);
        Assert.Null(engine.Validate(new ActivateTrap(1, hole.Id)));
        Scenario.Submit(engine, new ActivateTrap(1, hole.Id));
        Scenario.ResolveChain(engine);
        Assert.Equal(Location.Graveyard, elf.Loc);
        Assert.Equal(Location.Graveyard, hole.Loc);

        // Under 1000 ATK: nobody can respond, the summon window closes by itself.
        CardInstance second = Scenario.Set(engine, 1, Real("trap_hole"));
        Scenario.PassUntil(engine, s => s.Window == Window.Open && s.Phase == Phase.Main1);
        Scenario.PassUntil(engine, s => s.TurnNumber == 4 && s.Phase == Phase.Main1);
        Scenario.Submit(engine, new NormalSummon(0, Scenario.InHand(engine, 0, Real("morphing_jar")).Id));
        // Player 0 holds a live Quick-Play Spell, so they pass by hand; player 1 has no response and the window closes.
        Scenario.Submit(engine, new Pass(0));
        Assert.Equal(Window.Open, engine.State.Window);
        Assert.Equal(Location.SpellTrapZone, second.Loc);

        // A Special Summon is not answered either.
        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Cards.Reborn).Id));
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(Location.MonsterZone, elf.Loc);
        Assert.Equal(SummonKind.Special, engine.State.LastSummon);
        Assert.Equal(Window.Open, engine.State.Window);
        Assert.Equal(Location.SpellTrapZone, second.Loc);
    }

    [Fact]
    public void ZaborgDestroysATargetWhenTributeSummoned()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("zaborg_the_thunder_monarch") });
        CardInstance tribute = Scenario.Place(engine, 0, Cards.Filler, Position.FaceUpAttack);
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance zaborg = Scenario.InHand(engine, 0, Real("zaborg_the_thunder_monarch"));

        Scenario.Submit(engine, new NormalSummon(0, zaborg.Id, new[] { tribute.Id }));

        Assert.Equal(SummonKind.Tribute, engine.State.LastSummon);
        PendingChoice target = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Target, target.Kind);
        Assert.Equal(zaborg.Id, target.Source);
        Assert.Equal(new[] { zaborg.Id, elf.Id }, target.Choice.Options);
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Graveyard, elf.Loc);
        Assert.Equal(Location.MonsterZone, zaborg.Loc);
        Assert.Contains(engine.Events, e => e is ChainLinkAdded { Link: 1, Player: 0, EffectId: ZaborgEffect.EffectId });
        Assert.Contains(engine.Events, e => e is EffectActivated { EffectId: ZaborgEffect.EffectId });
    }

    [Fact]
    public void MonarchsDoNotFireOnASpecialSummon()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Reborn });
        CardInstance zaborg = Scenario.InGraveyard(engine, 0, Real("zaborg_the_thunder_monarch"));
        Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);

        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Cards.Reborn).Id));
        Scenario.Answer(engine, zaborg.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.MonsterZone, zaborg.Loc);
        Assert.Null(engine.State.PendingChoice);
        Assert.DoesNotContain(engine.Events, e => e is EffectActivated { EffectId: ZaborgEffect.EffectId });
    }

    [Fact]
    public void MobiusOffersToDestroyUpToTwoSpellsOrTraps()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("mobius_the_frost_monarch") });
        CardInstance tribute = Scenario.Place(engine, 0, Cards.Filler, Position.FaceUpAttack);
        CardInstance first = Scenario.Set(engine, 1, Cards.AttackTrap);
        CardInstance second = Scenario.Set(engine, 1, Cards.PotOfGreed);
        CardInstance third = Scenario.Set(engine, 1, Cards.AttackTrap);
        CardInstance mobius = Scenario.InHand(engine, 0, Real("mobius_the_frost_monarch"));

        Scenario.Submit(engine, new NormalSummon(0, mobius.Id, new[] { tribute.Id }));

        PendingChoice ask = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.OptionalTrigger, ask.Kind);
        Scenario.Answer(engine, mobius.Id);
        PendingChoice targets = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Target, targets.Kind);
        Assert.Equal((1, 2), (targets.Choice.Min, targets.Choice.Max));
        Scenario.Answer(engine, first.Id, third.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Graveyard, first.Loc);
        Assert.Equal(Location.Graveyard, third.Loc);
        Assert.Equal(Location.SpellTrapZone, second.Loc);
    }

    [Fact]
    public void ExiledForceTributesItselfToDestroyAMonster()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance exiled = Scenario.Place(engine, 0, Real("exiled_force"), Position.FaceUpAttack);
        Assert.Equal("Exiled Force cannot be activated now", engine.Validate(new ActivateEffect(0, exiled.Id, 0)));

        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceDownDefense);
        Scenario.Submit(engine, new ActivateEffect(0, exiled.Id, 0));

        // The tribute is paid before the target is asked.
        Assert.Equal(Location.Graveyard, exiled.Loc);
        PendingChoice target = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Target, target.Kind);
        Assert.Equal(new[] { elf.Id }, target.Choice.Options);
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Graveyard, elf.Loc);
        Assert.Empty(engine.State.Chain);
    }

    // ----- Search and recover -----

    [Fact]
    public void SanganSearchesWhenItGoesFromTheFieldToTheGraveyard()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.SummonedSkull });
        CardInstance sangan = Scenario.Place(engine, 0, Real("sangan"), Position.FaceUpAttack);
        CardInstance skull = Scenario.InHand(engine, 0, Cards.SummonedSkull);
        CardInstance weakling = Scenario.InDeck(engine, 0, Cards.Weakling);
        CardInstance titan = Scenario.InDeck(engine, 0, Cards.Titan);
        var deckBefore = engine.State.Player(0).Deck.ToList();

        Scenario.Submit(engine, new NormalSummon(0, skull.Id, new[] { sangan.Id }));
        Assert.Equal(Location.Graveyard, sangan.Loc);
        Scenario.ResolveChain(engine);

        PendingChoice search = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Resolution, search.Kind);
        Assert.Equal(sangan.Id, search.Source);
        Assert.All(search.Choice.Options, id => Assert.True(engine.State.Find(id)!.Def.Monster!.Atk <= SanganEffect.MaximumAtk));
        Assert.DoesNotContain(titan.Id, search.Choice.Options);
        Scenario.Answer(engine, weakling.Id);

        Assert.Equal(Location.Hand, weakling.Loc);
        Assert.Contains(engine.Events, e => e is DeckShuffled { Player: 0 });
        Assert.NotEqual(deckBefore.Where(c => c != weakling).Select(c => c.Id), engine.State.Player(0).Deck.Select(c => c.Id));
        // Nobody could respond to the summon, so the window closed by itself once the search was done.
        Assert.Equal(Window.Open, engine.State.Window);
        Assert.Equal(Location.MonsterZone, skull.Loc);
    }

    [Fact]
    public void SanganDiscardedFromTheHandDoesNotSearch()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Duo }, new[] { Real("sangan") });
        CardInstance sangan = Scenario.InHand(engine, 1, Real("sangan"));
        engine.State.Player(1).Hand.RemoveAll(c => c != sangan);

        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Cards.Duo).Id));
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Graveyard, sangan.Loc);
        Assert.Null(engine.State.PendingChoice);
        Assert.DoesNotContain(engine.Events, e => e is EffectActivated { EffectId: SanganEffect.EffectId });
    }

    [Fact]
    public void ReinforcementOfTheArmySearchesALowLevelWarrior()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("reinforcement_of_the_army") });
        CardInstance rota = Scenario.InHand(engine, 0, Real("reinforcement_of_the_army"));
        CardInstance celtic = Scenario.InDeck(engine, 0, Real("celtic_guardian"));
        CardInstance squire = Scenario.InDeck(engine, 0, Cards.Squire);
        Scenario.InDeck(engine, 0, Cards.Titan);

        Scenario.Submit(engine, new ActivateSpell(0, rota.Id));
        Scenario.ResolveChain(engine);

        PendingChoice search = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Resolution, search.Kind);
        Assert.Equal(new[] { squire.Id, celtic.Id }, search.Choice.Options.OrderBy(id => id == celtic.Id));
        Scenario.Answer(engine, celtic.Id);

        Assert.Equal(Location.Hand, celtic.Loc);
        Assert.Equal(Location.Graveyard, rota.Loc);
        Assert.Contains(engine.Events, e => e is DeckShuffled { Player: 0 });
    }

    [Fact]
    public void ReinforcementOfTheArmyNeedsAWarriorInTheDeck()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("reinforcement_of_the_army") });

        Assert.Equal("Reinforcement of the Army cannot be activated now", engine.Validate(new ActivateSpell(0, Scenario.InHand(engine, 0, Real("reinforcement_of_the_army")).Id)));
    }

    [Fact]
    public void TheWarriorReturningAliveTargetsAWarriorInTheGraveyard()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("the_warrior_returning_alive") });
        CardInstance celtic = Scenario.InGraveyard(engine, 0, Real("celtic_guardian"));
        Scenario.InGraveyard(engine, 0, Cards.GeminiElf);

        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Real("the_warrior_returning_alive")).Id));
        PendingChoice target = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(new[] { celtic.Id }, target.Choice.Options);
        Scenario.Answer(engine, celtic.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Hand, celtic.Loc);
    }

    [Fact]
    public void GravekeepersSpySummonsAGravekeeperFromTheDeck()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance spy = Scenario.Place(engine, 0, Real("gravekeepers_spy"), Position.FaceDownDefense);
        CardInstance guard = Scenario.InDeck(engine, 0, Real("gravekeepers_guard"));

        Scenario.Submit(engine, new FlipSummon(0, spy.Id));
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.MonsterZone, guard.Loc);
        Assert.Equal(Position.FaceUpDefense, guard.Pos);
        Assert.Contains(engine.Events, e => e is MonsterSpecialSummoned s && s.Card == guard.Id && s.From == Location.Deck);
        Assert.Contains(engine.Events, e => e is DeckShuffled { Player: 0 });
        // The Guard's own flip effect does not fire: it arrived face-up.
        Assert.DoesNotContain(engine.Events, e => e is EffectActivated { EffectId: GravekeepersGuardEffect.EffectId });
    }

    [Fact]
    public void MagicianOfFaithRecoversASpellFromTheGraveyard()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance magician = Scenario.Place(engine, 0, Real("magician_of_faith"), Position.FaceDownDefense);
        CardInstance pot = Scenario.InGraveyard(engine, 0, Cards.PotOfGreed);
        Scenario.InGraveyard(engine, 0, Cards.AttackTrap);

        Scenario.Submit(engine, new FlipSummon(0, magician.Id));
        PendingChoice target = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Target, target.Kind);
        Assert.Equal(new[] { pot.Id }, target.Choice.Options);
        Scenario.Answer(engine, pot.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Hand, pot.Loc);
    }

    [Fact]
    public void GravekeepersGuardBouncesAnOpponentsMonsterWhenFlippedByBattle()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance weakling = Scenario.Place(engine, 0, Cards.Weakling, Position.FaceUpAttack);
        CardInstance guard = Scenario.Place(engine, 1, Real("gravekeepers_guard"), Position.FaceDownDefense);

        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, weakling, guard);

        // The Weakling bounced off the 1900 DEF, then the flip effect targets it.
        Assert.Equal(8000 - 400, engine.State.Player(0).LifePoints);
        PendingChoice target = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(1, target.Player);
        Assert.Equal(new[] { weakling.Id }, target.Choice.Options);
        Scenario.Answer(engine, weakling.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Hand, weakling.Loc);
        Assert.Equal(Position.FaceUpDefense, guard.Pos);
    }

    // ----- Stats and position -----

    [Fact]
    public void AxeOfDespairBoostsAndRecyclesItselfForATribute()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("axe_of_despair") });
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance typhoon = Scenario.Set(engine, 1, Real("mystical_space_typhoon"));
        CardInstance axe = Scenario.InHand(engine, 0, Real("axe_of_despair"));

        Scenario.Submit(engine, new ActivateSpell(0, axe.Id));
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(1900 + AxeOfDespairEffect.Amount, elf.Atk);
        Assert.Equal(elf.Id, axe.EquippedTo);

        // The opponent blows the Axe up: its owner may tribute a monster to put it back on top of the Deck.
        Scenario.Submit(engine, new Pass(0));
        Scenario.Submit(engine, new ActivateSpell(1, typhoon.Id));
        Scenario.Answer(engine, axe.Id);
        Scenario.ResolveChain(engine);
        Assert.Equal(Location.Graveyard, axe.Loc);
        Assert.Equal(1900, elf.Atk);

        PendingChoice ask = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.OptionalTrigger, ask.Kind);
        Assert.Equal(0, ask.Player);
        Scenario.Answer(engine, axe.Id);
        PendingChoice cost = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Cost, cost.Kind);
        Assert.Equal(new[] { elf.Id }, cost.Choice.Options);
        Scenario.Answer(engine, elf.Id);
        Assert.Equal(Location.Graveyard, elf.Loc);
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Deck, axe.Loc);
        Assert.Same(axe, engine.State.Player(0).Deck[^1]);
        Assert.Contains(engine.Events, e => e is CardReturnedToDeck r && r.Card == axe.Id && r.Top);
    }

    [Fact]
    public void AxeOfDespairDiscardedFromTheHandStaysInTheGraveyard()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Duo }, new[] { Real("axe_of_despair") });
        Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance axe = Scenario.InHand(engine, 1, Real("axe_of_despair"));
        engine.State.Player(1).Hand.RemoveAll(c => c != axe);

        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Cards.Duo).Id));
        Scenario.ResolveChain(engine);

        Assert.Equal(Location.Graveyard, axe.Loc);
        Assert.Null(engine.State.PendingChoice);
    }

    [Fact]
    public void BookOfMoonFlipsAMonsterFaceDown()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Real("book_of_moon") });
        CardInstance elf = Scenario.Place(engine, 1, Cards.GeminiElf, Position.FaceUpAttack);

        Scenario.Submit(engine, new ActivateSpell(0, Scenario.InHand(engine, 0, Real("book_of_moon")).Id));
        Scenario.Answer(engine, elf.Id);
        Scenario.ResolveChain(engine);

        Assert.Equal(Position.FaceDownDefense, elf.Pos);
        Assert.Equal(Location.MonsterZone, elf.Loc);
    }

    [Fact]
    public void BerserkGorillaMustAttackWhenItCan()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance gorilla = Scenario.Place(engine, 0, Real("berserk_gorilla"), Position.FaceUpAttack);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);

        Assert.True(gorilla.Has(Restriction.MustAttack));
        Assert.Equal("Berserk Gorilla must attack", engine.Validate(new Pass(0)));
        Assert.DoesNotContain(engine.LegalActions(0), a => a is Pass);
        Assert.Contains(engine.LegalActions(0), a => a is EnterBattlePhase);

        Scenario.EnterBattle(engine);
        Assert.Equal(BattleStep.Battle, engine.State.BattleStep);
        Assert.Equal("Berserk Gorilla must attack", engine.Validate(new Pass(0)));
        Assert.All(engine.LegalActions(0), a => Assert.IsType<DeclareAttack>(a));

        Scenario.Attack(engine, gorilla, weakling);
        Assert.Equal(Location.Graveyard, weakling.Loc);
        Assert.Null(engine.Validate(new Pass(0)));
    }

    [Fact]
    public void BerserkGorillaMayPassWhenItCannotAttack()
    {
        DuelEngine engine = Scenario.Start(0);
        Scenario.Place(engine, 0, Real("berserk_gorilla"), Position.FaceUpAttack);
        Scenario.PassUntil(engine, s => s.Phase == Phase.Main1);

        // Turn 1: no Battle Phase, so nothing forces the attack.
        Assert.Null(engine.Validate(new Pass(0)));
    }

    [Fact]
    public void BerserkGorillaIsDestroyedWhenSwitchedToDefense()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance gorilla = Scenario.Place(engine, 0, Real("berserk_gorilla"), Position.FaceUpAttack);

        Scenario.Submit(engine, new ChangePosition(0, gorilla.Id));

        Assert.Equal(Location.Graveyard, gorilla.Loc);
        Assert.Contains(engine.Events, e => e is MonsterDestroyed d && d.Card == gorilla.Id && d.Reason == DestroyReason.Effect);
    }

    [Theory]
    [InlineData("goblin_attack_force")]
    [InlineData("giant_orc")]
    public void GoblinAttackForceAndGiantOrcSwitchToDefenseAfterAttackingAndStayThere(string id)
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(configure: _noHandLimit);
        CardInstance goblin = Scenario.Place(engine, 0, Real(id), Position.FaceUpAttack);
        CardInstance bystander = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);

        Scenario.EnterBattle(engine);
        Scenario.Attack(engine, goblin, null);
        Assert.Equal(Position.FaceUpAttack, goblin.Pos);
        Scenario.Submit(engine, new Pass(0)); // Battle Step → End Step → Main Phase 2

        Assert.Equal(Phase.Main2, engine.State.Phase);
        Assert.Equal(Position.FaceUpDefense, goblin.Pos);
        Assert.Equal(Position.FaceUpAttack, bystander.Pos);
        Assert.True(goblin.Has(Restriction.CannotChangePosition));
        Modifier lockMod = Assert.Single(engine.State.Modifiers);
        Assert.Equal(engine.State.TurnNumber + 2, lockMod.ExpiresAfterTurn);

        Scenario.PassUntil(engine, s => s.TurnNumber == 4 && s.Phase == Phase.Main1);
        Assert.Equal($"{goblin.Def.Name} cannot change its battle position", engine.Validate(new ChangePosition(0, goblin.Id)));

        Scenario.PassUntil(engine, s => s.TurnNumber == 6 && s.Phase == Phase.Main1);
        Assert.Empty(engine.State.Modifiers);
        Assert.Null(engine.Validate(new ChangePosition(0, goblin.Id)));
    }

    // ----- Acceptance: the Beatdown list without its tier 3–4 cards is playable by the heuristic agents -----

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    [InlineData(4UL)]
    [InlineData(5UL)]
    public void HeuristicAgentsPlayTheTierTwoBeatdownListToAWinner(ulong seed)
    {
        Deck deck = BeatdownWithoutHigherTiers();
        DuelEngine engine = DuelEngine.Start(deck, deck, new DuelOptions { Seed = seed });

        int steps = DuelRunner.Play(engine, new HeuristicAgent(AiProfile.Nico, seed), new HeuristicAgent(AiProfile.Mara, seed + 100));

        Assert.True(engine.State.IsOver, $"duel still running after {steps} commands");
        Assert.NotNull(engine.State.Winner);
        Assert.InRange(engine.State.TurnNumber, 3, 80);
    }

    /// <summary>The Beatdown list from <c>data/decks/beatdown.json</c> with its tier 3 and 4 cards dropped, padded back to 40 with its own tier 1 monsters.</summary>
    private static Deck BeatdownWithoutHigherTiers()
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Cards.DecksDirectory, "beatdown.json")));
        var main = new List<CardDefinition>();
        foreach (JsonProperty entry in doc.RootElement.GetProperty("main").EnumerateObject())
        {
            CardDefinition card = Cards.Real(entry.Name);
            if (card.Tier <= 2)
            {
                main.AddRange(Enumerable.Repeat(card, entry.Value.GetInt32()));
            }
        }

        Assert.Equal(31, main.Count);
        var filler = main.Where(c => c.IsVanilla).Distinct().ToList();
        for (int i = 0; main.Count < Scenario.DeckSize; i++)
        {
            main.Add(filler[i % filler.Count]);
        }

        return new Deck(main);
    }
}
