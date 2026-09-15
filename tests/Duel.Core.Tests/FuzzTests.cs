using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Ai;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Model;
using Xunit;

namespace BattleCity.Duel.Core.Tests;

/// <summary>Random-agent duels from fixed seeds that assert the invariants of systems.md §5.7 after every command.</summary>
public class FuzzTests
{
    private static readonly CardDefinition[] _vanillaPool =
    {
        Cards.GeminiElf, Cards.ArchfiendSoldier, Cards.SummonedSkull, Cards.Weakling, Cards.Wall, Cards.Titan, Cards.PotOfGreed,
    };

    /// <summary>The vanilla pool plus a stub of every response and trigger timing (issue #54 acceptance).</summary>
    private static readonly CardDefinition[] _responsePool =
    {
        Cards.GeminiElf, Cards.Weakling, Cards.Wall, Cards.PotOfGreed,
        Cards.AttackTrap, Cards.SummonTrap, Cards.NegateTrap, Cards.Boost,
        Cards.Drawer, Cards.Avenger, Cards.Flipper, Cards.Destroyer, Cards.Searcher, Cards.StandbyDrawer,
    };

    /// <summary>Every #55 mechanism on top of the vanilla pool: continuous and timed modifiers, equips, control, tokens, counters, costs, Special Summons, Spirits, locks.</summary>
    private static readonly CardDefinition[] _mechanicsPool =
    {
        Cards.GeminiElf, Cards.Squire, Cards.Weakling, Cards.Knight, Cards.Axe, Cards.Snatch, Cards.Controller, Cards.Goats,
        Cards.Breaker, Cards.LifeDraw, Cards.Duo, Cards.Reborn, Cards.FusionCall, Cards.Spirit, Cards.Lock, Cards.Jinzo,
        Cards.Shield, Cards.Piercer, Cards.Reaper, Cards.Banisher, Cards.BookOfMoon, Cards.Burial, Cards.AttackTrap, Cards.Avenger,
    };

    /// <summary>Every real tier 2 card (issue #56) with vanilla monsters to fight over.</summary>
    private static readonly CardDefinition[] _tier2Pool = new[]
    {
        "gemini_elf", "celtic_guardian", "giant_soldier_of_stone", "summoned_skull", "mystical_elf",
        "graceful_charity", "morphing_jar", "fissure", "smashing_ground", "heavy_storm", "mystical_space_typhoon", "lightning_vortex",
        "dust_tornado", "mirror_force", "sakuretsu_armor", "widespread_ruin", "torrential_tribute", "trap_hole",
        "zaborg_the_thunder_monarch", "mobius_the_frost_monarch", "exiled_force", "sangan", "reinforcement_of_the_army",
        "the_warrior_returning_alive", "gravekeepers_spy", "magician_of_faith", "axe_of_despair", "book_of_moon",
        "berserk_gorilla", "goblin_attack_force", "giant_orc", "gravekeepers_guard",
    }.Select(Cards.Real).ToArray();

    /// <summary>Every real tier 3 card (issue #57) with the tier 2 pool's monsters to fight over; the two Fusion monsters ride in the Fusion Deck for Metamorphosis.</summary>
    private static readonly CardDefinition[] _tier3Pool = new[]
    {
        "gemini_elf", "mystical_elf", "sangan", "exiled_force", "magician_of_faith", "book_of_moon", "sakuretsu_armor",
        "enraged_battle_ox", "jinzo", "blade_knight", "don_zaloog", "dd_warrior_lady", "dd_assailant", "mystic_swordsman_lv2",
        "marauding_captain", "command_knight", "chaos_sorcerer", "airknight_parshath", "breaker_the_magical_warrior",
        "tribe_infecting_virus", "sinister_serpent", "tsukuyomi", "asura_priest", "skilled_dark_magician", "kycoo_the_ghost_destroyer",
        "mystic_tomato", "shining_angel", "delinquent_duo", "premature_burial", "snatch_steal", "nobleman_of_crossout",
        "enemy_controller", "scapegoat", "metamorphosis", "swords_of_revealing_light", "creature_swap",
        "ring_of_destruction", "call_of_the_haunted", "bottomless_trap_hole", "waboku",
    }.Select(Cards.Real).ToArray();

    private static readonly CardDefinition[] _tier3Fusion = new[] { "dark_balter_the_terrible", "reaper_on_the_nightmare" }.Select(Cards.Real).ToArray();

    public static IEnumerable<object[]> Seeds() => Enumerable.Range(1, 20).Select(i => new object[] { (ulong)i });

    [Theory]
    [MemberData(nameof(Seeds))]
    public void RandomVanillaDuelsKeepTheInvariantsAndEnd(ulong seed) => Run(seed, _vanillaPool);

