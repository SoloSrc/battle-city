using System.Collections.Generic;
using System.IO;
using BattleCity.Duel.Core.Data;
using Xunit;

namespace BattleCity.Data.Tests;

/// <summary>Each loader names the file and the rule when a document is wrong (issue #26: clear errors).</summary>
public class LoaderRejectTests
{
    private static readonly CardLibrary _cards = new CardLoader().LoadDirectory(Path.Combine(GameDataTests.DataRoot, "cards"));

    [Theory]
    [InlineData("""{"id":"x","name":"X","main":{"gemini_elf":39}}""", "main deck has 39 cards")]
    [InlineData("""{"id":"x","name":"X","main":{"gemini_elf":4,"archfiend_soldier":36}}""", "Gemini Elf: 4 copies, limit 3")]
    [InlineData("""{"id":"x","name":"X","main":{"pot_of_greed":2,"archfiend_soldier":38}}""", "Pot of Greed: 2 copies, limit 1")]
    [InlineData("""{"id":"x","name":"X","main":{"nobody":40}}""", "unknown card 'nobody'")]
    [InlineData("""{"id":"x","name":"X","main":{"thousand_eyes_restrict":1,"archfiend_soldier":39}}""", "Fusion monster and belongs in the Fusion Deck")]
    [InlineData("""{"id":"x","name":"X","main":{"archfiend_soldier":40},"fusion":{"gemini_elf":1}}""", "Gemini Elf is not a Fusion monster")]
    [InlineData("""{"id":"Bad","name":"X","main":{"archfiend_soldier":40}}""", "'id' must be snake_case")]
    [InlineData("""{"id":"x","main":{"archfiend_soldier":40}}""", "'name' is required")]
    [InlineData("""{"id":"x","name":"X","main":{"archfiend_soldier":40,"gemini_elf":0}}""", "copy count must be at least 1")]
    public void DeckLoaderRejects(string json, string expected)
    {
        var error = Assert.Throws<DataException>(() => new DeckLoader(_cards).Parse(json));
        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void DeckFileNameMustMatchId()
    {
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "renamed.json");
            File.Copy(Path.Combine(GameDataTests.DataRoot, "decks", "beatdown.json"), path);
            var error = Assert.Throws<DataException>(() => new DeckLoader(_cards).LoadFile(path));
            Assert.Contains("does not match the file name", error.Message);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData("""{"duelists":[]}""", "at least one duelist")]
    [InlineData("""{"duelists":[{"id":"d9","name":"Z","area":"plaza","deck":"nope","profile":{},"reward_first":{},"reward_rematch":{}}]}""", "'deck' 'nope' is not a loaded deck")]
    [InlineData("""{"duelists":[{"id":"d9","name":"Z","area":"plaza","deck":"beatdown","required_flag":"Defeated D1","profile":{},"reward_first":{},"reward_rematch":{}}]}""", "is not a flag id")]
    [InlineData("""{"duelists":[{"id":"d9","name":"Z","area":"plaza","deck":"beatdown","profile":{"jitter":-1},"reward_first":{},"reward_rematch":{}}]}""", "'profile.jitter' must be")]
    [InlineData("""{"duelists":[{"id":"d9","name":"Z","area":"plaza","deck":"beatdown","profile":{},"reward_first":{"coins":-5},"reward_rematch":{}}]}""", "'reward_first' coins and boosters must be")]
    [InlineData("""{"duelists":[{"id":"d9","name":"Z","area":"plaza","deck":"beatdown","reward_first":{},"reward_rematch":{}}]}""", "'profile' is required")]
    [InlineData("""{"duelists":[{"id":"d9","name":"Z","area":"plaza","deck":"beatdown","profile":{},"reward_first":{},"reward_rematch":{}},{"id":"d9","name":"Z","area":"plaza","deck":"beatdown","profile":{},"reward_first":{},"reward_rematch":{}}]}""", "duplicate duelist id 'd9'")]
    public void DuelistLoaderRejects(string json, string expected)
    {
        var decks = new DeckLoader(_cards).LoadDirectory(Path.Combine(GameDataTests.DataRoot, "decks"));
        var error = Assert.Throws<DataException>(() => new DuelistLoader(decks).Parse(json));
        Assert.Contains(expected, error.Message);
    }

