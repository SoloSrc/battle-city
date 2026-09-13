using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Rng;

namespace BattleCity.Duel.Core.Model;

/// <summary>
/// The whole duel (systems.md §5.2). Player 0 is the human side by
/// convention, player 1 the opponent; the engine itself is symmetric.
/// </summary>
public sealed class DuelState
{
    public DuelState(DuelRng rng)
    {
        Rng = rng ?? throw new ArgumentNullException(nameof(rng));
        Players = new[] { new PlayerState(0), new PlayerState(1) };
        Chain = new List<ChainLink>();
    }

    private DuelState(DuelState other)
    {
        Rng = other.Rng.Clone();
        var copies = new Dictionary<Guid, CardInstance>();
        Players = other.Players.Select(p => p.Clone(copies)).ToArray();
        Chain = other.Chain.Select(l => new ChainLink(l.Index, l.Player, copies[l.Source.Id], l.Effect)).ToList();
        TurnPlayer = other.TurnPlayer;
        TurnNumber = other.TurnNumber;
        Phase = other.Phase;
        BattleStep = other.BattleStep;
        DamageSubstep = other.DamageSubstep;
        Priority = other.Priority;
        ConsecutivePasses = other.ConsecutivePasses;
        NormalSummonUsed = other.NormalSummonUsed;
        BattlePhaseUsed = other.BattlePhaseUsed;
        Attacker = other.Attacker;
        AttackTarget = other.AttackTarget;
        Winner = other.Winner;
        Outcome = other.Outcome;
    }

    public DuelRng Rng { get; }

    public PlayerState[] Players { get; }

    public int TurnPlayer { get; set; }

    /// <summary>1-based; turn 1 belongs to the player who won the coin flip.</summary>
    public int TurnNumber { get; set; }

    public Phase Phase { get; set; }

    public BattleStep BattleStep { get; set; }

    public DamageSubstep DamageSubstep { get; set; }

    /// <summary>Chain links in activation order; resolves LIFO.</summary>
    public List<ChainLink> Chain { get; }

    /// <summary>The player who may act now.</summary>
    public int Priority { get; set; }

    /// <summary>Consecutive priority passes; two in a row resolve the chain, run a pending attack or advance the phase.</summary>
    public int ConsecutivePasses { get; set; }

    public bool NormalSummonUsed { get; set; }

    /// <summary>The turn player entered the Battle Phase this turn (it cannot be entered twice).</summary>
    public bool BattlePhaseUsed { get; set; }

    /// <summary>Attacking monster while an attack is being resolved.</summary>
    public Guid? Attacker { get; set; }

    /// <summary>Attack target; null for a direct attack.</summary>
    public Guid? AttackTarget { get; set; }

    public int? Winner { get; set; }

    public DuelOutcome Outcome { get; set; }

    public bool IsOver => Outcome != DuelOutcome.None;

    public bool FirstTurn => TurnNumber == 1;

    public PlayerState Player(int index) => Players[index];

    public PlayerState Opponent(int index) => Players[1 - index];

    public PlayerState Current => Players[TurnPlayer];

    /// <summary>Finds a card anywhere on either side; null when the id is unknown.</summary>
    public CardInstance? Find(Guid id) => Players.SelectMany(p => p.AllCards).FirstOrDefault(c => c.Id == id);

    public DuelState Clone() => new(this);
}
