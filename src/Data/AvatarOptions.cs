using System.Collections.Generic;

namespace BattleCity.Data;

/// <summary>Name constraints for the creator (GDD §1.1).</summary>
public sealed record NameRule(string Default, int MinLength, int MaxLength, string Pattern);

/// <summary>Creator defaults as indices into the option lists.</summary>
public sealed record AvatarDefaults(string BodyType, int SkinTone, int HairStyle, int HairColor, int Outfit, int AccentColor);

/// <summary><c>data/avatar.json</c>: the creator's option lists (GDD §1.1). Colours are <c>#rrggbb</c>.</summary>
public sealed record AvatarOptions(
    NameRule Name,
    IReadOnlyList<string> BodyTypes,
    IReadOnlyList<string> SkinTones,
    IReadOnlyDictionary<string, IReadOnlyList<string>> HairStyles,
    IReadOnlyList<string> HairColors,
    IReadOnlyList<string> Outfits,
    IReadOnlyList<string> AccentColors,
    AvatarDefaults Defaults);
