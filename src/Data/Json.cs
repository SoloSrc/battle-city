using System.IO;
using System.Text.Json;

namespace BattleCity.Data;

/// <summary>Shared JSON settings for <c>data/</c>: snake_case keys (architecture.md §5).</summary>
internal static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static T Parse<T>(string json, string source)
        where T : class
    {
        T? dto;
        try
        {
            dto = JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException e)
        {
            throw new DataException($"{source}: invalid JSON: {e.Message}", e);
        }

        return dto ?? throw new DataException($"{source}: empty document.");
    }

    public static T ParseFile<T>(string path)
        where T : class
    {
        if (!File.Exists(path))
        {
            throw new DataException($"{path}: file not found.");
        }

        return Parse<T>(File.ReadAllText(path), path);
    }
}
