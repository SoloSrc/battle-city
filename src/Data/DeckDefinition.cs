using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core;
using BattleCity.Duel.Core.Data;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Data;

/// <summary>A deck list from <c>data/decks/&lt;id&gt;.json</c>: card id to copy count for the Main and Fusion Decks (systems.md §8).</summary>
public sealed record DeckDefinition(
    string Id,
    string Name,
    string Description,
    IReadOnlyDictionary<string, int> Main,
    IReadOnlyDictionary<string, int> Fusion)
{
    public int MainCount => Main.Values.Sum();

    public int FusionCount => Fusion.Values.Sum();

    /// <summary>Expands the counts into a <see cref="Deck"/> for <see cref="DuelEngine.Start"/>; ids are resolved through <paramref name="library"/>.</summary>
    public Deck ToDeck(CardLibrary library)
    {
        System.ArgumentNullException.ThrowIfNull(library);
        return new Deck(Expand(Main, library), Expand(Fusion, library));
    }

    private static List<CardDefinition> Expand(IReadOnlyDictionary<string, int> counts, CardLibrary library) =>
        counts.SelectMany(pair => Enumerable.Repeat(library[pair.Key], pair.Value)).ToList();
}
