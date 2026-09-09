using BattleCity.Duel.Core;
using Xunit;

namespace BattleCity.Duel.Core.Tests;

public class DuelCoreInfoTests
{
    [Fact]
    public void RulesetIsGoatFormat()
    {
        Assert.Equal("Goat Format", DuelCoreInfo.Ruleset);
    }

    [Fact]
    public void StartingValuesMatchGdd()
    {
        Assert.Equal(8000, DuelCoreInfo.StartingLifePoints);
        Assert.Equal(5, DuelCoreInfo.OpeningHandSize);
    }
}
