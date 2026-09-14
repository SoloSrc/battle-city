using System;
using System.Collections.Generic;

namespace BattleCity.Duel.Core.Model;

/// <summary>
/// Immutable printed card (systems.md §5.3). One instance per card id,
/// shared by every <see cref="CardInstance"/> of that card.
/// </summary>
public sealed record CardDefinition(
    string Id,
    string Name,
    CardKind Kind,
    MonsterStats? Monster,
    SpellInfo? Spell,
    TrapInfo? Trap,
    string Text,
    int Limit,
    int Tier,
    IReadOnlyList<string> Effects,
    IReadOnlyList<string> Materials)
{
    /// <summary>A token: created on the field by an effect and removed from the duel when it leaves it.</summary>
    public bool IsToken { get; init; }

    public bool IsMonster => Kind is CardKind.Monster or CardKind.Fusion;

    public bool IsSpell => Kind == CardKind.Spell;

    public bool IsTrap => Kind == CardKind.Trap;

    /// <summary>A vanilla monster: printed stats and nothing else (tier 1).</summary>
    public bool IsVanilla => Kind == CardKind.Monster && Monster?.Category == MonsterCategory.Normal && Effects.Count == 0;

    /// <summary>Builds a vanilla monster definition; the test suites and the data loader share it.</summary>
    public static CardDefinition Vanilla(string id, string name, string type, MonsterAttribute attribute, int level, int atk, int def, string text = "")
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return new CardDefinition(
            id,
            name,
            CardKind.Monster,
            new MonsterStats(type, attribute, level, atk, def, MonsterCategory.Normal),
            null,
            null,
            text,
            3,
            1,
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    /// <summary>Builds a token definition: a Normal monster that exists only on the field.</summary>
    public static CardDefinition Token(string id, string name, string type, MonsterAttribute attribute, int level, int atk, int def) =>
        Vanilla(id, name, type, attribute, level, atk, def) with { IsToken = true, Limit = 0 };

    /// <summary>Builds an Effect (or Flip) monster definition with the given effect ids.</summary>
    public static CardDefinition EffectMonster(string id, string name, string type, MonsterAttribute attribute, int level, int atk, int def, MonsterCategory category, int tier, params string[] effects)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return new CardDefinition(id, name, CardKind.Monster, new MonsterStats(type, attribute, level, atk, def, category), null, null, string.Empty, 3, tier, effects, Array.Empty<string>());
    }

    /// <summary>Builds a Normal Spell definition with the given effect ids.</summary>
    public static CardDefinition NormalSpell(string id, string name, string text, int limit, int tier, params string[] effects)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return SpellCard(id, name, SpellSubtype.Normal, text, limit, tier, effects);
    }

    /// <summary>Builds a Spell definition of any subtype with the given effect ids.</summary>
    public static CardDefinition SpellCard(string id, string name, SpellSubtype subtype, string text, int limit, int tier, params string[] effects)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return new CardDefinition(id, name, CardKind.Spell, null, new SpellInfo(subtype), null, text, limit, tier, effects, Array.Empty<string>());
    }

    /// <summary>Builds a Trap definition with the given effect ids.</summary>
    public static CardDefinition TrapCard(string id, string name, TrapSubtype subtype, string text, int limit, int tier, params string[] effects)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return new CardDefinition(id, name, CardKind.Trap, null, null, new TrapInfo(subtype), text, limit, tier, effects, Array.Empty<string>());
    }

    public override string ToString() => Name;
}
