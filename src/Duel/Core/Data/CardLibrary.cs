using System;
using System.Collections.Generic;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Data;

/// <summary>Loaded card definitions by id.</summary>
public sealed class CardLibrary
{
    private readonly Dictionary<string, CardDefinition> _cards = new(StringComparer.Ordinal);

    public int Count => _cards.Count;

    public IEnumerable<CardDefinition> All => _cards.Values;

    public CardDefinition this[string id] => Get(id);

    public void Add(CardDefinition card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!_cards.TryAdd(card.Id, card))
        {
            throw new CardDataException($"Duplicate card id '{card.Id}'.");
        }
    }

    public bool Contains(string id) => _cards.ContainsKey(id);

    public bool TryGet(string id, out CardDefinition? card) => _cards.TryGetValue(id, out card);

    public CardDefinition Get(string id)
    {
        if (!_cards.TryGetValue(id, out CardDefinition? card))
        {
            throw new KeyNotFoundException($"Unknown card id '{id}'.");
        }

        return card;
    }
}
