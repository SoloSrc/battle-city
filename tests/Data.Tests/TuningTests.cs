using System.IO;
using Xunit;

namespace BattleCity.Data.Tests;

public class TuningTests
{
    [Fact]
    public void ShippedTuningLoadsWithTheSystemsMdValues()
    {
        // systems.md §10: the table values are the file values.
        TuningDefinition t = new TuningLoader().LoadFile(Path.Combine(GameDataTests.DataRoot, "tuning.json"));

        Assert.Equal(2.2f, t.Player.WalkSpeed);
        Assert.Equal(4.5f, t.Player.RunSpeed);
        Assert.Equal(57.0f, t.Camera.Overworld.Pitch);
        Assert.Equal(12.0f, t.Camera.Overworld.Distance);
        Assert.Equal(35.0f, t.Camera.Overworld.Fov);
        Assert.Equal(1.2f, t.Camera.Duel.BlendTime);
        Assert.Equal(8.0f, t.Encounter.ConeRange);
        Assert.Equal(60.0f, t.Encounter.ConeAngle);
        Assert.Equal(7.0f, t.Encounter.StandDistance);
        Assert.Equal(8000, t.Duel.StartLp);
        Assert.Equal(6, t.Duel.HandLimit);
        Assert.Equal(5, t.Duel.OpeningHand);
        Assert.Equal(0.15f, t.Duel.CardTween);
        Assert.Equal(1.0f, t.Anchors.Forward);
        Assert.Equal(0.22f, t.Anchors.SpacingX);
        Assert.Equal(0.28f, t.Anchors.SpacingZ);
    }

    [Fact]
    public void ShippedTuningMatchesTheCodeDefaults()
    {
        // The Default record is the fallback when the file is unreadable; it must not drift from the file.
        TuningDefinition t = new TuningLoader().LoadFile(Path.Combine(GameDataTests.DataRoot, "tuning.json"));

        Assert.Equal(TuningDefinition.Default, t);
    }

    [Fact]
    public void GameDataCarriesTheTuning()
    {
        GameData data = GameData.Load(GameDataTests.DataRoot);

        Assert.Equal(TuningDefinition.Default, data.Tuning);
    }

    [Theory]
    [InlineData("\"walk_speed\": 2.2", "\"walk_speed\": 0", "player.walk_speed")]
    [InlineData("\"cone_angle\": 60", "\"cone_angle\": 400", "encounter.cone_angle")]
    [InlineData("\"start_lp\": 8000", "\"start_lp\": -1", "duel.start_lp")]
    [InlineData("\"lunge_fraction\": 0.35", "\"lunge_fraction\": 2", "anchors.lunge_fraction")]
    [InlineData("\"spacing_z\": 0.28,", "", "anchors.spacing_z")]
    public void BadValuesAreRejectedByKey(string good, string bad, string key)
    {
        string json = File.ReadAllText(Path.Combine(GameDataTests.DataRoot, "tuning.json"));
        Assert.Contains(good, json);

        var e = Assert.Throws<DataException>(() => new TuningLoader().Parse(json.Replace(good, bad), "tuning"));

        Assert.Contains(key, e.Message);
    }

    [Fact]
    public void MissingSectionIsRejected()
    {
        var e = Assert.Throws<DataException>(() => new TuningLoader().Parse("{ \"player\": { \"walk_speed\": 2, \"run_speed\": 4 } }"));

        Assert.Contains("'camera' is required", e.Message);
    }
}
