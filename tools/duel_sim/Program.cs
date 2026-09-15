using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BattleCity.Data;
using BattleCity.Duel.Core;
using BattleCity.Duel.Core.Ai;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Tools.DuelSim;

/// <summary>
/// Plays seeded matchups between the duelist profiles of <c>data/duelists.json</c>,
/// the decks of <c>data/decks/</c> and a random agent, and prints a win-rate
/// table (systems.md §7, issue #59). <c>--check</c> turns the GDD targets
/// into an exit code.
/// </summary>
internal static class Program
{
    private const string Usage = """
        usage: duel_sim [--data <dir>] [--games <n>] [--seed <n>] [--check] [--matchup "<agent>:<deck> vs <agent>:<deck>"]...

        agents: a duelist id or name from data/duelists.json (nico, mara, arcade_owner) or "random"
        decks:  a deck id from data/decks/ (starter, rookie_beatdown, warrior_toolbox, goat_control, beatdown)
        Without --matchup the acceptance matrix of issue #59 is played. --check exits 1 when a GDD target is missed.
        """;

    private static int Main(string[] args)
    {
        string? dataDir = null;
        int games = 20;
        ulong seed = 1;
        bool check = false;
        var specs = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--data":
                    dataDir = Next(args, ref i);
                    break;
                case "--games":
                    games = int.Parse(Next(args, ref i), CultureInfo.InvariantCulture);
                    break;
                case "--seed":
                    seed = ulong.Parse(Next(args, ref i), CultureInfo.InvariantCulture);
                    break;
                case "--check":
                    check = true;
                    break;
                case "--matchup":
                    specs.Add(Next(args, ref i));
                    break;
                case "--help":
                case "-h":
                    Console.WriteLine(Usage);
                    return 0;
                default:
                    Console.Error.WriteLine($"unknown argument '{args[i]}'");
                    Console.Error.WriteLine(Usage);
                    return 2;
            }
        }

        dataDir ??= FindDataDirectory();
        if (dataDir is null)
        {
            Console.Error.WriteLine("data/ not found; pass --data <dir>");
            return 2;
        }

        GameData data = GameData.Load(dataDir);
        var agents = new Dictionary<string, AiProfile?>(StringComparer.OrdinalIgnoreCase) { ["random"] = null };
        foreach (DuelistDefinition duelist in data.Duelists.Values)
        {
            agents[duelist.Id] = duelist.Profile;
            agents[Slug(duelist.Name)] = duelist.Profile;
        }

        List<Matchup> matchups;
        try
        {
            matchups = (specs.Count > 0 ? specs : Acceptance.Matchups).Select(spec => Matchup.Parse(spec, agents, data)).ToList();
        }
        catch (ArgumentException e)
        {
            Console.Error.WriteLine(e.Message);
            return 2;
        }

        var results = new List<Result>();
        foreach (Matchup matchup in matchups)
        {
            results.Add(Play(matchup, games, seed));
        }

        Console.WriteLine($"duel_sim: {games} games per matchup, seeds from {seed}, data {Path.GetFullPath(dataDir)}");
        Console.WriteLine();
        Console.WriteLine("| Matchup | A wins | B wins | Draws | A win rate | Avg turns |");
        Console.WriteLine("| --- | --- | --- | --- | --- | --- |");
        foreach (Result r in results)
        {
            Console.WriteLine(FormattableString.Invariant($"| {r.Matchup} | {r.WinsA} | {r.WinsB} | {r.Draws} | {r.WinRateA:P0} | {r.AverageTurns:F1} |"));
        }

        if (!check)
        {
            return 0;
        }

        Console.WriteLine();
        bool ok = true;
        foreach ((string description, Func<IReadOnlyList<Result>, bool> holds) in Acceptance.Targets)
        {
            bool met = holds(results);
            ok &= met;
            Console.WriteLine($"{(met ? "PASS" : "FAIL")}: {description}");
        }

        return ok ? 0 : 1;
    }

    private static Result Play(Matchup matchup, int games, ulong seed)
    {
        var outcomes = new Outcome[games];
        Parallel.For(0, games, i =>
        {
            ulong gameSeed = seed + (ulong)i;
            // Sides alternate so a fixed player index does not favour either agent.
            bool swap = i % 2 == 1;
            Side first = swap ? matchup.B : matchup.A;
            Side second = swap ? matchup.A : matchup.B;
            DuelEngine engine = DuelEngine.Start(first.Deck, second.Deck, new DuelOptions { Seed = gameSeed });
            DuelRunner.Play(engine, first.CreateAgent(gameSeed), second.CreateAgent(gameSeed + 1000));
            DuelState s = engine.State;
            int? winner = s.IsOver && s.Winner is { } w ? (swap ? 1 - w : w) : null;
            outcomes[i] = new Outcome(winner, s.TurnNumber);
        });

        return new Result(
            matchup.ToString(),
            outcomes.Count(o => o.Winner == 0),
            outcomes.Count(o => o.Winner == 1),
            outcomes.Count(o => o.Winner is null),
            outcomes.Average(o => o.Turns));
    }

    private static string Next(string[] args, ref int i)
    {
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException($"'{args[i]}' needs a value");
        }

        return args[++i];
    }

    /// <summary>"The Arcade Owner" → "arcade_owner".</summary>
    private static string Slug(string name)
    {
        string slug = string.Join('_', name.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w != "the"));
        return slug;
    }

    private static string? FindDataDirectory()
    {
        for (DirectoryInfo? dir = new(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, "duelists.json")))
            {
                return candidate;
            }
        }

        return null;
    }

    private readonly record struct Outcome(int? Winner, int Turns);
}

