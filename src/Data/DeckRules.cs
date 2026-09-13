using System;
using System.Collections.Generic;
using BattleCity.Duel.Core.Data;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Data;

/// <summary>Deck validity (systems.md §8, GDD §5.3): 40–60 cards, copies within <c>limit</c>, Fusion monsters only in the Fusion Deck.</summary>
public static class DeckRules
{
    public const int MinMain = 40;
    public const int MaxMain = 60;

    /// <summary>Every rule the deck breaks; empty when it is legal.</summary>
    public static IReadOnlyList<string> Problems(DeckDefinition deck, CardLibrary library)
    {
        ArgumentNullException.ThrowIfNull(deck);
        ArgumentNullException.ThrowIfNull(library);
        var problems = new List<string>();
        if (deck.MainCount is < MinMain or > MaxMain)
        {
            problems.Add($"main deck has {deck.MainCount} cards; {MinMain}–{MaxMain} required");
        }

        foreach ((string id, int count) in deck.Main)
        {
            if (!library.TryGet(id, out CardDefinition? card))
            {
                problems.Add($"unknown card '{id}'");
                continue;
            }

            if (card!.Kind == CardKind.Fusion)
            {
                problems.Add($"{card.Name} is a Fusion monster and belongs in the Fusion Deck");
            }

            CheckCopies(problems, card, count);
        }

        foreach ((string id, int count) in deck.Fusion)
        {
            if (!library.TryGet(id, out CardDefinition? card))
            {
                problems.Add($"unknown card '{id}'");
                continue;
            }

            if (card!.Kind != CardKind.Fusion)
            {
                problems.Add($"{card.Name} is not a Fusion monster");
            }

            CheckCopies(problems, card, count);
        }

        return problems;
    }

    public static bool IsLegal(DeckDefinition deck, CardLibrary library) => Problems(deck, library).Count == 0;

    private static void CheckCopies(List<string> problems, CardDefinition card, int count)
    {
        if (count < 1)
        {
            problems.Add($"{card.Name}: copy count must be at least 1");
        }
        else if (card.Limit == 0)
        {
            problems.Add($"{card.Name} is Forbidden");
        }
        else if (count > card.Limit)
        {
            problems.Add($"{card.Name}: {count} copies, limit {card.Limit}");
        }
    }
}
