using System;
using System.Linq;
using BattleCity.Duel.Core.Ai;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;
using Xunit;

namespace BattleCity.Duel.Core.Tests;

/// <summary>Response windows, chains, triggers and the choice protocol (issue #54, systems.md §5.4–§5.5).</summary>
public class ChainTests
{
    [Fact]
    public void SummonOpensAWindowTheOpponentMayRespondIn()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.GeminiElf, Cards.Boost });
        CardInstance elf = Scenario.InHand(engine, 0, Cards.GeminiElf);
        CardInstance boost = Scenario.InHand(engine, 0, Cards.Boost);
        CardInstance trap = Scenario.Set(engine, 1, Cards.SummonTrap);

        Scenario.Submit(engine, new NormalSummon(0, elf.Id));

        // The turn player holds priority in the window; with a Quick-Play in hand they are not passed for automatically.
        Assert.Equal(Window.Summon, engine.State.Window);
        Assert.Equal(elf.Id, engine.State.WindowCard);
        Assert.Equal(0, engine.State.Priority);
        Assert.Equal(new PlayerCommand[] { new Pass(0), new ActivateSpell(0, boost.Id) }, engine.LegalActions(0));
        Assert.Equal("not while a window is open for responses", engine.Validate(new EnterBattlePhase(0)));
        Assert.Empty(engine.LegalActions(1));

        Scenario.Submit(engine, new Pass(0));

        Assert.Equal(1, engine.State.Priority);
        Assert.Contains(engine.LegalActions(1), a => a is ActivateTrap t && t.Card == trap.Id);

        Scenario.Submit(engine, new ActivateTrap(1, trap.Id));

        // Player 0 could chain the Boost; passing lets the chain resolve, then both passes close the window.
        Assert.Single(engine.State.Chain);
        Assert.Equal(0, engine.State.Priority);
        Scenario.Submit(engine, new Pass(0));

        Assert.Equal(Location.Graveyard, elf.Loc);
        Assert.Equal(Location.Graveyard, trap.Loc);
        Assert.Equal(Window.Open, engine.State.Window);
        Assert.Equal(Phase.Main1, engine.State.Phase);
        Assert.Equal(0, engine.State.Priority);
        Assert.Contains(engine.LegalActions(0), a => a is EnterBattlePhase);
        Assert.Contains(engine.Events, e => e is TrapActivated { Player: 1 });
        Assert.Contains(engine.Events, e => e is MonsterDestroyed { Reason: DestroyReason.Effect } d && d.Card == elf.Id);
    }

    [Fact]
    public void ClosingTheSummonWindowWithoutResponsesReturnsToTheMainPhase()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.GeminiElf, Cards.PotOfGreed });
        CardInstance elf = Scenario.InHand(engine, 0, Cards.GeminiElf);

        Scenario.Submit(engine, new NormalSummon(0, elf.Id));

        // Neither player has a response: the automatic passes close the window instead of ending the phase.
        Assert.Equal(Window.Open, engine.State.Window);
        Assert.Equal(Phase.Main1, engine.State.Phase);
        Assert.Equal(0, engine.State.Priority);
        Assert.Contains(engine.LegalActions(0), a => a is ActivateSpell);
        Assert.Equal(
            new[] { new WindowChanged(Window.Summon, elf.Id), new WindowChanged(Window.Open, null) },
            engine.Events.OfType<WindowChanged>());
    }

    [Fact]
    public void IgnitionEffectsKeepPriorityAfterASummonAndCollectCostsAndTargets()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Destroyer });
        CardInstance destroyer = Scenario.InHand(engine, 0, Cards.Destroyer);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);
        Scenario.Set(engine, 1, Cards.SummonTrap);

        Scenario.Submit(engine, new NormalSummon(0, destroyer.Id));
        Assert.Contains(engine.LegalActions(0), a => a is ActivateEffect { EffectIndex: 0 } e && e.Card == destroyer.Id);

        Scenario.Submit(engine, new ActivateEffect(0, destroyer.Id, 0));

        PendingChoice cost = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Cost, cost.Kind);
        Assert.Equal(0, cost.Player);
        Assert.Equal(destroyer.Id, cost.Source);
        Assert.Equal(engine.State.Player(0).Hand.Count, cost.Choice.Options.Count);
        Assert.All(engine.LegalActions(0), a => Assert.IsType<AnswerChoice>(a));
        Assert.Empty(engine.LegalActions(1));
        Assert.Empty(engine.State.Chain);

        CardInstance discarded = engine.State.Player(0).Hand[0];
        Scenario.Submit(engine, new AnswerChoice(0, discarded.Id));

        Assert.Equal(Location.Graveyard, discarded.Loc);
        PendingChoice target = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.Target, target.Kind);
        Assert.Equal(new[] { weakling.Id }, target.Choice.Options);

        Scenario.Submit(engine, new AnswerChoice(0, weakling.Id));

        // The opponent's Summon Trap could not respond to the summon before the turn player used their ignition priority; it may chain now or respond after the chain.
        Assert.Single(engine.State.Chain);
        Assert.Equal(1, engine.State.Priority);
        Scenario.Submit(engine, new Pass(1));

        Assert.Equal(Location.Graveyard, weakling.Loc);
        Assert.Equal(Location.MonsterZone, destroyer.Loc);
        Assert.Empty(engine.State.Chain);
        Assert.Equal(Window.Summon, engine.State.Window);
        Assert.Contains(engine.LegalActions(1), a => a is ActivateTrap);
        Assert.Contains(engine.Events, e => e is ChainLinkAdded { Link: 1, Player: 0, EffectId: TestEffects.IgnitionDestroy.EffectId });
        Assert.Contains(engine.Events, e => e is ChoiceRequested { Kind: ChoiceKind.Cost, Player: 0 });
        Assert.Contains(engine.Events, e => e is ChoiceAnswered { Kind: ChoiceKind.Cost } a && a.Selected.SequenceEqual(new[] { discarded.Id }));
    }

    [Fact]
    public void AnswersAreValidated()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Destroyer });
        CardInstance destroyer = Scenario.InHand(engine, 0, Cards.Destroyer);
        Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);
        Scenario.Submit(engine, new NormalSummon(0, destroyer.Id));
        Scenario.Submit(engine, new ActivateEffect(0, destroyer.Id, 0));
        var hand = engine.State.Player(0).Hand;

        Assert.Equal("answer the pending choice first", engine.Validate(new Pass(0)));
        Assert.Equal("player 0 owes the answer", engine.Validate(new AnswerChoice(1, hand[0].Id)));
        Assert.Equal("select 1–1 option(s), 2 given", engine.Validate(new AnswerChoice(0, hand[0].Id, hand[1].Id)));
        Assert.Equal("select 1–1 option(s), 0 given", engine.Validate(new AnswerChoice(0)));
        Assert.Equal("an option is not among the choices", engine.Validate(new AnswerChoice(0, Guid.NewGuid())));
        Assert.Null(engine.Validate(new AnswerChoice(0, hand[0].Id)));
        Assert.Equal(hand.Count, engine.LegalActions(0).Count);
    }

    [Fact]
    public void OptionalTriggersAskTheirController()
    {
        DuelEngine accepted = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Drawer });
        CardInstance drawer = Scenario.InHand(accepted, 0, Cards.Drawer);
        Scenario.Submit(accepted, new NormalSummon(0, drawer.Id));

        PendingChoice choice = Assert.IsType<PendingChoice>(accepted.State.PendingChoice);
        Assert.Equal(ChoiceKind.OptionalTrigger, choice.Kind);
        Assert.Equal(drawer.Id, choice.Source);
        Assert.Equal(2, accepted.LegalActions(0).Count);
        Assert.Equal(5, accepted.State.Player(0).Hand.Count);

        Scenario.Submit(accepted, new AnswerChoice(0, drawer.Id));
        Assert.Equal(6, accepted.State.Player(0).Hand.Count);
        Assert.Contains(accepted.Events, e => e is EffectActivated { EffectId: TestEffects.OptionalSummonDraw.EffectId });
        Assert.Equal(Window.Open, accepted.State.Window);

        DuelEngine declined = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Drawer });
        Scenario.Submit(declined, new NormalSummon(0, Scenario.InHand(declined, 0, Cards.Drawer).Id));
        Scenario.Submit(declined, new AnswerChoice(0));
        Assert.Equal(5, declined.State.Player(0).Hand.Count);
        Assert.DoesNotContain(declined.Events, e => e is ChainLinkAdded);
        Assert.Equal(Window.Open, declined.State.Window);
    }

    [Fact]
    public void MandatoryTriggersGoFirstTurnPlayerFirstAndTheControllerOrdersTheirOwn()
    {
        DuelEngine engine = Scenario.Start(0);
        CardInstance a = Scenario.Place(engine, 0, Cards.StandbyDrawer, Position.FaceUpAttack);
        CardInstance b = Scenario.Place(engine, 0, Cards.StandbyDrawer, Position.FaceUpDefense);
        CardInstance c = Scenario.Place(engine, 1, Cards.StandbyDrawer, Position.FaceUpAttack);
        int hand0 = engine.State.Player(0).Hand.Count;

        Scenario.Submit(engine, new Pass(0)); // turn 2: player 1 draws, Standby Phase triggers fire

        Assert.Equal(Phase.Standby, engine.State.Phase);
        PendingChoice order = Assert.IsType<PendingChoice>(engine.State.PendingChoice);
        Assert.Equal(ChoiceKind.TriggerOrder, order.Kind);
        Assert.Equal(0, order.Player);
        Assert.Equal(new[] { a.Id, b.Id }, order.Choice.Options);
        Assert.Equal(new[] { c.Id }, engine.State.Chain.Select(l => l.Source.Id));

        Scenario.Submit(engine, new AnswerChoice(0, b.Id));

        Assert.Equal(Phase.Main1, engine.State.Phase);
        Assert.Equal(
            new[] { (1, c.Id, 1), (2, b.Id, 0), (3, a.Id, 0) },
            engine.Events.OfType<ChainLinkAdded>().Select(e => (e.Link, e.Card, e.Player)));
        Assert.Equal(new[] { 3, 2, 1 }, engine.Events.OfType<ChainLinkResolved>().Select(e => e.Link));
        Assert.Equal(hand0 + 2, engine.State.Player(0).Hand.Count);
        Assert.Equal(7, engine.State.Player(1).Hand.Count);
    }

    [Fact]
    public void SpellSpeedRuleAndLifoResolution()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);
        CardInstance negate = Scenario.Set(engine, 0, Cards.NegateTrap);
        CardInstance attackTrap = Scenario.Set(engine, 1, Cards.AttackTrap);
        CardInstance secondAttackTrap = Scenario.Set(engine, 1, Cards.AttackTrap);
        CardInstance opponentNegate = Scenario.Set(engine, 1, Cards.NegateTrap);
        Scenario.EnterBattle(engine);
        Assert.Equal("Negate Trap cannot be activated now", engine.Validate(new ActivateTrap(0, negate.Id)));

        Scenario.Submit(engine, new DeclareAttack(0, elf.Id, weakling.Id));

        // The attacker had nothing to respond with and was passed for; the opponent holds the declaration window.
        Assert.Equal(Window.AttackDeclared, engine.State.Window);
        Assert.Equal(1, engine.State.Priority);

        Scenario.Submit(engine, new ActivateTrap(1, attackTrap.Id));

        Assert.Single(engine.State.Chain);
        Assert.Equal(0, engine.State.Priority);
        Scenario.Submit(engine, new ActivateTrap(0, negate.Id));

        Assert.Equal(2, engine.State.Chain.Count);
        Assert.Equal(1, engine.State.Priority);
        Assert.Equal("spell speed too low to chain", engine.Validate(new ActivateTrap(1, secondAttackTrap.Id)));
        Assert.Equal(new PlayerCommand[] { new Pass(1), new ActivateTrap(1, opponentNegate.Id) }, engine.LegalActions(1));

        Scenario.Submit(engine, new Pass(1));

        // Both passed on the chain: link 2 (the Counter Trap) resolves first and negates link 1.
        Assert.Contains(engine.Events, e => e is ChainLinkNegated { Link: 1 });
        Assert.Equal(new[] { 2, 1 }, engine.Events.OfType<ChainLinkResolved>().Select(e => e.Link));
        Assert.Equal(Location.MonsterZone, elf.Loc);
        Assert.Equal(Window.AttackDeclared, engine.State.Window);

        // The declaration window is still open and the second Attack Trap is live; declining it runs the Damage Step.
        Assert.Equal(1, engine.State.Priority);
        Scenario.Submit(engine, new Pass(1));

        Assert.Equal(Location.Graveyard, weakling.Loc);
        Assert.Equal(Location.Graveyard, negate.Loc);
        Assert.Equal(Location.Graveyard, attackTrap.Loc);
        Assert.Equal(7600, engine.State.Player(1).LifePoints);
        Assert.Equal(Window.Open, engine.State.Window);
        Assert.Equal(BattleStep.Battle, engine.State.BattleStep);
    }

    [Fact]
    public void SetCardsWaitATurnAndQuickPlaysFromTheHandNeedYourTurn()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.AttackTrap, Cards.Boost, Cards.Boost });
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance trap = Scenario.InHand(engine, 0, Cards.AttackTrap);
        CardInstance setBoost = Scenario.InHand(engine, 0, Cards.Boost);
        Scenario.Submit(engine, new SetSpellTrap(0, trap.Id));
        Scenario.Submit(engine, new SetSpellTrap(0, setBoost.Id));
        CardInstance handBoost = Scenario.InHand(engine, 0, Cards.Boost);

        Assert.Equal("Attack Trap cannot be activated on the turn it was Set", engine.Validate(new ActivateTrap(0, trap.Id)));
        Assert.Equal("Boost cannot be activated on the turn it was Set", engine.Validate(new ActivateSpell(0, setBoost.Id)));
        Assert.Null(engine.Validate(new ActivateSpell(0, handBoost.Id)));

        Scenario.Submit(engine, new Pass(0)); // End Phase: the Boost in hand is live, so player 0 is not passed for
        Assert.Equal(Phase.End, engine.State.Phase);
        Scenario.Submit(engine, new Pass(0)); // turn 3: player 1's Draw Phase, where player 0 may already respond

        Assert.Equal(1, engine.State.TurnPlayer);
        Assert.Equal(Phase.Draw, engine.State.Phase);
        Assert.Equal(0, engine.State.Priority);
        Assert.Null(engine.Validate(new ActivateSpell(0, setBoost.Id)));
        Assert.Equal("Quick-Play Spells are activated from the hand only on your turn", engine.Validate(new ActivateSpell(0, handBoost.Id)));
        Assert.Equal("Attack Trap cannot be activated now", engine.Validate(new ActivateTrap(0, trap.Id)));

        Scenario.Submit(engine, new ActivateSpell(0, setBoost.Id));
        Scenario.Submit(engine, new AnswerChoice(0, elf.Id));

        Assert.Equal(1900 + TestEffects.BoostSpell.Amount, elf.Atk);
        Assert.Equal(Location.Graveyard, setBoost.Loc);
    }

    [Fact]
    public void BeforeDamageCalculationOnlyCounterTrapsAndStatModifiersActivate()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);
        CardInstance boost = Scenario.Set(engine, 0, Cards.Boost);
        CardInstance attackTrap = Scenario.Set(engine, 1, Cards.AttackTrap);
        CardInstance negate = Scenario.Set(engine, 1, Cards.NegateTrap);
        Scenario.EnterBattle(engine);
        Scenario.Submit(engine, new Pass(0)); // Start Step: the Set Boost keeps player 0 from being passed for
        Scenario.Submit(engine, new DeclareAttack(0, elf.Id, weakling.Id));
        Scenario.Submit(engine, new Pass(0));
        Scenario.Submit(engine, new Pass(1));

        Assert.Equal(Window.DamageBeforeCalc, engine.State.Window);
        Assert.Equal(DamageSubstep.BeforeCalc, engine.State.DamageSubstep);
        Assert.Equal(0, engine.State.Priority);
        Assert.Null(engine.Validate(new ActivateSpell(0, boost.Id)));

        Scenario.Submit(engine, new ActivateSpell(0, boost.Id));
        Scenario.Submit(engine, new AnswerChoice(0, elf.Id));

        Assert.Equal(1, engine.State.Priority);
        Assert.Equal("only Counter Traps and ATK/DEF modifiers before damage calculation", engine.Validate(new ActivateTrap(1, attackTrap.Id)));
        Assert.Contains(engine.LegalActions(1), a => a is ActivateTrap t && t.Card == negate.Id);

        Scenario.Submit(engine, new Pass(1));

        Assert.Equal(1900 + TestEffects.BoostSpell.Amount, elf.Atk);
        Assert.Equal(Location.Graveyard, weakling.Loc);
        Assert.Equal(8000 - (1900 + TestEffects.BoostSpell.Amount - 1500), engine.State.Player(1).LifePoints);
        Assert.Equal(Window.Open, engine.State.Window);
        Assert.Equal(
            new[] { Window.AttackDeclared, Window.DamageBeforeCalc, Window.DamageAfterCalc, Window.Open },
            engine.Events.OfType<WindowChanged>().Select(e => e.Window));
    }

    [Fact]
    public void BattleDestructionAndFlipTriggersFireAfterDamageCalculation()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance avenger = Scenario.Place(engine, 1, Cards.Avenger, Position.FaceUpAttack);
        CardInstance flipper = Scenario.Place(engine, 1, Cards.Flipper, Position.FaceDownDefense);
        CardInstance soldier = Scenario.Place(engine, 0, Cards.ArchfiendSoldier, Position.FaceUpAttack);
        int hand1 = engine.State.Player(1).Hand.Count;
        Scenario.EnterBattle(engine);

        Scenario.Attack(engine, elf, avenger);

        Assert.Equal(Location.Graveyard, avenger.Loc);
        Assert.Equal(hand1 + 1, engine.State.Player(1).Hand.Count);
        Assert.Contains(engine.Events, e => e is EffectActivated { EffectId: TestEffects.BattleDestroyedDraw.EffectId });

        Scenario.Attack(engine, soldier, flipper);

        Assert.Equal(Location.Graveyard, flipper.Loc);
        Assert.Equal(hand1 + 2, engine.State.Player(1).Hand.Count);
        var order = engine.Events.SkipWhile(e => e is not AttackDeclared { Target: { } t } || t != flipper.Id)
            .Where(e => e is DamageSubstepChanged or MonsterFlipped or ChainLinkAdded).ToList();
        Assert.Equal(
            new DuelEvent[]
            {
                new DamageSubstepChanged(DamageSubstep.StartDamage),
                new DamageSubstepChanged(DamageSubstep.BeforeCalc),
                new MonsterFlipped(1, flipper.Id, "test_flipper"),
                new DamageSubstepChanged(DamageSubstep.Calc),
                new DamageSubstepChanged(DamageSubstep.AfterCalc),
                new ChainLinkAdded(1, 1, flipper.Id, TestEffects.FlipDraw.EffectId),
                new DamageSubstepChanged(DamageSubstep.EndDamage),
            },
            order);
    }

    [Fact]
    public void NothingActivatesOnAnEmptyChainAfterDamageCalculation()
    {
        // Automatic passes off: the empty-chain window after damage calculation is otherwise skipped for both players.
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(configure: o => o with { AutoPass = false });
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance avenger = Scenario.Place(engine, 1, Cards.Avenger, Position.FaceUpAttack);
        CardInstance negate = Scenario.Set(engine, 0, Cards.NegateTrap);
        CardInstance boost = Scenario.Set(engine, 1, Cards.Boost);
        Scenario.EnterBattle(engine);
        Scenario.PassUntil(engine, s => s.BattleStep == BattleStep.Battle);
        Scenario.Submit(engine, new DeclareAttack(0, elf.Id, avenger.Id));
        Scenario.PassUntil(engine, s => s.Window == Window.DamageAfterCalc);

        Assert.Single(engine.State.Chain);
        Assert.Equal(0, engine.State.Priority);
        Assert.Contains(engine.LegalActions(0), a => a is ActivateTrap t && t.Card == negate.Id);

        Scenario.Submit(engine, new ActivateTrap(0, negate.Id));
        Assert.Equal("spell speed too low to chain", engine.Validate(new ActivateSpell(1, boost.Id)));
        Scenario.Submit(engine, new Pass(1));
        Scenario.Submit(engine, new Pass(0));

        Assert.Empty(engine.State.Chain);
        Assert.Contains(engine.Events, e => e is ChainLinkNegated { EffectId: TestEffects.BattleDestroyedDraw.EffectId });
        Assert.Equal(Window.DamageAfterCalc, engine.State.Window);
        Assert.Equal(0, engine.State.Priority);
        Scenario.Submit(engine, new Pass(0));

        Assert.Equal(1, engine.State.Priority);
        Assert.Equal("after damage calculation only Trigger effects activate", engine.Validate(new ActivateSpell(1, boost.Id)));
        Assert.Equal(new PlayerCommand[] { new Pass(1) }, engine.LegalActions(1));

        Scenario.Submit(engine, new Pass(1));

        Assert.Equal(Window.Open, engine.State.Window);
        Assert.Equal(BattleStep.Battle, engine.State.BattleStep);
        Assert.Equal(Location.Graveyard, avenger.Loc);
    }

    [Fact]
    public void AnAttackIsCancelledWhenTheAttackerLeavesTheFieldBeforeTheDamageStep()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance trap = Scenario.Set(engine, 1, Cards.AttackTrap);
        Scenario.EnterBattle(engine);
        Scenario.Submit(engine, new DeclareAttack(0, elf.Id, null));

        Scenario.Submit(engine, new ActivateTrap(1, trap.Id));

        Assert.Equal(Location.Graveyard, elf.Loc);
        Assert.Contains(engine.Events, e => e is AttackCancelled a && a.Attacker == elf.Id);
        Assert.DoesNotContain(engine.Events, e => e is DamageSubstepChanged);
        Assert.Equal(8000, engine.State.Player(1).LifePoints);
        Assert.Null(engine.State.Attacker);
        Assert.Equal(Window.Open, engine.State.Window);
        Assert.Equal(BattleStep.Battle, engine.State.BattleStep);
        Assert.Equal(0, engine.State.Priority);
    }

    [Fact]
    public void GraveyardTriggersKnowWhereTheCardCameFrom()
    {
        DuelEngine tributed = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.SummonedSkull });
        CardInstance searcher = Scenario.Place(tributed, 0, Cards.Searcher, Position.FaceUpAttack);
        CardInstance skull = Scenario.InHand(tributed, 0, Cards.SummonedSkull);
        int hand = tributed.State.Player(0).Hand.Count;

        Scenario.Submit(tributed, new NormalSummon(0, skull.Id, new[] { searcher.Id }));

        Assert.Equal(hand, tributed.State.Player(0).Hand.Count); // −1 for the summon, +1 for the trigger
        Assert.Contains(tributed.Events, e => e is EffectActivated { EffectId: TestEffects.FieldToGraveDraw.EffectId });

        DuelEngine discarded = Scenario.Start(0, new[] { Cards.Searcher, Cards.PotOfGreed });
        Scenario.Submit(discarded, new ActivateSpell(0, Scenario.InHand(discarded, 0, Cards.PotOfGreed).Id));
        Scenario.Submit(discarded, new Pass(0));
        Scenario.Submit(discarded, new Discard(0, Scenario.InHand(discarded, 0, Cards.Searcher).Id));

        Assert.Equal(2, discarded.State.TurnNumber);
        Assert.DoesNotContain(discarded.Events, e => e is EffectActivated);
    }

    [Fact]
    public void ANegatedSpellStillGoesToTheGraveyard()
    {
        DuelEngine engine = Scenario.Start(0, new[] { Cards.PotOfGreed });
        CardInstance pot = Scenario.InHand(engine, 0, Cards.PotOfGreed);
        CardInstance negate = Scenario.Set(engine, 1, Cards.NegateTrap);

        Scenario.Submit(engine, new ActivateSpell(0, pot.Id));

        Assert.Single(engine.State.Chain);
        Assert.Equal(1, engine.State.Priority);
        Assert.Contains(engine.LegalActions(1), a => a is ActivateTrap);

        Scenario.Submit(engine, new ActivateTrap(1, negate.Id));

        Assert.Empty(engine.State.Chain);
        Assert.Equal(5, engine.State.Player(0).Hand.Count);
        Assert.Equal(Location.Graveyard, pot.Loc);
        Assert.Equal(Location.Graveyard, negate.Loc);
        Assert.Equal(0, engine.State.Priority);
        Assert.Equal(Phase.Main1, engine.State.Phase);
    }

    [Fact]
    public void ACounterTrapMayNotAnswerAnEmptyChainOrItsOwnLink()
    {
        DuelEngine engine = Scenario.Start(0, new[] { Cards.PotOfGreed });
        CardInstance pot = Scenario.InHand(engine, 0, Cards.PotOfGreed);
        CardInstance ownNegate = Scenario.Set(engine, 0, Cards.NegateTrap);

        Assert.Equal("Negate Trap cannot be activated now", engine.Validate(new ActivateTrap(0, ownNegate.Id)));

        Scenario.Submit(engine, new ActivateSpell(0, pot.Id));

        // Nobody could respond, so the automatic passes resolved the chain.
        Assert.Equal(7, engine.State.Player(0).Hand.Count);
        Assert.Equal(Location.SpellTrapZone, ownNegate.Loc);
    }

    [Fact]
    public void CloneCopiesThePendingChoice()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.Destroyer });
        CardInstance destroyer = Scenario.InHand(engine, 0, Cards.Destroyer);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);
        Scenario.Submit(engine, new NormalSummon(0, destroyer.Id));
        Scenario.Submit(engine, new ActivateEffect(0, destroyer.Id, 0));
        Guid discard = engine.State.Player(0).Hand[0].Id;

        DuelEngine clone = engine.Clone();
        Scenario.Submit(clone, new AnswerChoice(0, discard));
        Scenario.Submit(clone, new AnswerChoice(0, weakling.Id));

        Assert.Equal(ChoiceKind.Cost, engine.State.PendingChoice!.Kind);
        Assert.Equal(Location.Hand, engine.State.Find(discard)!.Loc);
        Assert.Equal(Location.MonsterZone, weakling.Loc);
        Assert.Null(clone.State.PendingChoice);
        Assert.Equal(Location.Graveyard, clone.State.Find(weakling.Id)!.Loc);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(3UL)]
    public void HeuristicAgentsPlayADuelWithResponsesToTheEnd(ulong seed)
    {
        CardDefinition[] pool =
        {
            Cards.GeminiElf, Cards.Weakling, Cards.Wall, Cards.PotOfGreed, Cards.AttackTrap, Cards.SummonTrap,
            Cards.NegateTrap, Cards.Boost, Cards.Drawer, Cards.Avenger, Cards.Flipper, Cards.Destroyer, Cards.Searcher,
        };
        var deck = new Deck(Enumerable.Range(0, Scenario.DeckSize).Select(i => pool[i % pool.Length]).ToList());
        DuelEngine engine = DuelEngine.Start(deck, deck, new DuelOptions { Seed = seed }, TestEffects.Registry());

        int steps = DuelRunner.Play(engine, new HeuristicAgent(AiProfile.Nico, seed), new HeuristicAgent(AiProfile.ArcadeOwner, seed + 100));

        Assert.True(engine.State.IsOver, $"duel still running after {steps} commands");
        Assert.Contains(engine.Events, e => e is TrapActivated);
    }
}
