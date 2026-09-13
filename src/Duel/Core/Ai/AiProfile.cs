namespace BattleCity.Duel.Core.Ai;

/// <summary>Evaluator weights per duelist (systems.md §7). The three slice profiles are the presets.</summary>
public sealed record AiProfile(
    string Name,
    double BoardWeight,
    double CardsWeight,
    double LifeWeight,
    double RiskWeight,
    double Jitter,
    double BluffSet)
{
    public static AiProfile Nico { get; } = new("Nico (aggressive)", 1.2, 0.6, 1.0, 0.0, 0.6, 0.0);

    public static AiProfile Mara { get; } = new("Mara (balanced)", 1.0, 1.0, 0.8, 0.6, 0.3, 0.2);

    public static AiProfile ArcadeOwner { get; } = new("Arcade Owner (control)", 0.8, 1.4, 0.5, 1.0, 0.1, 0.5);
}
