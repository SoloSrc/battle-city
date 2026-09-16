using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using BattleCity.Duel.Core.Data;

namespace BattleCity.Data;

/// <summary>
/// Reads and writes <c>save.json</c> (systems.md §9). Loading validates every
/// card id against the library and drops the unknown ones (reported through
/// <see cref="LoadResult.Dropped"/>) instead of failing, so a save survives a
/// card being cut from the pool.
/// </summary>
public static class SaveCodec
{
    public const int Version = 1;

    /// <summary>The prefix of a defeated-duelist flag; <c>Defeated</c> holds the ids, <c>Flags</c> everything else.</summary>
    public const string DefeatedPrefix = "defeated:";

    private static readonly JsonSerializerOptions _writeOptions = new(Json.Options) { WriteIndented = true };

    public static string Serialize(SaveData save)
    {
        ArgumentNullException.ThrowIfNull(save);
        var dto = new SaveDto
        {
            Version = Version,
            Seed = save.Seed,
            RngDraws = save.RngDraws,
            Name = save.Name,
            Appearance = new Dictionary<string, string>(save.Appearance, StringComparer.Ordinal),
            Level = save.Level,
            Spawn = save.Spawn,
            Coins = save.Coins,
            Owned = save.Owned.OrderBy(o => o.Key, StringComparer.Ordinal).ToDictionary(o => o.Key, o => o.Value, StringComparer.Ordinal),
            Deck = save.Deck.ToList(),
            FusionDeck = save.FusionDeck.ToList(),
            Defeated = save.Defeated.ToList(),
            Flags = save.Flags.ToList(),
        };
        return JsonSerializer.Serialize(dto, _writeOptions);
    }

    /// <summary>Parses a save; throws <see cref="DataException"/> for malformed JSON, another version or missing essentials.</summary>
    public static LoadResult Parse(string json, CardLibrary cards, string source = "save")
    {
        ArgumentNullException.ThrowIfNull(cards);
        SaveDto dto = Json.Parse<SaveDto>(json, source);
        if (dto.Version != Version)
        {
            throw new DataException($"{source}: save version {dto.Version} is not {Version}.");
        }

        if (string.IsNullOrEmpty(dto.Level) || string.IsNullOrEmpty(dto.Spawn))
        {
            throw new DataException($"{source}: 'level' and 'spawn' are required.");
        }

        if (dto.Coins < 0)
        {
            throw new DataException($"{source}: 'coins' must be zero or more.");
        }

        var dropped = new List<string>();
        var owned = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach ((string id, int copies) in dto.Owned ?? new Dictionary<string, int>())
        {
            if (!cards.Contains(id))
            {
                dropped.Add(id);
            }
            else if (copies > 0)
            {
                owned[id] = copies;
            }
        }

        List<string> deck = Known(dto.Deck, cards, dropped);
        List<string> fusion = Known(dto.FusionDeck, cards, dropped);
        var save = new SaveData(
            dto.Seed,
            dto.RngDraws,
            string.IsNullOrWhiteSpace(dto.Name) ? "Duelist" : dto.Name,
            dto.Appearance ?? new Dictionary<string, string>(StringComparer.Ordinal),
            dto.Level,
            dto.Spawn,
            dto.Coins,
            owned,
            deck,
            fusion,
            (dto.Defeated ?? new List<string>()).Where(d => !string.IsNullOrEmpty(d)).Distinct(StringComparer.Ordinal).ToList(),
            (dto.Flags ?? new List<string>()).Where(f => !string.IsNullOrEmpty(f)).Distinct(StringComparer.Ordinal).ToList());
        return new LoadResult(save, dropped.Distinct(StringComparer.Ordinal).ToList());
    }

    /// <summary>Splits game flags into the defeated duelist ids and the rest.</summary>
    public static (List<string> Defeated, List<string> Flags) SplitFlags(IEnumerable<string> flags)
    {
        ArgumentNullException.ThrowIfNull(flags);
        var defeated = new List<string>();
        var rest = new List<string>();
        foreach (string flag in flags.OrderBy(f => f, StringComparer.Ordinal))
        {
            if (flag.StartsWith(DefeatedPrefix, StringComparison.Ordinal))
            {
                defeated.Add(flag[DefeatedPrefix.Length..]);
            }
            else
            {
                rest.Add(flag);
            }
        }

        return (defeated, rest);
    }

    /// <summary>The game flags a save stands for: every <c>defeated:&lt;id&gt;</c> plus the plain flags.</summary>
    public static IEnumerable<string> JoinFlags(SaveData save)
    {
        ArgumentNullException.ThrowIfNull(save);
        return save.Defeated.Select(id => DefeatedPrefix + id).Concat(save.Flags);
    }

    private static List<string> Known(List<string>? ids, CardLibrary cards, List<string> dropped)
    {
        var known = new List<string>();
        foreach (string id in ids ?? new List<string>())
        {
            if (cards.Contains(id))
            {
                known.Add(id);
            }
            else
            {
                dropped.Add(id);
            }
        }

        return known;
    }

    private sealed class SaveDto
    {
        public int Version { get; set; }

        public ulong Seed { get; set; }

        public ulong RngDraws { get; set; }

        public string? Name { get; set; }

        public Dictionary<string, string>? Appearance { get; set; }

        public string? Level { get; set; }

        public string? Spawn { get; set; }

        public int Coins { get; set; }

        public Dictionary<string, int>? Owned { get; set; }

        public List<string>? Deck { get; set; }

        public List<string>? FusionDeck { get; set; }

        public List<string>? Defeated { get; set; }

        public List<string>? Flags { get; set; }
    }
}

/// <summary>A parsed save and the card ids that were dropped because the library no longer has them.</summary>
public sealed record LoadResult(SaveData Save, IReadOnlyList<string> Dropped);
