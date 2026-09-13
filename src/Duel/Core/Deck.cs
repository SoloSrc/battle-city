using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core;

/// <summary>A deck list handed to <see cref="DuelEngine.Start"/>: main deck in list order, plus the Fusion Deck.</summary>
public sealed record Deck(IReadOnlyList<CardDefinition> Main, IReadOnlyList<CardDefinition> Fusion)
{
    public Deck(IReadOnlyList<CardDefinition> main)
        : this(main, Array.Empty<CardDefinition>())
    {
    }

    /// <summary>A main deck made of the given definitions repeated <paramref name="copies"/> times each.</summary>
    public static Deck Repeat(IEnumerable<CardDefinition> definitions, int copies)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        return new Deck(definitions.SelectMany(d => Enumerable.Repeat(d, copies)).ToList());
    }
}
