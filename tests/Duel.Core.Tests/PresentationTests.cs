using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Ai;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;
using BattleCity.Duel.Core.Presentation;
using Xunit;

namespace BattleCity.Duel.Core.Tests;

/// <summary>The HUD helpers of issue #61: menu entries, response and advance commands, log text.</summary>
public class PresentationTests
{
    [Fact]
    public void HandMonsterOffersSummonAndSet()
    {
        DuelEngine engine = Scenario.Start(0, new[] { Cards.Weakling, Cards.GeminiElf });
        CardInstance elf = Scenario.InHand(engine, 0, Cards.GeminiElf);

        IReadOnlyList<CardAction> actions = ActionCatalog.ForCard(engine, engine.LegalActions(0), elf.Id);

        Assert.Equal(new[] { ActionKind.Summon, ActionKind.Set }, actions.Select(a => a.Kind));
        Assert.All(actions, a => Assert.NotNull(a.Single));
        Assert.Equal(elf.Id, ActionCatalog.CardOf(actions[0].Single!));
    }

    [Fact]
    public void TributeSummonGroupsEveryTributeSet()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo(new[] { Cards.SummonedSkull });
        CardInstance a = Scenario.Place(engine, 0, Cards.Weakling, Position.FaceUpAttack);
        CardInstance b = Scenario.Place(engine, 0, Cards.Filler, Position.FaceUpAttack);
        CardInstance skull = Scenario.InHand(engine, 0, Cards.SummonedSkull);

        CardAction summon = ActionCatalog.ForCard(engine, engine.LegalActions(0), skull.Id).Single(x => x.Kind == ActionKind.Summon);

