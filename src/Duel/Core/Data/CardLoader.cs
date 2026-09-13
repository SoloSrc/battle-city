using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using BattleCity.Duel.Core.Effects;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Data;

/// <summary>
/// Reads <c>data/cards/&lt;id&gt;.json</c> (systems.md §5.3, architecture.md §5).
/// Every effect id must exist in the <see cref="EffectRegistry"/>, so a
/// card whose tier is not implemented yet fails here, not mid-duel.
/// </summary>
public sealed partial class CardLoader
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly EffectRegistry _effects;

    public CardLoader(EffectRegistry? effects = null)
    {
        _effects = effects ?? EffectRegistry.CreateDefault();
    }

    /// <summary>Loads every <c>*.json</c> in <paramref name="directory"/>; the file name must equal the card id.</summary>
    public CardLibrary LoadDirectory(string directory)
    {
        var library = new CardLibrary();
        foreach (string path in Directory.EnumerateFiles(directory, "*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            library.Add(LoadFile(path));
        }

        return library;
    }

    public CardDefinition LoadFile(string path)
    {
        CardDefinition card = Parse(File.ReadAllText(path), path);
        string expected = Path.GetFileNameWithoutExtension(path);
        if (card.Id != expected)
        {
            throw new CardDataException($"{path}: id '{card.Id}' does not match the file name '{expected}'.");
        }

        return card;
    }

    /// <summary>Parses one card document; <paramref name="source"/> names it in errors.</summary>
    public CardDefinition Parse(string json, string source = "card")
    {
        CardDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<CardDto>(json, _jsonOptions);
        }
        catch (JsonException e)
        {
            throw new CardDataException($"{source}: invalid JSON: {e.Message}", e);
        }

        if (dto is null)
        {
            throw new CardDataException($"{source}: empty document.");
        }

        return Convert(dto, source);
    }

    private CardDefinition Convert(CardDto dto, string source)
    {
        if (string.IsNullOrEmpty(dto.Id) || !IdPattern().IsMatch(dto.Id))
        {
            throw new CardDataException($"{source}: 'id' must be snake_case (got '{dto.Id}').");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new CardDataException($"{source}: 'name' is required.");
        }

        CardKind kind = ParseEnum<CardKind>(dto.Kind, "kind", source);
        bool wantsMonster = kind is CardKind.Monster or CardKind.Fusion;
        if (wantsMonster != (dto.Monster is not null))
        {
            throw new CardDataException($"{source}: 'monster' must be present exactly when kind is monster or fusion.");
        }

        if ((kind == CardKind.Spell) != (dto.Spell is not null))
        {
            throw new CardDataException($"{source}: 'spell' must be present exactly when kind is spell.");
        }

        if ((kind == CardKind.Trap) != (dto.Trap is not null))
        {
            throw new CardDataException($"{source}: 'trap' must be present exactly when kind is trap.");
        }

        if (dto.Limit is < 0 or > 3)
        {
            throw new CardDataException($"{source}: 'limit' must be 0–3 (April 2005 list).");
        }

        if (dto.Tier is < 1 or > 4)
        {
            throw new CardDataException($"{source}: 'tier' must be 1–4.");
        }

        List<string> effects = dto.Effects ?? new List<string>();
        foreach (string effect in effects)
        {
            if (!_effects.Contains(effect))
            {
                throw new CardDataException($"{source}: effect '{effect}' is not implemented (registered: {string.Join(", ", _effects.Ids)}).");
            }
        }

        MonsterStats? monster = null;
        if (dto.Monster is not null)
        {
            MonsterDto m = dto.Monster;
            if (string.IsNullOrWhiteSpace(m.Type))
            {
                throw new CardDataException($"{source}: 'monster.type' is required.");
            }

            if (m.Level is < 1 or > 12 || m.Atk < 0 || m.Def < 0)
            {
                throw new CardDataException($"{source}: monster level must be 1–12 and ATK/DEF non-negative.");
            }

            MonsterCategory category = ParseEnum<MonsterCategory>(m.Category, "monster.category", source);
            if (category == MonsterCategory.Normal && effects.Count > 0)
            {
                throw new CardDataException($"{source}: a normal monster has no effects.");
            }

            monster = new MonsterStats(m.Type, ParseEnum<MonsterAttribute>(m.Attribute, "monster.attribute", source), m.Level, m.Atk, m.Def, category);
        }

        SpellInfo? spell = dto.Spell is null ? null : new SpellInfo(ParseEnum<SpellSubtype>(dto.Spell.Subtype, "spell.subtype", source));
        TrapInfo? trap = dto.Trap is null ? null : new TrapInfo(ParseEnum<TrapSubtype>(dto.Trap.Subtype, "trap.subtype", source));
        if (kind == CardKind.Fusion && (dto.Materials is null || dto.Materials.Count < 2))
        {
            throw new CardDataException($"{source}: a fusion monster lists at least two 'materials'.");
        }

        return new CardDefinition(
            dto.Id,
            dto.Name,
            kind,
            monster,
            spell,
            trap,
            dto.Text ?? string.Empty,
            dto.Limit,
            dto.Tier,
            effects,
            dto.Materials ?? new List<string>());
    }

    private static T ParseEnum<T>(string? value, string field, string source)
        where T : struct, Enum
    {
        if (!string.IsNullOrEmpty(value) && Enum.TryParse(value.Replace("_", string.Empty, StringComparison.Ordinal), ignoreCase: true, out T parsed))
        {
            return parsed;
        }

        throw new CardDataException($"{source}: '{field}' has unknown value '{value}' (expected one of {string.Join(", ", Enum.GetNames<T>())}).");
    }

    [GeneratedRegex("^[a-z0-9_]+$")]
    private static partial Regex IdPattern();

    private sealed class CardDto
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Kind { get; set; }

        public MonsterDto? Monster { get; set; }

        public SpellDto? Spell { get; set; }

        public TrapDto? Trap { get; set; }

        public string? Text { get; set; }

        public int Limit { get; set; } = 3;

        public int Tier { get; set; }

        public List<string>? Effects { get; set; }

        public List<string>? Materials { get; set; }
    }

    private sealed class MonsterDto
    {
        public string? Type { get; set; }

        public string? Attribute { get; set; }

        public int Level { get; set; }

        public int Atk { get; set; }

        public int Def { get; set; }

        public string? Category { get; set; }
    }

    private sealed class SpellDto
    {
        public string? Subtype { get; set; }
    }

    private sealed class TrapDto
    {
        public string? Subtype { get; set; }
    }
}
