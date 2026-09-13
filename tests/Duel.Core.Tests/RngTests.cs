using System.Linq;
using BattleCity.Duel.Core.Rng;
using Xunit;

namespace BattleCity.Duel.Core.Tests;

public class RngTests
{
    [Fact]
    public void SameSeedSameSequence()
    {
        var a = new DuelRng(99);
        var b = new DuelRng(99);

        Assert.Equal(Enumerable.Range(0, 16).Select(_ => a.NextUInt64()), Enumerable.Range(0, 16).Select(_ => b.NextUInt64()));
    }

    [Fact]
    public void CloneContinuesFromTheSamePoint()
    {
        var a = new DuelRng(5);
        a.NextUInt64();
        DuelRng b = a.Clone();

        Assert.Equal(a.Next(1000), b.Next(1000));
    }

    [Fact]
    public void ShuffleIsAPermutation()
    {
        var rng = new DuelRng(3);
        var list = Enumerable.Range(0, 40).ToList();

        rng.Shuffle(list);

        Assert.Equal(Enumerable.Range(0, 40), list.OrderBy(i => i));
        Assert.NotEqual(Enumerable.Range(0, 40), list);
    }

    [Fact]
    public void DoublesStayInUnitInterval()
    {
        var rng = new DuelRng(11);
        Assert.All(Enumerable.Range(0, 1000).Select(_ => rng.NextDouble()), d => Assert.InRange(d, 0.0, 0.999999999999));
    }
}
