using System.Text.RegularExpressions;

namespace BattleCity.Core;

/// <summary>
/// Progression flag names (systems.md §3.4, §8). <c>defeated:&lt;duelistId&gt;</c>
/// is set by the encounter system on a first victory; the rest are named here.
/// </summary>
public static partial class Flags
{
    public const string DefeatedPrefix = "defeated:";
    public const string TutorialDone = "tutorial_done";
    public const string EndingSeen = "ending_seen";

    public static string Defeated(string duelistId) => DefeatedPrefix + duelistId;

    /// <summary>True for a flag the game can set; the level checklist uses the same grammar.</summary>
    public static bool IsKnown(string flag)
    {
        return flag is TutorialDone or EndingSeen || DefeatedPattern().IsMatch(flag);
    }

    [GeneratedRegex("^defeated:[a-z][a-z0-9_]*$")]
    private static partial Regex DefeatedPattern();
}
