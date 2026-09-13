using System.Collections.Generic;

namespace BattleCity.Data;

/// <summary>A single card for sale (GDD §5.2).</summary>
public sealed record StockEntry(string CardId, int Price);

/// <summary>A booster pack: <paramref name="Count"/> random cards weighted by tier, at most <paramref name="LimitedMax"/> Limited cards (systems.md §8).</summary>
public sealed record BoosterDefinition(string Id, string Name, int Price, int Count, IReadOnlyDictionary<int, int> Weights, int LimitedMax);

/// <summary><c>data/shop.json</c>.</summary>
public sealed record ShopDefinition(IReadOnlyList<StockEntry> Stock, IReadOnlyList<BoosterDefinition> Boosters);
