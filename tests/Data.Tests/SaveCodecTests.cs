using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Rng;
using Xunit;

namespace BattleCity.Data.Tests;

public class SaveCodecTests
{
    private static readonly GameData _data = GameData.Load(GameDataTests.DataRoot);

    [Fact]
    public void RoundTripKeepsEverything()
    {
        // Acceptance for issue #63: save round-trip.
        Collection collection = Collection.Starter(_data);
        collection.Add("gemini_elf", 2);
        var rng = new DuelRng(123456);
        rng.NextUInt64();
        rng.NextUInt64();
        (List<string> defeated, List<string> flags) = SaveCodec.SplitFlags(new[] { "tutorial_done", "defeated:d1", "met_mara" });
        var save = new SaveData(123456, rng.Draws, "Duelist", new Dictionary<string, string> { ["body_type"] = "a" }, "res://levels/district/District.tscn", "arrival", 1100, collection.Owned, collection.Deck, collection.FusionDeck, defeated, flags);

        string json = SaveCodec.Serialize(save);
        LoadResult result = SaveCodec.Parse(json, _data.Cards);

        Assert.Empty(result.Dropped);
        Assert.Equal(save.Seed, result.Save.Seed);
        Assert.Equal(2UL, result.Save.RngDraws);
        Assert.Equal("Duelist", result.Save.Name);
        Assert.Equal("a", result.Save.Appearance["body_type"]);
        Assert.Equal(save.Level, result.Save.Level);
        Assert.Equal("arrival", result.Save.Spawn);
        Assert.Equal(1100, result.Save.Coins);
        Assert.Equal(collection.Owned.OrderBy(o => o.Key), result.Save.Owned.OrderBy(o => o.Key));
        Assert.Equal(collection.Deck, result.Save.Deck);
        Assert.Equal(collection.FusionDeck, result.Save.FusionDeck);
        Assert.Equal(new[] { "d1" }, result.Save.Defeated);
        Assert.Equal(new[] { "met_mara", "tutorial_done" }, result.Save.Flags);
        Assert.Equal(new[] { "defeated:d1", "met_mara", "tutorial_done" }, SaveCodec.JoinFlags(result.Save));
        Assert.Contains("\"version\": 1", json);
        Assert.Contains("\"rng_draws\": 2", json);

        Collection restored = Collection.FromSave(result.Save);
        Assert.Equal(collection.Total, restored.Total);
        Assert.Equal(collection.Deck, restored.Deck);
    }

    [Fact]
    public void UnknownCardIdsAreDropped()
    {
        Collection collection = Collection.Starter(_data);
        var owned = new Dictionary<string, int>(collection.Owned) { ["blue_eyes_white_dragon"] = 3 };
        var deck = collection.Deck.Append("blue_eyes_white_dragon").ToList();
        var save = new SaveData(1, 0, "Duelist", new Dictionary<string, string>(), "res://levels/district/District.tscn", "arrival", 500, owned, deck, collection.FusionDeck, new List<string>(), new List<string>());

        LoadResult result = SaveCodec.Parse(SaveCodec.Serialize(save), _data.Cards);

        Assert.Equal(new[] { "blue_eyes_white_dragon" }, result.Dropped);
        Assert.False(result.Save.Owned.ContainsKey("blue_eyes_white_dragon"));
        Assert.Equal(collection.Deck, result.Save.Deck);
    }

    [Theory]
    [InlineData("{ \"version\": 2, \"level\": \"x\", \"spawn\": \"y\" }", "save version 2")]
    [InlineData("{ \"version\": 1, \"spawn\": \"y\" }", "'level' and 'spawn' are required")]
    [InlineData("{ \"version\": 1, \"level\": \"x\", \"spawn\": \"y\", \"coins\": -5 }", "'coins' must be zero or more")]
    [InlineData("not json", "invalid JSON")]
    public void MalformedSavesAreRejected(string json, string message)
    {
        var e = Assert.Throws<DataException>(() => SaveCodec.Parse(json, _data.Cards));

        Assert.Contains(message, e.Message);
    }

    [Fact]
    public void RngResumesFromTheSavedPosition()
    {
        var original = new DuelRng(99);
        original.Skip(5);
        ulong next = original.NextUInt64();

        var resumed = new DuelRng(99);
        resumed.Skip(5);

        Assert.Equal(next, resumed.NextUInt64());
        Assert.Equal(original.Draws, resumed.Draws);
    }
}
