namespace BattleCity.Duel.Core.Commands;

/// <summary>Outcome of <see cref="DuelEngine.Submit"/>: accepted, or rejected with the rule that blocked it.</summary>
public readonly record struct SubmitResult(bool Accepted, string? Error)
{
    public static SubmitResult Ok => new(true, null);

    public static SubmitResult Rejected(string error) => new(false, error);
}
