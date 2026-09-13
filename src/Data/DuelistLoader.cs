using System.Collections.Generic;
using System.IO;
using BattleCity.Duel.Core.Ai;

namespace BattleCity.Data;

/// <summary>Reads <c>data/duelists.json</c>; deck ids are checked against the loaded decks.</summary>
public sealed class DuelistLoader
{
    private readonly IReadOnlyDictionary<string, DeckDefinition> _decks;

    public DuelistLoader(IReadOnlyDictionary<string, DeckDefinition> decks)
    {
        _decks = decks ?? throw new System.ArgumentNullException(nameof(decks));
    }

    public IReadOnlyDictionary<string, DuelistDefinition> LoadFile(string path) => Parse(File.ReadAllText(path), path);

    public IReadOnlyDictionary<string, DuelistDefinition> Parse(string json, string source = "duelists")
    {
        RootDto root = Json.Parse<RootDto>(json, source);
        if (root.Duelists is null || root.Duelists.Count == 0)
        {
            throw new DataException($"{source}: 'duelists' must list at least one duelist.");
        }

        var result = new Dictionary<string, DuelistDefinition>(System.StringComparer.Ordinal);
        foreach (DuelistDto d in root.Duelists)
        {
            string where = $"{source} duelist '{d.Id}'";
            if (!Ids.IsSnakeCase(d.Id))
            {
                throw new DataException($"{source}: duelist 'id' must be snake_case (got '{d.Id}').");
            }

            Require(d.Name, "name", where);
            Require(d.Area, "area", where);
            if (d.Deck is null || !_decks.ContainsKey(d.Deck))
            {
                throw new DataException($"{where}: 'deck' '{d.Deck}' is not a loaded deck.");
            }

            string flag = d.RequiredFlag ?? string.Empty;
            if (flag.Length > 0 && !Ids.IsFlag(flag))
            {
                throw new DataException($"{where}: 'required_flag' '{flag}' is not a flag id.");
            }

            if (d.Profile is null)
            {
                throw new DataException($"{where}: 'profile' is required.");
            }

            ProfileDto p = d.Profile;
            if (p.Jitter < 0 || p.BluffSet is < 0 or > 1)
            {
                throw new DataException($"{where}: 'profile.jitter' must be ≥ 0 and 'profile.bluff_set' within 0–1.");
            }

            var profile = new AiProfile(d.Name!, p.Board, p.Cards, p.Life, p.Risk, p.Jitter, p.BluffSet);
            var definition = new DuelistDefinition(
                d.Id!,
                d.Name!,
                d.Area!,
                d.Deck,
                flag,
                profile,
                d.ChallengeLine ?? string.Empty,
                d.WinLine ?? string.Empty,
                d.LoseLine ?? string.Empty,
                ToReward(d.RewardFirst, "reward_first", where),
                ToReward(d.RewardRematch, "reward_rematch", where),
                d.Ending);
            if (!result.TryAdd(definition.Id, definition))
            {
                throw new DataException($"{source}: duplicate duelist id '{definition.Id}'.");
            }
        }

        return result;
    }

    private static void Require(string? value, string field, string where)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DataException($"{where}: '{field}' is required.");
        }
    }

    private static Reward ToReward(RewardDto? dto, string field, string where)
    {
        if (dto is null)
        {
            throw new DataException($"{where}: '{field}' is required.");
        }

        if (dto.Coins < 0 || dto.Boosters < 0)
        {
            throw new DataException($"{where}: '{field}' coins and boosters must be ≥ 0.");
        }

        return new Reward(dto.Coins, dto.Boosters);
    }

    private sealed class RootDto
    {
        public List<DuelistDto>? Duelists { get; set; }
    }

    private sealed class DuelistDto
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Area { get; set; }

        public string? Deck { get; set; }

        public string? RequiredFlag { get; set; }

        public ProfileDto? Profile { get; set; }

        public string? ChallengeLine { get; set; }

        public string? WinLine { get; set; }

        public string? LoseLine { get; set; }

        public RewardDto? RewardFirst { get; set; }

        public RewardDto? RewardRematch { get; set; }

        public bool Ending { get; set; }
    }

    private sealed class ProfileDto
    {
        public double Board { get; set; }

        public double Cards { get; set; }

        public double Life { get; set; }

        public double Risk { get; set; }

        public double Jitter { get; set; }

        public double BluffSet { get; set; }
    }

    private sealed class RewardDto
    {
        public int Coins { get; set; }

        public int Boosters { get; set; }
    }
}
