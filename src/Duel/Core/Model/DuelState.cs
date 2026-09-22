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
    public DuelState(DuelRng rng, int startingLifePoints = DuelCoreInfo.StartingLifePoints)
    {
        Rng = rng ?? throw new ArgumentNullException(nameof(rng));
        Players = new[] { new PlayerState(0, startingLifePoints), new PlayerState(1, startingLifePoints) };
        Chain = new List<ChainLink>();
        Triggers = new List<PendingTrigger>();
        Modifiers = new List<Modifier>();
    }

    private DuelState(DuelState other)
    {
        Rng = other.Rng.Clone();
        var copies = new Dictionary<Guid, CardInstance>();
        Players = other.Players.Select(p => p.Clone(copies)).ToArray();
        Chain = other.Chain.Select(l => l.Clone(copies[l.Source.Id])).ToList();
        Triggers = other.Triggers.ToList();
        Modifiers = other.Modifiers.ToList();
        PendingLink = other.PendingLink?.Clone(copies[other.PendingLink.Source.Id]);
        ResolvingLink = other.ResolvingLink?.Clone(copies[other.ResolvingLink.Source.Id]);
        PendingChoice = other.PendingChoice;
        TurnPlayer = other.TurnPlayer;
        TurnNumber = other.TurnNumber;
        Phase = other.Phase;
        BattleStep = other.BattleStep;
        DamageSubstep = other.DamageSubstep;
        Window = other.Window;
        WindowCard = other.WindowCard;
        LastSummon = other.LastSummon;
        Priority = other.Priority;
        ConsecutivePasses = other.ConsecutivePasses;
        NormalSummonUsed = other.NormalSummonUsed;
        BattlePhaseUsed = other.BattlePhaseUsed;
        Attacker = other.Attacker;
        AttackerStay = other.AttackerStay;
        AttackTarget = other.AttackTarget;
        AttackTargetStay = other.AttackTargetStay;
        BattleFlipped = other.BattleFlipped;
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

    /// <summary>The response window the duel is in (systems.md §5.5).</summary>
    public Window Window { get; set; }

    /// <summary>The monster a <see cref="Model.Window.Summon"/> window is about.</summary>
    public Guid? WindowCard { get; set; }

    /// <summary>How <see cref="WindowCard"/> was Summoned (Trap Hole answers Normal and Flip Summons only).</summary>
    public SummonKind? LastSummon { get; set; }

    /// <summary>Chain links in activation order; resolves LIFO.</summary>
    public List<ChainLink> Chain { get; }

    /// <summary>Trigger effects whose window fired and that are not on the chain yet, in the order they fired.</summary>
    public List<PendingTrigger> Triggers { get; }

    /// <summary>Modifiers registered by resolved effects (systems.md §5.4); those from Continuous effects and equips are recomputed instead of stored.</summary>
    public List<Modifier> Modifiers { get; }

    /// <summary>An activation collecting its cost and target answers before it joins the chain.</summary>
    public ChainLink? PendingLink { get; set; }

    /// <summary>A chain link whose resolution stopped to ask its player something; it resumes when the answer arrives, then the rest of the chain resolves.</summary>
    public ChainLink? ResolvingLink { get; set; }

    /// <summary>The question the engine is waiting on; while set, only <c>AnswerChoice</c> from its player is legal.</summary>
    public PendingChoice? PendingChoice { get; set; }

    /// <summary>The player who may act now.</summary>
    public int Priority { get; set; }

    /// <summary>Consecutive priority passes; two in a row resolve the chain or close the window.</summary>
    public int ConsecutivePasses { get; set; }

    public bool NormalSummonUsed { get; set; }

    /// <summary>The turn player entered the Battle Phase this turn (it cannot be entered twice).</summary>
    public bool BattlePhaseUsed { get; set; }

    /// <summary>Attacking monster from the attack declaration to the end of the Damage Step.</summary>
    public Guid? Attacker { get; set; }

    /// <summary>The attacker's <see cref="CardInstance.FieldStay"/> at the declaration: the attack belongs to that stay, and ends when it does.</summary>
    public int AttackerStay { get; set; }

    /// <summary>Attack target; null for a direct attack.</summary>
    public Guid? AttackTarget { get; set; }

    /// <summary>The target's <see cref="CardInstance.FieldStay"/> at the declaration.</summary>
    public int AttackTargetStay { get; set; }

    /// <summary>The declared attacker while it is still the monster that declared: null once it left the field, whether or not it came back.</summary>
    public CardInstance? AttackingMonster => Attacker is { } id ? InMonsterZoneSince(id, AttackerStay) : null;

    /// <summary>The declared target while it is still the monster that was attacked; null for a direct attack or once it left the field.</summary>
    public CardInstance? AttackedMonster => AttackTarget is { } id ? InMonsterZoneSince(id, AttackTargetStay) : null;

    /// <summary>The card with this id if it is in a Monster Zone in the stay that was recorded, so a returned card does not pass for the one that left.</summary>
    public CardInstance? InMonsterZoneSince(Guid id, int stay) =>
        Find(id) is { Loc: Location.MonsterZone } card && card.FieldStay == stay ? card : null;

    /// <summary>The attack target flipped face-up by this Damage Step; its flip effect fires after damage calculation.</summary>
    public Guid? BattleFlipped { get; set; }

    public int? Winner { get; set; }

    public DuelOutcome Outcome { get; set; }

    public bool IsOver => Outcome != DuelOutcome.None;

    public bool FirstTurn => TurnNumber == 1;

    /// <summary>A pending choice or an activation in progress: no other command is accepted.</summary>
    public bool IsPaused => PendingChoice is not null || PendingLink is not null || ResolvingLink is not null;

    public PlayerState Player(int index) => Players[index];

    public PlayerState Opponent(int index) => Players[1 - index];

    public PlayerState Current => Players[TurnPlayer];

    /// <summary>Finds a card anywhere on either side; null when the id is unknown.</summary>
    public CardInstance? Find(Guid id) => Players.SelectMany(p => p.AllCards).FirstOrDefault(c => c.Id == id);

    /// <summary>The number of <paramref name="player"/>'s next turn: "until the end of your next turn" expires after it.</summary>
    public int NextTurnOf(int player) => TurnPlayer == player ? TurnNumber + 2 : TurnNumber + 1;

    public DuelState Clone() => new(this);
}
