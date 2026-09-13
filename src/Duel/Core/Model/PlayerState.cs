using System;
using System.Collections.Generic;
using System.Linq;

namespace BattleCity.Duel.Core.Model;

/// <summary>One side of the field (systems.md §5.2).</summary>
public sealed class PlayerState
{
    public const int ZoneCount = 5;

    public PlayerState(int index)
    {
        Index = index;
        LifePoints = DuelCoreInfo.StartingLifePoints;
        Deck = new List<CardInstance>();
        Hand = new List<CardInstance>();
        Graveyard = new List<CardInstance>();
        Banished = new List<CardInstance>();
        FusionDeck = new List<CardInstance>();
        MonsterZones = new CardInstance?[ZoneCount];
        SpellTrapZones = new CardInstance?[ZoneCount];
    }

    private PlayerState(PlayerState other, Dictionary<Guid, CardInstance> copies)
    {
        Index = other.Index;
        LifePoints = other.LifePoints;
        Deck = other.Deck.Select(c => Copy(c, copies)).ToList();
        Hand = other.Hand.Select(c => Copy(c, copies)).ToList();
        Graveyard = other.Graveyard.Select(c => Copy(c, copies)).ToList();
        Banished = other.Banished.Select(c => Copy(c, copies)).ToList();
        FusionDeck = other.FusionDeck.Select(c => Copy(c, copies)).ToList();
        MonsterZones = other.MonsterZones.Select(c => c is null ? null : Copy(c, copies)).ToArray();
        SpellTrapZones = other.SpellTrapZones.Select(c => c is null ? null : Copy(c, copies)).ToArray();
        FieldZone = other.FieldZone is null ? null : Copy(other.FieldZone, copies);
    }

    public int Index { get; }

    public int LifePoints { get; set; }

    /// <summary>Top of the deck is the last element.</summary>
    public List<CardInstance> Deck { get; }

    public List<CardInstance> Hand { get; }

    public List<CardInstance> Graveyard { get; }

    public List<CardInstance> Banished { get; }

    public List<CardInstance> FusionDeck { get; }

    public CardInstance?[] MonsterZones { get; }

    public CardInstance?[] SpellTrapZones { get; }

    /// <summary>Present per the state model; unused in the slice.</summary>
    public CardInstance? FieldZone { get; set; }

    public IEnumerable<CardInstance> Monsters => MonsterZones.Where(c => c is not null)!;

    public IEnumerable<CardInstance> SpellTraps => SpellTrapZones.Where(c => c is not null)!;

    public int MonsterCount => MonsterZones.Count(c => c is not null);

    public int SpellTrapCount => SpellTrapZones.Count(c => c is not null);

    /// <summary>Every card this player currently holds, in every location.</summary>
    public IEnumerable<CardInstance> AllCards =>
        Deck.Concat(Hand).Concat(Graveyard).Concat(Banished).Concat(FusionDeck).Concat(Monsters).Concat(SpellTraps)
            .Concat(FieldZone is null ? Enumerable.Empty<CardInstance>() : new[] { FieldZone });

    public int FirstFreeMonsterZone() => Array.IndexOf(MonsterZones, null);

    public int FirstFreeSpellTrapZone() => Array.IndexOf(SpellTrapZones, null);

    /// <summary>Deep-copies the player; <paramref name="copies"/> maps original card ids to their copies so cross references stay consistent.</summary>
    public PlayerState Clone(Dictionary<Guid, CardInstance> copies)
    {
        ArgumentNullException.ThrowIfNull(copies);
        return new PlayerState(this, copies);
    }

    private static CardInstance Copy(CardInstance card, Dictionary<Guid, CardInstance> copies)
    {
        if (!copies.TryGetValue(card.Id, out CardInstance? copy))
        {
            copy = card.Clone();
            copies[card.Id] = copy;
        }

        return copy;
    }
}