    [Theory]
    [MemberData(nameof(Seeds))]
    public void RandomDuelsWithResponsesKeepTheInvariantsAndEnd(ulong seed)
    {
        DuelEngine engine = Run(seed, _responsePool);

        Assert.Empty(engine.State.Chain);
        Assert.Null(engine.State.PendingChoice);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void RandomDuelsWithMechanismsKeepTheInvariantsAndEnd(ulong seed)
    {
        DuelEngine engine = Run(seed, _mechanicsPool, new[] { Cards.FusionBeast });

        Assert.Empty(engine.State.Chain);
        Assert.Null(engine.State.PendingChoice);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void RandomDuelsWithTierTwoCardsKeepTheInvariantsAndEnd(ulong seed)
    {
        DuelEngine engine = Run(seed, _tier2Pool);

        Assert.Empty(engine.State.Chain);
        Assert.Null(engine.State.PendingChoice);
        Assert.Null(engine.State.ResolvingLink);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void RandomDuelsWithTierThreeCardsKeepTheInvariantsAndEnd(ulong seed)
    {
        DuelEngine engine = Run(seed, _tier3Pool, _tier3Fusion);

        Assert.Empty(engine.State.Chain);
        Assert.Null(engine.State.PendingChoice);
        Assert.Null(engine.State.ResolvingLink);
    }

    private static DuelEngine Run(ulong seed, CardDefinition[] pool, CardDefinition[]? fusion = null)
    {
        var deck = new Deck(Enumerable.Range(0, Scenario.DeckSize).Select(i => pool[i % pool.Length]).ToList(), fusion ?? Array.Empty<CardDefinition>());
        DuelEngine engine = DuelEngine.Start(deck, deck, new DuelOptions { Seed = seed }, TestEffects.Registry());
        CheckInvariants(engine);

        int steps = DuelRunner.Play(engine, new RandomAgent(seed), new RandomAgent(seed * 7919), maxSteps: 20_000, afterStep: CheckInvariants);

        Assert.True(engine.State.IsOver, $"seed {seed}: duel still running after {steps} commands");
        Assert.NotEqual(DuelOutcome.None, engine.State.Outcome);
        return engine;
    }

    private static void CheckInvariants(DuelEngine engine)
    {
        DuelState s = engine.State;
        var seen = new HashSet<Guid>();
        foreach (PlayerState p in s.Players)
        {
            Assert.InRange(p.LifePoints, 0, int.MaxValue);
            var cards = p.AllCards.ToList();
            var everything = s.Players.SelectMany(q => q.AllCards).ToList();
            // The 40 main-deck cards a player owns are all somewhere; tokens exist only on the field; a card off the field is with its owner, under its owner's control.
            Assert.Equal(Scenario.DeckSize, everything.Count(c => c.Owner == p.Index && !c.IsToken && c.Def.Kind != CardKind.Fusion));
            Assert.DoesNotContain(p.Deck.Concat(p.Hand).Concat(p.Graveyard).Concat(p.Banished), c => c.IsToken);
            foreach (CardInstance card in cards)
            {
                Assert.True(seen.Add(card.Id), $"{card} appears twice");
                Assert.True(card.IsOnField ? card.Controller == p.Index : card.Owner == p.Index, $"{card} is held by player {p.Index}");
                Assert.True(card.IsOnField || card.Controller == card.Owner, $"{card} off the field is still controlled by {card.Controller}");
                Assert.True(card.Atk >= 0 && card.DefValue >= 0, $"{card} has negative stats");
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
            Assert.All(p.Banished, c => Assert.Equal(Location.Banished, c.Loc));
            Assert.All(p.FusionDeck, c => Assert.Equal(Location.FusionDeck, c.Loc));

            // Equips are attached to a face-up monster on the field or gone; nothing off the field keeps an attachment or counters.
            foreach (CardInstance st in p.SpellTraps)
            {
                if (st.EquippedTo is { } target)
                {
                    Assert.True(s.Find(target) is { Loc: Location.MonsterZone, IsFaceUp: true }, $"{st} is equipped to a monster that is not face-up on the field");
                }
                else
                {
                    bool activating = s.Chain.Any(l => l.Source == st) || s.PendingLink?.Source == st || s.ResolvingLink?.Source == st;
                    Assert.True(st.Def.Spell?.Subtype != SpellSubtype.Equip || st.IsFaceDown || activating, $"{st} is a face-up Equip Spell attached to nothing");
                }
            }

            Assert.All(cards.Where(c => !c.IsOnField), c => Assert.True(c.EquippedTo is null && c.Counters.Count == 0 && c.AtkBonus == 0 && c.Restrictions == Restriction.None && c.LeavesAfterTurn is null && c.AttackTargetsThisTurn.Count == 0, $"{c} kept field state off the field"));
        }

        // Every stored modifier on a card names a card on the field.
        Assert.All(s.Modifiers.Where(m => m.Card is not null), m => Assert.True(s.Find(m.Card!.Value) is { IsOnField: true }, $"modifier {m.Kind} points at a card off the field"));

        // Chain links and the pending activation point at cards that exist; link numbers count up from 1.
        Assert.Equal(Enumerable.Range(1, s.Chain.Count), s.Chain.Select(l => l.Index));
        Assert.All(s.Chain, l => Assert.Same(l.Source, s.Find(l.Source.Id)));
        Assert.True(s.PendingLink is null || s.PendingChoice is not null, "an activation in progress is always waiting on a choice");

        // The attack bookkeeping exists exactly while an attack window is open.
        bool attackWindow = s.Window is Window.AttackDeclared or Window.DamageBeforeCalc or Window.DamageAfterCalc;
        Assert.Equal(attackWindow, s.Attacker is not null);
        Assert.Equal(s.Window is Window.DamageBeforeCalc or Window.DamageAfterCalc, s.DamageSubstep != DamageSubstep.None);
        Assert.True(s.Window != Window.Summon || s.WindowCard is not null, "a summon window names its monster");

        if (s.IsOver)
        {
            Assert.Empty(s.Triggers);
            return;
        }

        // Somebody can always act: the chooser answers, everyone else waits.
        int acting = engine.ActingPlayer;
        Assert.NotEmpty(engine.LegalActions(acting));
        Assert.Empty(engine.LegalActions(1 - acting));
        if (s.PendingChoice is not null)
        {
            Assert.All(engine.LegalActions(acting), a => Assert.IsType<AnswerChoice>(a));
        }
    }
}
