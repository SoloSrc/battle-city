using System;
using System.IO;
using System.Linq;
using BattleCity.Duel.Core;
using BattleCity.Duel.Core.Ai;
using BattleCity.Duel.Core.Model;
using Xunit;

namespace BattleCity.Data.Tests;

public class GameDataTests
{
    public static string DataRoot => Path.Combine(AppContext.BaseDirectory, "data");

    [Fact]
    public void EveryDataFileLoads()
    {
        // Acceptance for issue #26: all data files load in a test.
        GameData data = GameData.Load(DataRoot);

        Assert.Equal(79, data.Cards.Count);
        Assert.Equal(new[] { "beatdown", "goat_control", "rookie_beatdown", "starter", "warrior_toolbox" }, data.Decks.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(new[] { "d1", "d2", "d3" }, data.Duelists.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal(56, data.Shop.Stock.Count);
        Assert.Single(data.Shop.Boosters);
        Assert.Equal(2, data.Avatar.BodyTypes.Count);
    }

    [Fact]
    public void CardPoolMatchesTheGddSubset()
    {
        GameData data = GameData.Load(DataRoot);

        // Counted from the GDD §4.5 table rows (its summary line was corrected with #26; seven rookie vanillas added for Nico's deck).
        Assert.Equal(13, data.Cards.All.Count(c => c.Tier == 1));
        Assert.Equal(27, data.Cards.All.Count(c => c.Tier == 2));
        Assert.Equal(35, data.Cards.All.Count(c => c.Tier == 3));
        Assert.Equal(4, data.Cards.All.Count(c => c.Tier == 4));
        Assert.Equal(3, data.Cards.All.Count(c => c.Kind == CardKind.Fusion));
        Assert.All(data.Cards.All, c => Assert.InRange(c.Limit, 1, 3));
    }

    [Fact]
    public void DuelistDecksAreLegalFortyCardLists()
    {
        GameData data = GameData.Load(DataRoot);

        foreach (DuelistDefinition duelist in data.Duelists.Values)
        {
            DeckDefinition deck = data.Decks[duelist.DeckId];
            Assert.Equal(40, deck.MainCount);
            Assert.Empty(DeckRules.Problems(deck, data.Cards));
        }

        Assert.Equal(5, data.Decks["goat_control"].FusionCount);
        Assert.Equal(data.Decks["beatdown"].Main, data.Decks["starter"].Main);
    }

    [Fact]
    public void DuelistsFollowTheProgressionChain()
    {
        GameData data = GameData.Load(DataRoot);

        Assert.Equal("", data.Duelists["d1"].RequiredFlag);
        Assert.Equal("defeated:d1", data.Duelists["d2"].RequiredFlag);
        Assert.Equal("defeated:d2", data.Duelists["d3"].RequiredFlag);
        Assert.True(data.Duelists["d3"].Ending);
        Assert.Equal(new Reward(600, 1), data.Duelists["d1"].RewardFirst);
        Assert.Equal(new Reward(500, 3), data.Duelists["d3"].RewardRematch);
        Assert.Equal(AiProfile.Nico with { Name = "Nico" }, data.Duelists["d1"].Profile);
        Assert.Equal(AiProfile.ArcadeOwner with { Name = "The Arcade Owner" }, data.Duelists["d3"].Profile);
    }

    [Fact]
    public void ShopSellsNoLimitedCardsAndPricesByTier()
    {
        GameData data = GameData.Load(DataRoot);

        Assert.All(data.Shop.Stock, s => Assert.True(data.Cards[s.CardId].Limit >= 2));
        Assert.Equal(data.Cards.All.Count(c => c.Limit >= 2), data.Shop.Stock.Count);
        Assert.Equal(100, data.Shop.Stock.Single(s => s.CardId == "gemini_elf").Price);
        Assert.Equal(600, data.Shop.Stock.Single(s => s.CardId == "spirit_reaper").Price);
        BoosterDefinition pack = data.Shop.Boosters[0];
        Assert.Equal(300, pack.Price);
        Assert.Equal(5, pack.Count);
        Assert.Equal(100, pack.Weights.Values.Sum());
        Assert.Equal(1, pack.LimitedMax);
    }

    [Fact]
    public void AvatarOptionsMatchTheGdd()
    {
        GameData data = GameData.Load(DataRoot);

        Assert.Equal("Duelist", data.Avatar.Name.Default);
        Assert.Equal(12, data.Avatar.Name.MaxLength);
        Assert.Equal(6, data.Avatar.SkinTones.Count);
        Assert.All(data.Avatar.HairStyles.Values, styles => Assert.Equal(4, styles.Count));
        Assert.Equal(8, data.Avatar.HairColors.Count);
        Assert.Equal(3, data.Avatar.Outfits.Count);
        Assert.Equal(8, data.Avatar.AccentColors.Count);
        Assert.Equal("a", data.Avatar.Defaults.BodyType);
    }

    [Fact]
    public void TierOneDeckFromDataRunsADuel()
    {
        GameData data = GameData.Load(DataRoot);
        var vanilla = data.Cards.All.Where(c => c.IsVanilla).ToList();
        Deck deck = Deck.Repeat(vanilla, 4);

        DuelEngine engine = DuelEngine.Start(deck, deck, new DuelOptions { Seed = 9 });
        DuelRunner.Play(engine, new HeuristicAgent(data.Duelists["d1"].Profile, 1), new HeuristicAgent(data.Duelists["d2"].Profile, 2));

        Assert.True(engine.State.IsOver);
    }

    [Fact]
    public void EveryDuelistDeckIsDuellable()
    {
        GameData data = GameData.Load(DataRoot);

        foreach (DeckDefinition definition in data.Decks.Values)
        {
            Deck deck = definition.ToDeck(data.Cards);
            DuelEngine engine = DuelEngine.Start(deck, deck, new DuelOptions { Seed = 9 });
            Assert.False(engine.State.IsOver);
        }
    }

    [Fact]
    public void MissingDirectoryIsReported()
    {
        var error = Assert.Throws<DataException>(() => GameData.Load(Path.Combine(DataRoot, "nope")));
        Assert.Contains("data directory not found", error.Message);
    }
}
