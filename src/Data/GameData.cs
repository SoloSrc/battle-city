using System;
using System.Collections.Generic;
using System.IO;
using BattleCity.Duel.Core.Data;
using BattleCity.Duel.Core.Effects;

namespace BattleCity.Data;

/// <summary>
/// Everything under <c>data/</c> loaded and cross-checked in dependency
/// order: cards, then decks (ids and limits), duelists (deck ids), shop
/// (stock ids), the avatar options and the tuning numbers. Throws <see cref="DataException"/>
/// or <see cref="CardDataException"/> naming the file and the rule.
/// </summary>
public sealed class GameData
{
    private GameData(CardLibrary cards, IReadOnlyDictionary<string, DeckDefinition> decks, IReadOnlyDictionary<string, DuelistDefinition> duelists, ShopDefinition shop, AvatarOptions avatar, TuningDefinition tuning)
    {
        Cards = cards;
        Decks = decks;
        Duelists = duelists;
        Shop = shop;
        Avatar = avatar;
        Tuning = tuning;
    }

    public CardLibrary Cards { get; }

    public IReadOnlyDictionary<string, DeckDefinition> Decks { get; }

    public IReadOnlyDictionary<string, DuelistDefinition> Duelists { get; }

    public ShopDefinition Shop { get; }

    public AvatarOptions Avatar { get; }

    public TuningDefinition Tuning { get; }

    /// <summary>Loads <paramref name="root"/> (the <c>data/</c> directory).</summary>
    public static GameData Load(string root, EffectRegistry? effects = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        if (!Directory.Exists(root))
        {
            throw new DataException($"{root}: data directory not found.");
        }

        CardLibrary cards = new CardLoader(effects).LoadDirectory(Path.Combine(root, "cards"));
        IReadOnlyDictionary<string, DeckDefinition> decks = new DeckLoader(cards).LoadDirectory(Path.Combine(root, "decks"));
        IReadOnlyDictionary<string, DuelistDefinition> duelists = new DuelistLoader(decks).LoadFile(Path.Combine(root, "duelists.json"));
        ShopDefinition shop = new ShopLoader(cards).LoadFile(Path.Combine(root, "shop.json"));
        AvatarOptions avatar = new AvatarLoader().LoadFile(Path.Combine(root, "avatar.json"));
        TuningDefinition tuning = new TuningLoader().LoadFile(Path.Combine(root, "tuning.json"));
        return new GameData(cards, decks, duelists, shop, avatar, tuning);
    }
}
