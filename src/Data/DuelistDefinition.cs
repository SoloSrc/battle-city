using BattleCity.Duel.Core.Ai;

namespace BattleCity.Data;

/// <summary>Coins and boosters granted after a duel (systems.md §8).</summary>
public sealed record Reward(int Coins, int Boosters);

/// <summary>One entry of <c>data/duelists.json</c> (GDD §3.6, systems.md §7).</summary>
public sealed record DuelistDefinition(
    string Id,
    string Name,
    string Area,
    string DeckId,
    string RequiredFlag,
    AiProfile Profile,
    string ChallengeLine,
    string WinLine,
    string LoseLine,
    Reward RewardFirst,
    Reward RewardRematch,
    bool Ending);