    [Theory]
    [InlineData("""{"stock":[{"card":"pot_of_greed","price":100}],"boosters":[{"id":"p","name":"P","price":1,"count":1,"weights":{"1":1}}]}""", "Pot of Greed is Limited")]
    [InlineData("""{"stock":[{"card":"nobody","price":100}],"boosters":[]}""", "stock card 'nobody' is unknown")]
    [InlineData("""{"stock":[{"card":"gemini_elf","price":0}],"boosters":[]}""", "needs a positive price")]
    [InlineData("""{"stock":[{"card":"gemini_elf","price":1},{"card":"gemini_elf","price":1}],"boosters":[]}""", "listed twice")]
    [InlineData("""{"stock":[],"boosters":[]}""", "at least one booster")]
    [InlineData("""{"stock":[],"boosters":[{"id":"p","name":"P","price":1,"count":1,"weights":{"7":1}}]}""", "keyed by tier 1–4")]
    [InlineData("""{"stock":[],"boosters":[{"id":"p","name":"P","price":1,"count":1,"weights":{"1":0}}]}""", "weights must sum to more than 0")]
    [InlineData("""{"stock":[],"boosters":[{"id":"p","name":"P","price":0,"count":1,"weights":{"1":1}}]}""", "positive price and count")]
    public void ShopLoaderRejects(string json, string expected)
    {
        var error = Assert.Throws<DataException>(() => new ShopLoader(_cards).Parse(json));
        Assert.Contains(expected, error.Message);
    }

    [Theory]
    [InlineData("""{"name":{"default":"","min_length":1,"max_length":12,"pattern":"^.+$"}}""", "'name' needs default")]
    [InlineData("""{"name":{"default":"Duelist!","min_length":1,"max_length":12,"pattern":"^[A-Za-z]+$"},"body_types":["a"]}""", "does not match its own pattern")]
    [InlineData("""{"name":{"default":"D","min_length":1,"max_length":12,"pattern":"^[A-Za-z]+$"},"body_types":[]}""", "'body_types' must be a non-empty list")]
    [InlineData("""{"name":{"default":"D","min_length":1,"max_length":12,"pattern":"^[A-Za-z]+$"},"body_types":["a"],"skin_tones":["red"]}""", "is not a #rrggbb colour")]
    [InlineData("""{"name":{"default":"D","min_length":1,"max_length":12,"pattern":"^[A-Za-z]+$"},"body_types":["a"],"skin_tones":["#000000"],"hair_colors":["#000000"],"accent_colors":["#000000"],"outfits":["o"],"hair_styles":{"b":["x"]}}""", "at least one style for body type 'a'")]
    [InlineData("""{"name":{"default":"D","min_length":1,"max_length":12,"pattern":"^[A-Za-z]+$"},"body_types":["a"],"skin_tones":["#000000"],"hair_colors":["#000000"],"accent_colors":["#000000"],"outfits":["o"],"hair_styles":{"a":["x"]},"defaults":{"body_type":"a","skin_tone":3}}""", "default 'skin_tone' 3 is out of range")]
    [InlineData("""{"name":{"default":"D","min_length":1,"max_length":12,"pattern":"^[A-Za-z]+$"},"body_types":["a"],"skin_tones":["#000000"],"hair_colors":["#000000"],"accent_colors":["#000000"],"outfits":["o"],"hair_styles":{"a":["x"]},"defaults":{"body_type":"c"}}""", "default body type 'c' is not in 'body_types'")]
    [InlineData("""{"name":{"default":"D","min_length":1,"max_length":12,"pattern":"["},"body_types":["a"]}""", "not a valid regular expression")]
    [InlineData("""{""", "invalid JSON")]
    public void AvatarLoaderRejects(string json, string expected)
    {
        var error = Assert.Throws<DataException>(() => new AvatarLoader().Parse(json));
        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void DeckRulesListEveryProblem()
    {
        var deck = new DeckDefinition("x", "X", "", new Dictionary<string, int> { ["pot_of_greed"] = 3, ["nobody"] = 1 }, new Dictionary<string, int>());

        IReadOnlyList<string> problems = DeckRules.Problems(deck, _cards);

        Assert.Equal(3, problems.Count);
        Assert.False(DeckRules.IsLegal(deck, _cards));
    }
}
