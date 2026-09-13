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

    /// <summary>Builds a Normal Spell definition with the given effect ids.</summary>
    public static CardDefinition NormalSpell(string id, string name, string text, int limit, int tier, params string[] effects)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return new CardDefinition(id, name, CardKind.Spell, null, new SpellInfo(SpellSubtype.Normal), null, text, limit, tier, effects, Array.Empty<string>());
    }

    public override string ToString() => Name;
}
