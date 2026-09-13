using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Ai;
using BattleCity.Duel.Core.Model;
using Xunit;

namespace BattleCity.Duel.Core.Tests;

/// <summary>Random-agent duels from fixed seeds that assert the invariants of systems.md §5.7 after every command.</summary>
public class FuzzTests
{
    private static readonly CardDefinition[] _pool =
    {
        Cards.GeminiElf, Cards.ArchfiendSoldier, Cards.SummonedSkull, Cards.Weakling, Cards.Wall, Cards.Titan, Cards.PotOfGreed,
    };

    public static IEnumerable<object[]> Seeds() => Enumerable.Range(1, 20).Select(i => new object[] { (ulong)i });

    [Theory]
    [MemberData(nameof(Seeds))]
    public void RandomDuelsKeepTheInvariantsAndEnd(ulong seed)
    {
        var deck = new Deck(Enumerable.Range(0, Scenario.DeckSize).Select(i => _pool[i % _pool.Length]).ToList());
        DuelEngine engine = DuelEngine.Start(deck, deck, new DuelOptions { Seed = seed });
        CheckInvariants(engine);

        int steps = DuelRunner.Play(engine, new RandomAgent(seed), new RandomAgent(seed * 7919), maxSteps: 20_000, afterStep: CheckInvariants);

        Assert.True(engine.State.IsOver, $"seed {seed}: duel still running after {steps} commands");
        Assert.NotEqual(DuelOutcome.None, engine.State.Outcome);
    }

    private static void CheckInvariants(DuelEngine engine)
    {
        DuelState s = engine.State;
        var seen = new HashSet<Guid>();
        foreach (PlayerState p in s.Players)
        {
            Assert.InRange(p.LifePoints, 0, DuelCoreInfo.StartingLifePoints);
            var cards = p.AllCards.ToList();
            Assert.Equal(Scenario.DeckSize, cards.Count);
            foreach (CardInstance card in cards)
            {
                Assert.True(seen.Add(card.Id), $"{card} appears twice");
                Assert.Equal(p.Index, card.Owner);
            }

            for (int i = 0; i < PlayerState.ZoneCount; i++)
            {
                CardInstance? m = p.MonsterZones[i];
                if (m is not null)
                {
                    Assert.Equal(Location.MonsterZone, m.Loc);
                    Assert.Equal(i, m.ZoneIndex);
                    Assert.True(m.Pos is Position.FaceUpAttack or Position.FaceUpDefense or Position.FaceDownDefense, $"{m} has S/T position {m.Pos}");
                }

                CardInstance? st = p.SpellTrapZones[i];
                if (st is not null)
                {
                    Assert.Equal(Location.SpellTrapZone, st.Loc);
                    Assert.Equal(i, st.ZoneIndex);
                }
            }

            Assert.All(p.Hand, c => Assert.Equal(Location.Hand, c.Loc));
            Assert.All(p.Deck, c => Assert.Equal(Location.Deck, c.Loc));
            Assert.All(p.Graveyard, c => Assert.Equal(Location.Graveyard, c.Loc));
            if (p.Index != s.TurnPlayer)
            {
                // Tier 1 has no draws on the opponent's turn, so the End Phase discard keeps the idle player at the limit.
                Assert.True(p.Hand.Count <= engine.Options.HandLimit, $"player {p.Index} holds {p.Hand.Count} cards on the opponent's turn");
            }
        }

        // Tier 1 has no responses, so the chain never survives a command and nothing is pending when a player decides.
        Assert.Empty(s.Chain);
        Assert.Null(s.Attacker);
        Assert.Equal(DamageSubstep.None, s.DamageSubstep);
        if (!s.IsOver)
        {
            Assert.NotEmpty(engine.LegalActions(s.Priority));
        }
    }
}
