using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Tests;

/// <summary>Vanilla definitions for scripted scenarios; the real tier 1 cards live in <c>data/cards/</c>.</summary>
internal static class Cards
{
    public static readonly CardDefinition GeminiElf = CardDefinition.Vanilla("gemini_elf", "Gemini Elf", "Spellcaster", MonsterAttribute.Earth, 4, 1900, 900);
    public static readonly CardDefinition ArchfiendSoldier = CardDefinition.Vanilla("archfiend_soldier", "Archfiend Soldier", "Fiend", MonsterAttribute.Dark, 4, 1900, 1500);
    public static readonly CardDefinition SummonedSkull = CardDefinition.Vanilla("summoned_skull", "Summoned Skull", "Fiend", MonsterAttribute.Dark, 6, 2500, 1200);
    public static readonly CardDefinition Weakling = CardDefinition.Vanilla("test_weakling", "Weakling", "Beast", MonsterAttribute.Earth, 3, 1500, 1000);
    public static readonly CardDefinition Wall = CardDefinition.Vanilla("test_wall", "Wall", "Rock", MonsterAttribute.Earth, 4, 1000, 2000);
    public static readonly CardDefinition Titan = CardDefinition.Vanilla("test_titan", "Titan", "Warrior", MonsterAttribute.Light, 8, 3000, 2500);
    public static readonly CardDefinition Filler = CardDefinition.Vanilla("test_filler", "Filler", "Beast", MonsterAttribute.Wind, 3, 1200, 800);
    public static readonly CardDefinition PotOfGreed = CardDefinition.NormalSpell("pot_of_greed", "Pot of Greed", "Draw 2 cards.", 1, 1, "pot_of_greed");

    public static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "data", "cards");
}

/// <summary>Builds engines in known states. Decks are unshuffled, so the top cards are scripted.</summary>
internal static class Scenario
{
    public const int DeckSize = 40;

    /// <summary>A 40-card deck whose first draws are <paramref name="top"/> in order (opening hand first).</summary>
    public static Deck DeckWithTop(params CardDefinition[] top)
    {
        var cards = new List<CardDefinition>(Enumerable.Repeat(Cards.Filler, DeckSize - top.Length));
        cards.AddRange(top.Reverse());
        return new Deck(cards);
    }

    public static DuelEngine Start(int firstPlayer, CardDefinition[]? top0 = null, CardDefinition[]? top1 = null, Func<DuelOptions, DuelOptions>? configure = null)
    {
        var options = new DuelOptions { Seed = 7, FirstPlayer = firstPlayer, Shuffle = false };
        options = configure?.Invoke(options) ?? options;
        return DuelEngine.Start(DeckWithTop(top0 ?? Array.Empty<CardDefinition>()), DeckWithTop(top1 ?? Array.Empty<CardDefinition>()), options);
    }

    /// <summary>Player 0's turn 2, Main Phase 1: player 1 took turn 1 and passed.</summary>
    public static DuelEngine AtPlayerZeroTurnTwo(CardDefinition[]? top0 = null)
    {
        DuelEngine engine = Start(1, top0);
        Submit(engine, new Pass(1));
        return engine;
    }

    /// <summary>Puts a monster straight onto <paramref name="player"/>'s field as if it had been there since a previous turn.</summary>
    public static CardInstance Place(DuelEngine engine, int player, CardDefinition def, Position position)
    {
        PlayerState p = engine.State.Player(player);
        int zone = p.FirstFreeMonsterZone();
        var card = new CardInstance(Guid.NewGuid(), def, player)
        {
            Loc = Location.MonsterZone,
            ZoneIndex = zone,
            Pos = position,
        };
        p.MonsterZones[zone] = card;
        return card;
    }

    public static CardInstance InHand(DuelEngine engine, int player, CardDefinition def) =>
        engine.State.Player(player).Hand.First(c => c.Def == def);

    public static void Submit(DuelEngine engine, PlayerCommand command)
    {
        SubmitResult result = engine.Submit(command);
        if (!result.Accepted)
        {
            throw new InvalidOperationException($"{command} rejected: {result.Error}");
        }
    }

    /// <summary>Enters the Battle Phase for the turn player and lets the automatic passes reach the Battle Step.</summary>
    public static void EnterBattle(DuelEngine engine)
    {
        Submit(engine, new EnterBattlePhase(engine.State.TurnPlayer));
    }

    /// <summary>Declares an attack; with no responses the Damage Step runs on the opponent's automatic pass.</summary>
    public static void Attack(DuelEngine engine, CardInstance attacker, CardInstance? target)
    {
        Submit(engine, new DeclareAttack(engine.State.TurnPlayer, attacker.Id, target?.Id));
    }
}
