using System;
using System.Collections.Generic;

namespace BattleCity.Duel.Core.Rng;

/// <summary>
/// Seeded xoshiro256** generator (systems.md §5.1). Every shuffle, coin flip
/// and AI jitter goes through it, so a duel replays from its seed.
/// </summary>
public sealed class DuelRng
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public DuelRng(ulong seed)
    {
        Seed = seed;
        // splitmix64 expands the seed into the four state words.
        ulong x = seed;
        _s0 = SplitMix(ref x);
        _s1 = SplitMix(ref x);
        _s2 = SplitMix(ref x);
        _s3 = SplitMix(ref x);
    }

    private DuelRng(DuelRng other)
    {
        Seed = other.Seed;
        _s0 = other._s0;
        _s1 = other._s1;
        _s2 = other._s2;
        _s3 = other._s3;
    }

    public ulong Seed { get; }

    public ulong NextUInt64()
    {
        ulong result = RotateLeft(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotateLeft(_s3, 45);
        return result;
    }

    /// <summary>Uniform integer in [0, <paramref name="maxExclusive"/>).</summary>
    public int Next(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        }

        return (int)(NextUInt64() % (ulong)maxExclusive);
    }

    /// <summary>Uniform double in [0, 1).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    /// <summary>Standard normal sample (Box–Muller), used for AI jitter.</summary>
    public double NextGaussian()
    {
        double u1 = 1.0 - NextDouble();
        double u2 = NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    public bool CoinFlip() => (NextUInt64() & 1) == 1;

    /// <summary>In-place Fisher–Yates shuffle.</summary>
    public void Shuffle<T>(IList<T> list)
    {
        ArgumentNullException.ThrowIfNull(list);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public DuelRng Clone() => new(this);

    private static ulong SplitMix(ref ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        ulong z = x;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static ulong RotateLeft(ulong x, int k) => (x << k) | (x >> (64 - k));
}
