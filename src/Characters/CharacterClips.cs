using System.Collections.Generic;

namespace BattleCity.Characters;

/// <summary>
/// Clip names from asset-list.md §2.9 and the states that play them. Until a
/// clip is delivered, <see cref="Fallbacks"/> says which delivered clip stands
/// in, so the AnimationTree never references a missing animation.
/// </summary>
public static class CharacterClips
{
    public const string Idle = "idle";
    public const string Walk = "walk";
    public const string Run = "run";
    public const string TurnLeft = "turn_l";
    public const string TurnRight = "turn_r";
    public const string Talk = "talk";
    public const string DuelReady = "duel_ready";
    public const string DrawCard = "draw_card";
    public const string PlayCard = "play_card";
    public const string CardToGrave = "card_to_grave";
    public const string TakeDamage = "take_damage";
    public const string Win = "win";
    public const string Lose = "lose";
    public const string DuelIdle = "duel_idle";

    /// <summary>Clips the AnimationTree in Character.tscn references.</summary>
    public static readonly string[] Required =
    {
        Idle, Walk, Run, Talk, DuelReady, DrawCard, PlayCard, CardToGrave, TakeDamage, Win, Lose, DuelIdle,
    };

    /// <summary>Stand-in clip for each missing one, resolved in order until a delivered clip is found.</summary>
    public static readonly IReadOnlyDictionary<string, string> Fallbacks = new Dictionary<string, string>
    {
        [Run] = Walk,
        [Talk] = Idle,
        [DuelReady] = Idle,
        [DuelIdle] = Idle,
        [DrawCard] = DuelIdle,
        [PlayCard] = DuelIdle,
        [CardToGrave] = DuelIdle,
        [TakeDamage] = DuelIdle,
        [Win] = DuelIdle,
        [Lose] = DuelIdle,
        [Walk] = Idle,
    };
}