        Assert.True(summon.NeedsTributes);
        Assert.Null(summon.Single);
        Assert.Equal(1, summon.TributesRequired);
        Assert.Equal(new[] { a.Id, b.Id }, summon.TributeOptions);
        var command = Assert.IsType<NormalSummon>(summon.WithTributes(new[] { b.Id }));
        Assert.Equal(new[] { b.Id }, command.Tributes);
        Assert.Null(summon.WithTributes(new[] { a.Id, b.Id }));
        Assert.True(ActionCatalog.Same(command, new NormalSummon(0, skull.Id, new List<Guid> { b.Id })));
    }

    [Fact]
    public void SpellInHandOffersActivateAndSet()
    {
        DuelEngine engine = Scenario.Start(0, new[] { Cards.PotOfGreed });
        CardInstance pot = Scenario.InHand(engine, 0, Cards.PotOfGreed);

        IReadOnlyList<CardAction> actions = ActionCatalog.ForCard(engine, engine.LegalActions(0), pot.Id);

        Assert.Equal(new[] { ActionKind.Set, ActionKind.Activate }, actions.Select(a => a.Kind));
        Assert.IsType<ActivateSpell>(actions[1].Single);
    }

    [Fact]
    public void AttackEntryListsTheTargets()
    {
        DuelEngine engine = Scenario.AtPlayerZeroTurnTwo();
        CardInstance elf = Scenario.Place(engine, 0, Cards.GeminiElf, Position.FaceUpAttack);
        CardInstance weakling = Scenario.Place(engine, 1, Cards.Weakling, Position.FaceUpAttack);
        Assert.Equal("Battle Phase", ActionCatalog.AdvanceLabel(engine.State, engine.LegalActions(0)));
        Assert.IsType<EnterBattlePhase>(ActionCatalog.Advance(engine.LegalActions(0)));
        Scenario.EnterBattle(engine);

        CardAction attack = Assert.Single(ActionCatalog.ForCard(engine, engine.LegalActions(0), elf.Id));

        Assert.Equal(ActionKind.Attack, attack.Kind);
        Assert.Null(attack.Single);
        Assert.Equal(new Guid?[] { weakling.Id }, attack.AttackTargets);
        Assert.IsType<DeclareAttack>(attack.Targeting(weakling.Id));
        Assert.Null(attack.Targeting(null));
        Assert.Equal("Main Phase 2", ActionCatalog.AdvanceLabel(engine.State, engine.LegalActions(0)));
    }

    [Fact]
    public void FirstTurnAdvanceEndsTheTurn()
    {
        DuelEngine engine = Scenario.Start(0);

        Assert.IsType<Pass>(ActionCatalog.Advance(engine.LegalActions(0)));
        Assert.Equal("End turn", ActionCatalog.AdvanceLabel(engine.State, engine.LegalActions(0)));
        Assert.False(ActionCatalog.IsResponding(engine.State, 0));
    }

    [Fact]
    public void SummonWindowIsAResponse()
    {
        DuelEngine engine = Scenario.Start(0, new[] { Cards.GeminiElf });
        CardInstance elf = Scenario.InHand(engine, 0, Cards.GeminiElf);
        CardInstance trap = Scenario.Set(engine, 1, Cards.SummonTrap);
        Scenario.Submit(engine, new NormalSummon(0, elf.Id));

        // The turn player auto-passed the summon window; the opponent holds a Trap and is asked.
        Assert.Equal(1, engine.State.Priority);
        Assert.True(ActionCatalog.IsResponding(engine.State, 1));
        Assert.Equal("Opponent Summoned Gemini Elf. Respond?", DuelText.ResponseText(engine.State, 1));
        PlayerCommand response = Assert.Single(ActionCatalog.Responses(engine.LegalActions(1)));
        Assert.Equal(trap.Id, ActionCatalog.CardOf(response));
    }

    [Fact]
    public void InspectorLinesReadThePrintedCard()
    {
        DuelEngine engine = Scenario.Start(0, new[] { Cards.GeminiElf, Cards.PotOfGreed });
        CardInstance elf = Scenario.InHand(engine, 0, Cards.GeminiElf);

        Assert.Equal("Spellcaster / Normal · EARTH · Level 4", DuelText.TypeLine(elf.Def));
        Assert.Equal("ATK 1900 / DEF 900", DuelText.StatLine(elf));
        Assert.Equal("Normal Spell", DuelText.TypeLine(Cards.PotOfGreed));
        Assert.Equal(string.Empty, DuelText.StatLine(Scenario.InHand(engine, 0, Cards.PotOfGreed)));
        Assert.Equal("Quick-Play Spell", DuelText.TypeLine(Cards.Boost));
        Assert.Equal("Counter Trap", DuelText.TypeLine(Cards.NegateTrap));
    }

    [Fact]
    public void OpponentsHiddenCardsStayHidden()
    {
        DuelEngine engine = Scenario.Start(0);
        CardInstance set = Scenario.Set(engine, 1, Cards.AttackTrap);
        CardInstance inHand = engine.State.Player(1).Hand[0];

        Assert.Equal("a face-down card", DuelText.VisibleName(engine.State, set.Id, 0));
        Assert.Equal("a face-down card", DuelText.VisibleName(engine.State, inHand.Id, 0));
        Assert.Equal("Attack Trap", DuelText.VisibleName(engine.State, set.Id, 1));
    }

    [Fact]
    public void EveryCommandAndEventOfARealDuelHasText()
    {
        DuelEngine engine = DuelEngine.Start(TierThreeTests.RealDeck("goat_control"), TierThreeTests.RealDeck("warrior_toolbox"), new DuelOptions { Seed = 11 });
        var texts = new List<string>();
        engine.EventRaised += e =>
        {
            string? text = DuelText.Describe(e, engine.State, 0);
            if (text is not null)
            {
                texts.Add(text);
            }
        };
        int commandsChecked = 0;

        DuelRunner.Play(engine, new HeuristicAgent(AiProfile.Mara, 11), new HeuristicAgent(AiProfile.Nico, 12), afterStep: e =>
        {
            foreach (PlayerCommand command in e.LegalActions(e.ActingPlayer))
            {
                Assert.False(string.IsNullOrWhiteSpace(DuelText.Describe(command, e.State, 0)));
                commandsChecked++;
            }
        });

        Assert.True(engine.State.IsOver);
        Assert.True(commandsChecked > 100);
        Assert.Contains(texts, t => t.StartsWith("Turn ", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("Summoned", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("won", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, t => t.Contains("a card", StringComparison.Ordinal) && !t.Contains("drew a card", StringComparison.Ordinal));
    }
}
