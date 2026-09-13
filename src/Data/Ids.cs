using System.Text.RegularExpressions;

namespace BattleCity.Data;

/// <summary>Id conventions (architecture.md §5): snake_case ids, <c>defeated:&lt;duelist&gt;</c> flags.</summary>
internal static partial class Ids
{
    public static bool IsSnakeCase(string? value) => value is not null && SnakeCase().IsMatch(value);

    public static bool IsFlag(string? value) => value is not null && Flag().IsMatch(value);

    [GeneratedRegex("^[a-z0-9_]+$")]
    private static partial Regex SnakeCase();

    [GeneratedRegex("^[a-z0-9_]+(:[a-z0-9_]+)?$")]
    private static partial Regex Flag();
}
