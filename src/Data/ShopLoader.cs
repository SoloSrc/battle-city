using System.Collections.Generic;
using System.IO;
using System.Linq;
using BattleCity.Duel.Core.Data;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Data;

/// <summary>Reads <c>data/shop.json</c>: stock ids must exist and must not be Limited (GDD §5.2); boosters need positive prices, counts and weights.</summary>
public sealed class ShopLoader
{
    private readonly CardLibrary _cards;

    public ShopLoader(CardLibrary cards)
    {
        _cards = cards ?? throw new System.ArgumentNullException(nameof(cards));
    }

    public ShopDefinition LoadFile(string path) => Parse(File.ReadAllText(path), path);

    public ShopDefinition Parse(string json, string source = "shop")
    {
        ShopDto dto = Json.Parse<ShopDto>(json, source);
        var stock = new List<StockEntry>();
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (StockDto entry in dto.Stock ?? new List<StockDto>())
        {
            if (entry.Card is null || !_cards.TryGet(entry.Card, out CardDefinition? card))
            {
                throw new DataException($"{source}: stock card '{entry.Card}' is unknown.");
            }

            if (card!.Limit < 2)
            {
                throw new DataException($"{source}: {card.Name} is Limited and only comes from boosters and rewards (GDD §5.2).");
            }

            if (entry.Price <= 0)
            {
                throw new DataException($"{source}: {card.Name} needs a positive price.");
            }

            if (!seen.Add(card.Id))
            {
                throw new DataException($"{source}: {card.Name} is listed twice in the stock.");
            }

            stock.Add(new StockEntry(card.Id, entry.Price));
        }

        var boosters = new List<BoosterDefinition>();
        foreach (BoosterDto b in dto.Boosters ?? new List<BoosterDto>())
        {
            string where = $"{source} booster '{b.Id}'";
            if (!Ids.IsSnakeCase(b.Id))
            {
                throw new DataException($"{source}: booster 'id' must be snake_case (got '{b.Id}').");
            }

            if (string.IsNullOrWhiteSpace(b.Name) || b.Price <= 0 || b.Count <= 0 || b.LimitedMax < 0)
            {
                throw new DataException($"{where}: needs a name, a positive price and count, and limited_max ≥ 0.");
            }

            var weights = new Dictionary<int, int>();
            foreach ((string tier, int weight) in b.Weights ?? new Dictionary<string, int>())
            {
                if (!int.TryParse(tier, out int t) || t is < 1 or > 4 || weight < 0)
                {
                    throw new DataException($"{where}: weights are keyed by tier 1–4 with non-negative values (got '{tier}': {weight}).");
                }

                weights[t] = weight;
            }

            if (weights.Values.Sum() <= 0)
            {
                throw new DataException($"{where}: weights must sum to more than 0.");
            }

            boosters.Add(new BoosterDefinition(b.Id!, b.Name!, b.Price, b.Count, weights, b.LimitedMax));
        }

        if (boosters.Count == 0)
        {
            throw new DataException($"{source}: at least one booster is required (duel rewards are boosters).");
        }

        return new ShopDefinition(stock, boosters);
    }

    private sealed class ShopDto
    {
        public List<StockDto>? Stock { get; set; }

        public List<BoosterDto>? Boosters { get; set; }
    }

    private sealed class StockDto
    {
        public string? Card { get; set; }

        public int Price { get; set; }
    }

    private sealed class BoosterDto
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public int Price { get; set; }

        public int Count { get; set; }

        public Dictionary<string, int>? Weights { get; set; }

        public int LimitedMax { get; set; }
    }
}
