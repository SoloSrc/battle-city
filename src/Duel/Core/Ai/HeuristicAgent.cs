using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Model;
using BattleCity.Duel.Core.Rng;

namespace BattleCity.Duel.Core.Ai;

/// <summary>
/// Rule-based evaluator (GDD §3.5, systems.md §7): every legal action is
/// applied to a clone of the engine and the resulting state is scored; the
/// best score plus jitter wins. No lookahead beyond the immediate
/// resolution, and the opponent's responses are not simulated.
/// </summary>
public sealed class HeuristicAgent : IDuelAgent
{
    private const double WinScore = 1000.0;

    /// <summary>Unrealised damage counts for less than damage dealt, so attacking beats holding the attack.</summary>
    private const double PotentialDiscount = 0.5;

    private readonly DuelRng _rng;

    public HeuristicAgent(AiProfile profile, ulong seed)
    {
        Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _rng = new DuelRng(seed);
    }

    public AiProfile Profile { get; }

    public PlayerCommand Choose(DuelEngine engine, int player, IReadOnlyList<PlayerCommand> legal)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(legal);
        if (legal.Count == 0)
        {
            throw new ArgumentException("No legal action to choose from.", nameof(legal));
        }

        if (legal.Count == 1)
        {
            return legal[0];
        }

        PlayerCommand best = legal[0];
        double bestScore = double.NegativeInfinity;
        foreach (PlayerCommand command in legal)
        {
            DuelEngine sim = engine.Clone();
            if (!sim.Submit(command).Accepted)
            {
                continue;
            }

            double score = Evaluate(sim.State, player, Profile) + Profile.Jitter * _rng.NextGaussian();
            if (score > bestScore)
            {
                bestScore = score;
                best = command;
            }
        }

        return best;
    }

    /// <summary>The evaluator of systems.md §7 from <paramref name="player"/>'s point of view.</summary>
    public static double Evaluate(DuelState state, int player, AiProfile profile)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(profile);
        if (state.IsOver)
        {
            return state.Winner == player ? WinScore : state.Outcome == DuelOutcome.Draw ? 0.0 : -WinScore;
        }

        PlayerState own = state.Player(player);
        PlayerState opp = state.Opponent(player);

        double board = (own.Monsters.Sum(BoardValue) - opp.Monsters.Sum(BoardValue)) / 1000.0;
        int cards = own.Hand.Count + own.MonsterCount + own.SpellTrapCount - opp.Hand.Count - opp.MonsterCount - opp.SpellTrapCount;
        double life = (own.LifePoints - opp.LifePoints) / 1000.0;
        int exposure = own.Monsters.Count(m => m.IsInAttackPosition);
        double risk = opp.SpellTrapCount * exposure;
        double potential = PotentialDamage(state, player) / 1000.0 * PotentialDiscount;
        // Tie-breaker so discards and sets prefer to keep the strongest monsters in hand.
        double handQuality = own.Hand.Sum(c => c.Def.Monster?.Atk ?? 0) / 100000.0;

        return profile.BoardWeight * board
            + profile.CardsWeight * (cards + handQuality)
            + profile.LifeWeight * (life + potential)
            - profile.RiskWeight * risk;
    }

    /// <summary>Damage the turn player could still deal this turn with the monsters that have not attacked.</summary>
    private static int PotentialDamage(DuelState state, int player)
    {
        if (state.TurnPlayer != player)
        {
            return 0;
        }

        bool battleAhead = state.Phase == Phase.Main1 && !state.BattlePhaseUsed && !state.FirstTurn;
        bool inBattle = state.Phase == Phase.Battle && state.BattleStep is BattleStep.Start or BattleStep.Battle;
        if (!battleAhead && !inBattle)
        {
            return 0;
        }

        PlayerState opp = state.Opponent(player);
        var attackers = state.Player(player).Monsters.Where(m => m.IsInAttackPosition && !m.AttackedThisTurn).ToList();
        if (opp.MonsterCount == 0)
        {
            return attackers.Sum(m => m.Atk);
        }

        var targets = opp.Monsters.Where(m => m.IsInAttackPosition).ToList();
        if (targets.Count == 0)
        {
            return 0;
        }

        int weakest = targets.Min(t => t.Atk);
        return attackers.Sum(m => Math.Max(0, m.Atk - weakest));
    }

    /// <summary>What a monster is worth on the board: its ATK face-up in attack, its DEF in defense; an unknown face-down card counts half its DEF for the owner and nothing for the opponent.</summary>
    private static int BoardValue(CardInstance monster) =>
        monster.Pos switch
        {
            Position.FaceUpAttack => monster.Atk,
            Position.FaceUpDefense => monster.DefValue,
            _ => monster.DefValue / 2,
        };
}
