using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects.Cards;

/// <summary>Queries the card effects share: what is on the field, in a hand, Deck or Graveyard, and a tie-break question for "the monster with the lowest ATK" wordings.</summary>
internal static class Field
{
    public static IEnumerable<CardInstance> Monsters(DuelState s) => s.Players.SelectMany(p => p.Monsters);

    public static IEnumerable<CardInstance> SpellTraps(DuelState s) =>
        s.Players.SelectMany(p => p.SpellTraps.Concat(p.FieldZone is null ? Enumerable.Empty<CardInstance>() : new[] { p.FieldZone }));

    public static IEnumerable<CardInstance> FaceUpMonsters(PlayerState p) => p.Monsters.Where(m => m.IsFaceUp);

    public static IReadOnlyList<Guid> Ids(IEnumerable<CardInstance> cards) => cards.Select(c => c.Id).ToList();

    /// <summary>The card with <paramref name="id"/> if it is still on the field; targets that left the field are not affected.</summary>
    public static CardInstance? OnField(DuelState s, Guid id) => s.Find(id) is { IsOnField: true } card ? card : null;

    /// <summary>The Spell or Trap cards in <paramref name="p"/>'s hand that can be Set in a Spell &amp; Trap Zone.</summary>
    public static IEnumerable<CardInstance> SettableInHand(PlayerState p) =>
        p.Hand.Where(c => (c.Def.IsSpell || c.Def.IsTrap) && c.Def.Spell?.Subtype != SpellSubtype.Field);

    public static bool IsWarrior(CardInstance card) => card.Def.Monster?.Type == "Warrior";

    public static bool IsGravekeeper(CardInstance card) => card.IsMonster && card.Def.Name.StartsWith("Gravekeeper's", StringComparison.Ordinal);

    /// <summary>
    /// The monsters sharing the extreme of <paramref name="key"/> among <paramref name="candidates"/>
    /// ("the face-up monster with the lowest ATK"): one card, or several that tie.
    /// </summary>
    public static List<CardInstance> Extremes(IEnumerable<CardInstance> candidates, Func<CardInstance, int> key, bool highest)
    {
        var list = candidates.ToList();
        if (list.Count == 0)
        {
            return list;
        }

        int extreme = highest ? list.Max(key) : list.Min(key);
        return list.Where(c => key(c) == extreme).ToList();
    }

    /// <summary>
    /// Picks one of <paramref name="candidates"/> for a link that resolves: a single candidate needs no
    /// question, a tie is put to the link's player. Null when there is no candidate, or when the
    /// question was just asked and the effect must return (see <see cref="DuelEngine.Ask(ChainLink, Choice)"/>).
    /// </summary>
    public static CardInstance? PickOne(DuelEngine engine, ChainLink link, IReadOnlyList<CardInstance> candidates, string prompt)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        IReadOnlyList<Guid>? answer = engine.Ask(link, new Choice(prompt, Ids(candidates), 1, 1));
        return answer is null ? null : engine.State.Find(answer[0]);
    }
}
