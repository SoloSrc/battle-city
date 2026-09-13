using System.Collections.Generic;
using System.IO;
using System.Linq;
using BattleCity.Duel.Core.Data;

namespace BattleCity.Data;

/// <summary>Reads <c>data/decks/&lt;id&gt;.json</c> and checks each list against <see cref="DeckRules"/>.</summary>
public sealed class DeckLoader
{
    private readonly CardLibrary _cards;

    public DeckLoader(CardLibrary cards)
    {
        _cards = cards ?? throw new System.ArgumentNullException(nameof(cards));
    }

    public IReadOnlyDictionary<string, DeckDefinition> LoadDirectory(string directory)
    {
        var decks = new Dictionary<string, DeckDefinition>(System.StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(directory, "*.json").OrderBy(p => p, System.StringComparer.Ordinal))
        {
            DeckDefinition deck = LoadFile(path);
            if (!decks.TryAdd(deck.Id, deck))
            {
                throw new DataException($"{path}: duplicate deck id '{deck.Id}'.");
            }
        }

        return decks;
    }

    public DeckDefinition LoadFile(string path)
    {
        DeckDefinition deck = Parse(File.ReadAllText(path), path);
        string expected = Path.GetFileNameWithoutExtension(path);
        if (deck.Id != expected)
        {
            throw new DataException($"{path}: id '{deck.Id}' does not match the file name '{expected}'.");
        }

        return deck;
    }

    public DeckDefinition Parse(string json, string source = "deck")
    {
        DeckDto dto = Json.Parse<DeckDto>(json, source);
        if (!Ids.IsSnakeCase(dto.Id))
        {
            throw new DataException($"{source}: 'id' must be snake_case (got '{dto.Id}').");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new DataException($"{source}: 'name' is required.");
        }

        var deck = new DeckDefinition(dto.Id!, dto.Name, dto.Description ?? string.Empty, dto.Main ?? new Dictionary<string, int>(), dto.Fusion ?? new Dictionary<string, int>());
        IReadOnlyList<string> problems = DeckRules.Problems(deck, _cards);
        if (problems.Count > 0)
        {
            throw new DataException($"{source}: {string.Join("; ", problems)}.");
        }

        return deck;
    }

    private sealed class DeckDto
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }

        public Dictionary<string, int>? Main { get; set; }

        public Dictionary<string, int>? Fusion { get; set; }
    }
}