/// <summary>One side of a matchup: a profile (null for the random agent) and a deck.</summary>
internal sealed record Side(string AgentName, AiProfile? Profile, string DeckId, Deck Deck)
{
    public IDuelAgent CreateAgent(ulong seed) => Profile is null ? new RandomAgent(seed) : new HeuristicAgent(Profile, seed);

    public override string ToString() => $"{AgentName}:{DeckId}";
}

internal sealed record Matchup(Side A, Side B)
{
    public static Matchup Parse(string spec, IReadOnlyDictionary<string, AiProfile?> agents, GameData data)
    {
        string[] sides = spec.Split(" vs ", StringSplitOptions.TrimEntries);
        if (sides.Length != 2)
        {
            throw new ArgumentException($"matchup '{spec}' must read '<agent>:<deck> vs <agent>:<deck>'");
        }

        return new Matchup(ParseSide(sides[0], agents, data), ParseSide(sides[1], agents, data));
    }

    public override string ToString() => $"{A} vs {B}";

    private static Side ParseSide(string spec, IReadOnlyDictionary<string, AiProfile?> agents, GameData data)
    {
        string[] parts = spec.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw new ArgumentException($"side '{spec}' must read '<agent>:<deck>'");
        }

        if (!agents.TryGetValue(parts[0], out AiProfile? profile))
        {
            throw new ArgumentException($"unknown agent '{parts[0]}'; known: {string.Join(", ", agents.Keys.OrderBy(k => k, StringComparer.Ordinal))}");
        }

        if (!data.Decks.TryGetValue(parts[1], out DeckDefinition? deck))
        {
            throw new ArgumentException($"unknown deck '{parts[1]}'; known: {string.Join(", ", data.Decks.Keys.OrderBy(k => k, StringComparer.Ordinal))}");
        }

        return new Side(parts[0].ToLowerInvariant(), profile, deck.Id, deck.ToDeck(data.Cards));
    }
}

internal sealed record Result(string Matchup, int WinsA, int WinsB, int Draws, double AverageTurns)
{
    public double WinRateA => WinsA + WinsB == 0 ? 0.0 : (double)WinsA / (WinsA + WinsB);
}

/// <summary>The acceptance matrix and the GDD §3.5 targets of issue #59.</summary>
internal static class Acceptance
{
    public static readonly IReadOnlyList<string> Matchups = new[]
    {
        "nico:rookie_beatdown vs mara:starter",
        "nico:rookie_beatdown vs random:starter",
        "mara:warrior_toolbox vs random:starter",
        "arcade_owner:goat_control vs random:starter",
        "nico:starter vs mara:starter",
        "mara:starter vs arcade_owner:starter",
        "nico:warrior_toolbox vs mara:warrior_toolbox",
        "mara:warrior_toolbox vs arcade_owner:warrior_toolbox",
        "nico:goat_control vs mara:goat_control",
        "mara:goat_control vs arcade_owner:goat_control",
    };

    public static readonly IReadOnlyList<(string Description, Func<IReadOnlyList<Result>, bool> Holds)> Targets = new (string, Func<IReadOnlyList<Result>, bool>)[]
    {
        ("Nico with Rookie Beatdown wins at most 20% against Mara-level play with the starter", r => Rate(r, "nico:rookie_beatdown vs mara:starter") <= 0.20),
        ("Nico with Rookie Beatdown wins at least 90% against random play with the starter", r => Rate(r, "nico:rookie_beatdown vs random:starter") >= 0.90),
        ("Mara and the Arcade Owner beat random play with their own decks at least 90% of the time", r => Rate(r, "mara:warrior_toolbox vs random:starter") >= 0.90 && Rate(r, "arcade_owner:goat_control vs random:starter") >= 0.90),
        ("Nico < Mara in mirror matches on every deck", r => Mirrors(r, "nico", "mara").All(rate => rate < 0.5)),
        ("Mara < Arcade Owner in mirror matches on every deck", r => Mirrors(r, "mara", "arcade_owner").All(rate => rate < 0.5)),
    };

    private static double Rate(IReadOnlyList<Result> results, string matchup) =>
        results.FirstOrDefault(r => r.Matchup == matchup)?.WinRateA ?? double.NaN;

    private static IEnumerable<double> Mirrors(IReadOnlyList<Result> results, string weaker, string stronger) =>
        results.Where(r => r.Matchup.StartsWith(weaker + ":", StringComparison.Ordinal) && r.Matchup.Contains(" vs " + stronger + ":", StringComparison.Ordinal)
            && r.Matchup.Split(':')[1].Split(' ')[0] == r.Matchup.Split(':')[2])
            .Select(r => r.WinRateA);
}
