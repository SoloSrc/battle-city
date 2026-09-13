using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Tests;

/// <summary>Definitions for scripted scenarios; the real cards live in <c>data/cards/</c>, the effect cards here use <see cref="TestEffects"/>.</summary>
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

    public static readonly CardDefinition AttackTrap = CardDefinition.TrapCard("test_attack_trap", "Attack Trap", TrapSubtype.Normal, "Destroy the attacking monster.", 3, 2, TestEffects.AttackTrap.EffectId);
    public static readonly CardDefinition SummonTrap = CardDefinition.TrapCard("test_summon_trap", "Summon Trap", TrapSubtype.Normal, "Destroy the Summoned monster.", 3, 2, TestEffects.SummonTrap.EffectId);
    public static readonly CardDefinition NegateTrap = CardDefinition.TrapCard("test_negate_trap", "Negate Trap", TrapSubtype.Counter, "Negate the opponent's last link.", 3, 2, TestEffects.NegateTrap.EffectId);
    public static readonly CardDefinition Boost = CardDefinition.SpellCard("test_boost", "Boost", SpellSubtype.Quick, "A monster gains 700 ATK.", 3, 2, TestEffects.BoostSpell.EffectId);
    public static readonly CardDefinition Drawer = CardDefinition.EffectMonster("test_drawer", "Drawer", "Beast", MonsterAttribute.Earth, 4, 1500, 1000, MonsterCategory.Effect, 2, TestEffects.OptionalSummonDraw.EffectId);
    public static readonly CardDefinition Avenger = CardDefinition.EffectMonster("test_avenger", "Avenger", "Warrior", MonsterAttribute.Earth, 4, 1400, 1000, MonsterCategory.Effect, 2, TestEffects.BattleDestroyedDraw.EffectId);
    public static readonly CardDefinition Flipper = CardDefinition.EffectMonster("test_flipper", "Flipper", "Spellcaster", MonsterAttribute.Light, 3, 300, 400, MonsterCategory.Flip, 2, TestEffects.FlipDraw.EffectId);
    public static readonly CardDefinition Destroyer = CardDefinition.EffectMonster("test_destroyer", "Destroyer", "Fiend", MonsterAttribute.Dark, 4, 1600, 1000, MonsterCategory.Effect, 3, TestEffects.IgnitionDestroy.EffectId);
    public static readonly CardDefinition Searcher = CardDefinition.EffectMonster("test_searcher", "Searcher", "Fiend", MonsterAttribute.Dark, 3, 1000, 600, MonsterCategory.Effect, 2, TestEffects.FieldToGraveDraw.EffectId);
    public static readonly CardDefinition StandbyDrawer = CardDefinition.EffectMonster("test_standby_drawer", "Standby Drawer", "Fairy", MonsterAttribute.Light, 4, 1000, 1000, MonsterCategory.Effect, 2, TestEffects.StandbyDraw.EffectId);

    // Issue #55 mechanisms.
    public static readonly CardDefinition Squire = CardDefinition.Vanilla("test_squire", "Squire", "Warrior", MonsterAttribute.Earth, 4, 1400, 1200);
    public static readonly CardDefinition Knight = CardDefinition.EffectMonster("test_knight", "Knight", "Warrior", MonsterAttribute.Fire, 4, 1200, 1900, MonsterCategory.Effect, 3, TestEffects.WarriorBoost.EffectId);
    public static readonly CardDefinition Axe = CardDefinition.SpellCard("test_axe", "Axe", SpellSubtype.Equip, "The equipped monster gains 1000 ATK.", 3, 2, TestEffects.EquipBoost.EffectId);
    public static readonly CardDefinition Snatch = CardDefinition.SpellCard("test_snatch", "Snatch", SpellSubtype.Equip, "Take control of a face-up monster.", 1, 3, TestEffects.StealEquip.EffectId);
    public static readonly CardDefinition Controller = CardDefinition.SpellCard("test_controller", "Controller", SpellSubtype.Quick, "Take control until the End Phase.", 3, 3, TestEffects.TempSteal.EffectId);
    public static readonly CardDefinition Goats = CardDefinition.SpellCard("test_goats", "Goats", SpellSubtype.Quick, "Special Summon 4 Sheep Tokens.", 3, 3, TestEffects.TokenMaker.EffectId);
    public static readonly CardDefinition Breaker = CardDefinition.EffectMonster("test_breaker", "Breaker", "Spellcaster", MonsterAttribute.Dark, 4, 1600, 1000, MonsterCategory.Effect, 3, TestEffects.CounterCharge.EffectId, TestEffects.CounterDestroy.EffectId);
    public static readonly CardDefinition LifeDraw = CardDefinition.NormalSpell("test_life_draw", "Life Draw", "Pay 1000 Life Points: draw 1 card.", 3, 3, TestEffects.LifeCostDraw.EffectId);
    public static readonly CardDefinition Duo = CardDefinition.NormalSpell("test_duo", "Duo", "The opponent discards 1 random card.", 3, 3, TestEffects.RandomDiscard.EffectId);
    public static readonly CardDefinition Reborn = CardDefinition.SpellCard("test_reborn", "Reborn", SpellSubtype.Quick, "Special Summon a monster from your Graveyard.", 3, 3, TestEffects.Reviver.EffectId);
    public static readonly CardDefinition FusionCall = CardDefinition.NormalSpell("test_fusion_call", "Fusion Call", "Special Summon from the Fusion Deck.", 3, 3, TestEffects.FusionCall.EffectId);
    public static readonly CardDefinition FusionBeast = new("test_fusion_beast", "Fusion Beast", CardKind.Fusion, new MonsterStats("Beast", MonsterAttribute.Earth, 6, 2400, 2000, MonsterCategory.Fusion), null, null, string.Empty, 3, 3, Array.Empty<string>(), new[] { "test_squire", "test_filler" });
    public static readonly CardDefinition Spirit = CardDefinition.EffectMonster("test_spirit", "Spirit", "Fairy", MonsterAttribute.Light, 4, 1700, 1000, MonsterCategory.Spirit, 3);
    public static readonly CardDefinition Lock = CardDefinition.NormalSpell("test_lock", "Lock", "A monster cannot change position until the end of your next turn.", 3, 3, TestEffects.PositionLock.EffectId);
    public static readonly CardDefinition Jinzo = CardDefinition.EffectMonster("test_jinzo", "Jinzo", "Machine", MonsterAttribute.Dark, 6, 2400, 1500, MonsterCategory.Effect, 3, TestEffects.TrapLock.EffectId);
    public static readonly CardDefinition Shield = CardDefinition.TrapCard("test_shield", "Shield", TrapSubtype.Normal, "No battle damage, no battle destruction this turn.", 3, 3, TestEffects.Shield.EffectId);
    public static readonly CardDefinition Piercer = CardDefinition.EffectMonster("test_piercer", "Piercer", "Warrior", MonsterAttribute.Light, 4, 1900, 1400, MonsterCategory.Effect, 3, TestEffects.Piercer.EffectId);
    public static readonly CardDefinition Reaper = CardDefinition.EffectMonster("test_reaper", "Reaper", "Zombie", MonsterAttribute.Dark, 3, 300, 200, MonsterCategory.Effect, 4, TestEffects.Reaper.EffectId);
    public static readonly CardDefinition Banisher = CardDefinition.NormalSpell("test_banisher", "Banisher", "Banish a face-up monster the opponent controls.", 3, 3, TestEffects.Banisher.EffectId);
    public static readonly CardDefinition BookOfMoon = CardDefinition.SpellCard("test_book_of_moon", "Book of Moon", SpellSubtype.Quick, "Flip a face-up monster face-down.", 3, 2, TestEffects.FlipDown.EffectId);
    public static readonly CardDefinition Burial = CardDefinition.SpellCard("test_burial", "Burial", SpellSubtype.Equip, "Pay 800 Life Points: Special Summon from your Graveyard and equip.", 1, 3, TestEffects.Burial.EffectId);

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

    public static DuelEngine Start(int firstPlayer, CardDefinition[]? top0 = null, CardDefinition[]? top1 = null, Func<DuelOptions, DuelOptions>? configure = null, CardDefinition[]? fusion0 = null)
    {
        var options = new DuelOptions { Seed = 7, FirstPlayer = firstPlayer, Shuffle = false };
        options = configure?.Invoke(options) ?? options;
        Deck deck0 = DeckWithTop(top0 ?? Array.Empty<CardDefinition>()) with { Fusion = fusion0 ?? Array.Empty<CardDefinition>() };
        return DuelEngine.Start(deck0, DeckWithTop(top1 ?? Array.Empty<CardDefinition>()), options, TestEffects.Registry());
    }

    /// <summary>Player 0's turn 2, Main Phase 1: player 1 took turn 1 and passed.</summary>
    public static DuelEngine AtPlayerZeroTurnTwo(CardDefinition[]? top0 = null, CardDefinition[]? top1 = null, Func<DuelOptions, DuelOptions>? configure = null, CardDefinition[]? fusion0 = null)
    {
        DuelEngine engine = Start(1, top0, top1, configure, fusion0);
        PassUntil(engine, s => s.TurnNumber == 2 && s.Phase == Phase.Main1);
        return engine;
    }

    /// <summary>Passes for whoever holds priority until <paramref name="done"/> holds; for engines with automatic passes off.</summary>
    public static void PassUntil(DuelEngine engine, Func<DuelState, bool> done)
    {
        for (int i = 0; i < 50 && !done(engine.State); i++)
        {
            Submit(engine, new Pass(engine.State.Priority));
        }

        if (!done(engine.State))
        {
            throw new InvalidOperationException("the state was not reached within 50 passes");
        }
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
        engine.Refresh();
        return card;
    }

    /// <summary>Puts a Spell or Trap face-down on <paramref name="player"/>'s field as if it had been Set on a previous turn.</summary>
    public static CardInstance Set(DuelEngine engine, int player, CardDefinition def)
    {
        PlayerState p = engine.State.Player(player);
        int zone = p.FirstFreeSpellTrapZone();
        var card = new CardInstance(Guid.NewGuid(), def, player)
        {
            Loc = Location.SpellTrapZone,
            ZoneIndex = zone,
            Pos = Position.FaceDown,
        };
        p.SpellTrapZones[zone] = card;
        engine.Refresh();
        return card;
    }

    /// <summary>Puts a card straight into <paramref name="player"/>'s Graveyard.</summary>
    public static CardInstance InGraveyard(DuelEngine engine, int player, CardDefinition def)
    {
        var card = new CardInstance(Guid.NewGuid(), def, player) { Loc = Location.Graveyard };
        engine.State.Player(player).Graveyard.Add(card);
        return card;
    }

    /// <summary>Passes for whoever holds priority until the chain has resolved.</summary>
    public static void ResolveChain(DuelEngine engine)
    {
        for (int i = 0; i < 10 && engine.State.Chain.Count > 0; i++)
        {
            Submit(engine, new Pass(engine.State.Priority));
        }
    }

    /// <summary>Resolves the pending choice with the given options.</summary>
    public static void Answer(DuelEngine engine, params Guid[] selected) =>
        Submit(engine, new AnswerChoice(engine.State.PendingChoice!.Player, selected));

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

    /// <summary>Declares an attack; with no responses the automatic passes run the Damage Step to its end.</summary>
    public static void Attack(DuelEngine engine, CardInstance attacker, CardInstance? target)
    {
        Submit(engine, new DeclareAttack(engine.State.TurnPlayer, attacker.Id, target?.Id));
    }
}
