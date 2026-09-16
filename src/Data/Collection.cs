using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core;
using BattleCity.Duel.Core.Data;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Data;

/// <summary>
/// The player's cards (systems.md §8): owned copies by id, the Main and Fusion
/// Deck as id lists. Coins stay on <c>Game</c>. Until the deck editor exists the
/// deck is the <c>starter</c> list and boosters only grow <see cref="Owned"/>.
/// </summary>
public sealed class Collection
{
    public const string StarterDeckId = "starter";

    private readonly Dictionary<string, int> _owned = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, int> Owned => _owned;

    public List<string> Deck { get; } = new();

    public List<string> FusionDeck { get; } = new();

    /// <summary>Copies owned in total.</summary>
    public int Total => _owned.Values.Sum();

    /// <summary>The starting collection: exactly the <c>starter</c> deck, owned and built.</summary>
    public static Collection Starter(GameData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        DeckDefinition starter = data.Decks[StarterDeckId];
        var collection = new Collection();
        foreach ((string id, int copies) in starter.Main)
        {
            collection.Add(id, copies);
            collection.Deck.AddRange(Enumerable.Repeat(id, copies));
        }

        foreach ((string id, int copies) in starter.Fusion)
        {
            collection.Add(id, copies);
            collection.FusionDeck.AddRange(Enumerable.Repeat(id, copies));
        }

        return collection;
    }

    public int Count(string cardId) => _owned.TryGetValue(cardId, out int count) ? count : 0;

    public void Add(string cardId, int copies = 1)
    {
        ArgumentException.ThrowIfNullOrEmpty(cardId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(copies);
        _owned[cardId] = Count(cardId) + copies;
    }

    /// <summary>The built deck for <see cref="DuelEngine.Start"/>; ids resolve through <paramref name="library"/>.</summary>
    public Deck ToDeck(CardLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        return new Deck(Deck.Select(id => library[id]).ToList(), FusionDeck.Select(id => library[id]).ToList());
    }

    /// <summary>The deck as a definition so <see cref="DeckRules"/> can judge it.</summary>
    public DeckDefinition ToDefinition(string id = "player", string name = "Player's deck") =>
        new(id, name, string.Empty, Counts(Deck), Counts(FusionDeck));

    private static Dictionary<string, int> Counts(IEnumerable<string> ids) =>
        ids.GroupBy(i => i, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
}
