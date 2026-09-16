using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core;
using BattleCity.Duel.Core.Model;
using BattleCity.Duel.Core.Rng;
using Xunit;

namespace BattleCity.Data.Tests;

public class CollectionTests
{
    private static readonly GameData _data = GameData.Load(GameDataTests.DataRoot);

    [Fact]
    public void StarterCollectionIsTheStarterDeck()
    {
        Collection collection = Collection.Starter(_data);
        DeckDefinition starter = _data.Decks[Collection.StarterDeckId];

        Assert.Equal(starter.MainCount, collection.Deck.Count);
        Assert.Equal(starter.MainCount + starter.FusionCount, collection.Total);
        Assert.All(starter.Main, pair => Assert.Equal(pair.Value, collection.Count(pair.Key)));
        Assert.Empty(DeckRules.Problems(collection.ToDefinition(), _data.Cards));
        Deck deck = collection.ToDeck(_data.Cards);
        Assert.Equal(starter.MainCount, deck.Main.Count);
        DuelEngine engine = DuelEngine.Start(deck, _data.Decks["rookie_beatdown"].ToDeck(_data.Cards), new DuelOptions { Seed = 3 });
        Assert.False(engine.State.IsOver);
    }

    [Fact]
    public void BoosterRespectsCountLimitedMaxAndTheLibrary()
    {
        BoosterDefinition pack = _data.Shop.Boosters[0];
        var rng = new DuelRng(11);
        for (int i = 0; i < 200; i++)
        {
            IReadOnlyList<CardDefinition> cards = BoosterDraw.Open(pack, _data.Cards, rng);
            Assert.Equal(pack.Count, cards.Count);
            Assert.All(cards, c => Assert.True(_data.Cards.Contains(c.Id) && c.Limit > 0 && !c.IsToken));
            Assert.True(cards.Count(c => c.Limit == 1) <= pack.LimitedMax);
        }
    }

    [Fact]
    public void BoosterIsReproducibleFromTheSeed()
    {
        BoosterDefinition pack = _data.Shop.Boosters[0];
        string[] first = BoosterDraw.Open(pack, _data.Cards, new DuelRng(5)).Select(c => c.Id).ToArray();
        string[] again = BoosterDraw.Open(pack, _data.Cards, new DuelRng(5)).Select(c => c.Id).ToArray();
        string[] other = BoosterDraw.Open(pack, _data.Cards, new DuelRng(6)).Select(c => c.Id).ToArray();

        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void BoosterTiersFollowTheWeights()
    {
        BoosterDefinition pack = _data.Shop.Boosters[0];
        var rng = new DuelRng(2024);
        var perTier = new Dictionary<int, int>();
        const int packs = 2000;
        for (int i = 0; i < packs; i++)
        {
            foreach (CardDefinition card in BoosterDraw.Open(pack, _data.Cards, rng))
            {
                perTier[card.Tier] = perTier.GetValueOrDefault(card.Tier) + 1;
            }
        }

        double total = packs * pack.Count;
        int weightSum = pack.Weights.Values.Sum();
        foreach ((int tier, int weight) in pack.Weights)
        {
            double expected = weight / (double)weightSum;
            double observed = perTier.GetValueOrDefault(tier) / total;
            Assert.InRange(observed, expected - 0.04, expected + 0.04);
        }
    }

    [Fact]
    public void BoosterCardsJoinTheCollection()
    {
        Collection collection = Collection.Starter(_data);
        int before = collection.Total;
        IReadOnlyList<CardDefinition> cards = BoosterDraw.Open(_data.Shop.Boosters[0], _data.Cards, new DuelRng(9));
        foreach (CardDefinition card in cards)
        {
            collection.Add(card.Id);
        }

        Assert.Equal(before + cards.Count, collection.Total);
        Assert.Equal(_data.Decks[Collection.StarterDeckId].MainCount, collection.Deck.Count);
    }
}
