using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace BattleCity.Data;

/// <summary>Reads <c>data/avatar.json</c> and checks that every default points at an existing option.</summary>
public sealed partial class AvatarLoader
{
    public AvatarOptions LoadFile(string path) => Parse(File.ReadAllText(path), path);

    public AvatarOptions Parse(string json, string source = "avatar")
    {
        AvatarDto dto = Json.Parse<AvatarDto>(json, source);
        if (dto.Name is null || string.IsNullOrEmpty(dto.Name.Default) || dto.Name.MinLength < 1 || dto.Name.MaxLength < dto.Name.MinLength || string.IsNullOrEmpty(dto.Name.Pattern))
        {
            throw new DataException($"{source}: 'name' needs default, min_length ≥ 1, max_length ≥ min_length and a pattern.");
        }

        try
        {
            if (!Regex.IsMatch(dto.Name.Default, dto.Name.Pattern))
            {
                throw new DataException($"{source}: the default name '{dto.Name.Default}' does not match its own pattern.");
            }
        }
        catch (RegexParseException e)
        {
            throw new DataException($"{source}: 'name.pattern' is not a valid regular expression.", e);
        }

        List<string> bodyTypes = NonEmpty(dto.BodyTypes, "body_types", source);
        List<string> skinTones = Colours(dto.SkinTones, "skin_tones", source);
        List<string> hairColors = Colours(dto.HairColors, "hair_colors", source);
        List<string> accentColors = Colours(dto.AccentColors, "accent_colors", source);
        List<string> outfits = NonEmpty(dto.Outfits, "outfits", source);

        var hairStyles = new Dictionary<string, IReadOnlyList<string>>(System.StringComparer.Ordinal);
        foreach (string body in bodyTypes)
        {
            if (dto.HairStyles is null || !dto.HairStyles.TryGetValue(body, out List<string>? styles) || styles.Count == 0)
            {
                throw new DataException($"{source}: 'hair_styles' needs at least one style for body type '{body}'.");
            }

            hairStyles[body] = styles;
        }

        AvatarDefaultsDto d = dto.Defaults ?? throw new DataException($"{source}: 'defaults' is required.");
        if (d.BodyType is null || !bodyTypes.Contains(d.BodyType))
        {
            throw new DataException($"{source}: default body type '{d.BodyType}' is not in 'body_types'.");
        }

        Index(d.SkinTone, skinTones.Count, "skin_tone", source);
        Index(d.HairStyle, hairStyles[d.BodyType].Count, "hair_style", source);
        Index(d.HairColor, hairColors.Count, "hair_color", source);
        Index(d.Outfit, outfits.Count, "outfit", source);
        Index(d.AccentColor, accentColors.Count, "accent_color", source);

        return new AvatarOptions(
            new NameRule(dto.Name.Default, dto.Name.MinLength, dto.Name.MaxLength, dto.Name.Pattern),
            bodyTypes,
            skinTones,
            hairStyles,
            hairColors,
            outfits,
            accentColors,
            new AvatarDefaults(d.BodyType, d.SkinTone, d.HairStyle, d.HairColor, d.Outfit, d.AccentColor));
    }

    private static List<string> NonEmpty(List<string>? values, string field, string source)
    {
        if (values is null || values.Count == 0 || values.Any(string.IsNullOrWhiteSpace))
        {
            throw new DataException($"{source}: '{field}' must be a non-empty list of ids.");
        }

        return values;
    }

    private static List<string> Colours(List<string>? values, string field, string source)
    {
        List<string> list = NonEmpty(values, field, source);
        foreach (string colour in list)
        {
            if (!HexColour().IsMatch(colour))
            {
                throw new DataException($"{source}: '{field}' entry '{colour}' is not a #rrggbb colour.");
            }
        }

        return list;
    }

    private static void Index(int value, int count, string field, string source)
    {
        if (value < 0 || value >= count)
        {
            throw new DataException($"{source}: default '{field}' {value} is out of range (0–{count - 1}).");
        }
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColour();

    private sealed class AvatarDto
    {
        public NameDto? Name { get; set; }

        public List<string>? BodyTypes { get; set; }

        public List<string>? SkinTones { get; set; }

        public Dictionary<string, List<string>>? HairStyles { get; set; }

        public List<string>? HairColors { get; set; }

        public List<string>? Outfits { get; set; }

        public List<string>? AccentColors { get; set; }

        public AvatarDefaultsDto? Defaults { get; set; }
    }

    private sealed class NameDto
    {
        public string? Default { get; set; }

        public int MinLength { get; set; }

        public int MaxLength { get; set; }

        public string? Pattern { get; set; }
    }

    private sealed class AvatarDefaultsDto
    {
        public string? BodyType { get; set; }

        public int SkinTone { get; set; }

        public int HairStyle { get; set; }

        public int HairColor { get; set; }

        public int Outfit { get; set; }

        public int AccentColor { get; set; }
    }
}
