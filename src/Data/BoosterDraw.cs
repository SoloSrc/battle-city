using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Data;
using BattleCity.Duel.Core.Model;
using BattleCity.Duel.Core.Rng;

namespace BattleCity.Data;

/// <summary>
/// Opens a booster (systems.md §8): each card picks a tier by the pack's
/// weights, then a uniform card of that tier from the library; Limited cards
/// (limit 1) stop appearing once <see cref="BoosterDefinition.LimitedMax"/> of
/// them are in the pack. Forbidden cards and tokens (limit 0) never appear.
/// Deterministic for a given <see cref="DuelRng"/> state.
/// </summary>
public static class BoosterDraw
{
    public static IReadOnlyList<CardDefinition> Open(BoosterDefinition pack, CardLibrary library, DuelRng rng)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(rng);
        Dictionary<int, List<CardDefinition>> byTier = library.All
            .Where(c => !c.IsToken && c.Limit > 0)
            .GroupBy(c => c.Tier)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Id, StringComparer.Ordinal).ToList());
        var cards = new List<CardDefinition>(pack.Count);
        int limited = 0;
        for (int i = 0; i < pack.Count; i++)
        {
            bool allowLimited = limited < pack.LimitedMax;
            List<(int Tier, int Weight, List<CardDefinition> Pool)> tiers = pack.Weights
                .Where(w => w.Value > 0)
                .Select(w => (Tier: w.Key, Weight: w.Value, Pool: Eligible(byTier, w.Key, allowLimited)))
                .Where(t => t.Pool.Count > 0)
                .OrderBy(t => t.Tier)
                .ToList();
            if (tiers.Count == 0)
            {
                throw new DataException($"booster '{pack.Id}': no card in the library matches its tier weights.");
            }

            int roll = rng.Next(tiers.Sum(t => t.Weight));
            (int Tier, int Weight, List<CardDefinition> Pool) chosen = tiers[^1];
            foreach ((int Tier, int Weight, List<CardDefinition> Pool) tier in tiers)
            {
                if (roll < tier.Weight)
                {
                    chosen = tier;
                    break;
                }

                roll -= tier.Weight;
            }

            CardDefinition card = chosen.Pool[rng.Next(chosen.Pool.Count)];
            if (card.Limit == 1)
            {
                limited++;
            }

            cards.Add(card);
        }

        return cards;
    }

    private static List<CardDefinition> Eligible(Dictionary<int, List<CardDefinition>> byTier, int tier, bool allowLimited)
    {
        if (!byTier.TryGetValue(tier, out List<CardDefinition>? pool))
        {
            return new List<CardDefinition>();
        }

        return allowLimited ? pool : pool.Where(c => c.Limit > 1).ToList();
    }
}
