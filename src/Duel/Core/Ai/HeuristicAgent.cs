using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Effects;
using BattleCity.Duel.Core.Model;
using BattleCity.Duel.Core.Rng;

namespace BattleCity.Duel.Core.Ai;

/// <summary>
/// Rule-based evaluator (GDD §3.5, systems.md §7): every legal action is
/// applied to a clone of the engine, the clone is rolled out until the
/// chain and the open window have resolved (both players passing, the
/// agent's own prompts answered by the same evaluator, the opponent's by a
/// fixed policy), and the resulting state is scored; the best score plus
/// jitter wins. Responses go through the response table, prompts through
/// <see cref="Answer"/>. No lookahead beyond the immediate resolution: the
/// opponent's responses are not simulated.
/// </summary>
public sealed class HeuristicAgent : IDuelAgent
{
    private const double WinScore = 1000.0;

    /// <summary>Unrealised damage counts for less than damage dealt, so attacking beats holding the attack.</summary>
    private const double PotentialDiscount = 0.5;

    /// <summary>A Set Trap or Quick-Play Spell is worth this much more than the same card in the hand: it can be activated when it matters.</summary>
    private const double ReadinessBonus = 0.25;

    /// <summary>A Set Normal or Equip Spell is worth this much less than in the hand: it gained nothing and became a target. <see cref="AiProfile.BluffSet"/> overrides it.</summary>
    private const double DecoyPenalty = 0.1;

    /// <summary>What the opponent's face-down monster is assumed to be worth; the agent does not peek.</summary>
    private const int UnknownMonsterValue = 700;

    /// <summary>The share of Set cards assumed to answer an attack (Sakuretsu Armor, Mirror Force, Ring of Destruction and the like in the slice's decks).</summary>
    private const double BattleTrapShare = 0.35;

    /// <summary>A Spell or Trap is worth this much ATK when the agent has to give one up.</summary>
    private const int SpellTrapValue = 1500;

    /// <summary>A response with no entry in the response table still fires when it gains at least this much score: about one card.</summary>
    private const double UnlistedResponseGain = 1.0;

    /// <summary>Damage a response must prevent to enter the response table.</summary>
    private const int DamageWorthPreventing = 1000;

    /// <summary>Prompts with more legal answers than this are answered by the static policy instead of the evaluator.</summary>
    private const int MaxBranching = 48;

    /// <summary>How many of the agent's own prompts a rollout may branch on before falling back to the static policy.</summary>
    private const int BranchDepth = 2;

    /// <summary>Commands a rollout may submit before giving up on reaching a quiet state.</summary>
    private const int RolloutBudget = 200;

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

        DuelState s = engine.State;
        if (s.PendingChoice is not null)
        {
            return Answer(engine, player, legal);
        }

        if (IsResponseSituation(s, player))
        {
            return Respond(engine, player, legal);
        }

        return Act(engine, player, legal);
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

        double board = (own.Monsters.Sum(m => BoardValue(m, known: true)) - opp.Monsters.Sum(m => BoardValue(m, known: false))) / 1000.0;
        int cards = own.Hand.Count + own.MonsterCount + own.SpellTrapCount - opp.Hand.Count - opp.MonsterCount - opp.SpellTrapCount;
        double readiness = own.SpellTraps.Where(c => c.IsFaceDown).Sum(c => IsResponseCard(c) ? ReadinessBonus : -DecoyPenalty);
        double life = (own.LifePoints - opp.LifePoints) / 1000.0;
        double risk = AttackRisk(state, player);
        double potential = PotentialDamage(state, player) / 1000.0 * PotentialDiscount;
        // Tie-breaker so discards and sets prefer to keep the strongest cards in hand.
        double handQuality = own.Hand.Sum(HandValue) / 100000.0;

        return profile.BoardWeight * board
            + profile.CardsWeight * (cards + readiness + handQuality)
            + profile.LifeWeight * (life + potential)
            - profile.RiskWeight * risk;
    }

    /// <summary>
    /// The fixed answer policy (systems.md §7): optional triggers are taken,
    /// mandatory ones in option order, costs are paid with the lowest-value
    /// cards and targets point at the highest-value ones; a prompt over the
    /// opponent's cards takes as many of their best cards as it may. Used for
    /// the opponent's prompts inside a rollout and for prompts too wide to
    /// evaluate answer by answer.
    /// </summary>
    public static AnswerChoice StaticAnswer(DuelState state, PendingChoice pending)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(pending);
        Choice choice = pending.Choice;
        int player = pending.Player;
        switch (pending.Kind)
        {
            case ChoiceKind.OptionalTrigger:
                return new AnswerChoice(player, choice.Options.Take(Math.Min(1, choice.Max)).ToList());
            case ChoiceKind.TriggerOrder:
                return new AnswerChoice(player, choice.Options.Take(choice.Min).ToList());
        }

        var cards = choice.Options.Select(id => (Id: id, Card: state.Find(id))).ToList();
        bool theirs = cards.Count > 0 && cards.All(c => c.Card is { } card && card.Controller != player);
        bool wantBest = theirs || pending.Kind == ChoiceKind.Target;
        int count = theirs ? Math.Min(choice.Max, cards.Count) : choice.Min;
        IEnumerable<Guid> ranked = wantBest
            ? cards.OrderByDescending(c => CardValue(c.Card)).Select(c => c.Id)
            : cards.OrderBy(c => CardValue(c.Card)).Select(c => c.Id);
        return new AnswerChoice(player, ranked.Take(count).ToList());
    }

    /// <summary>Only <c>Pass</c> and responses are open: a chain is being built, a window waits for responses or it is the opponent's turn.</summary>
    private static bool IsResponseSituation(DuelState s, int player) =>
        s.Chain.Count > 0 || s.Window != Window.Open || s.TurnPlayer != player;

    /// <summary>A free action on the agent's own turn: the best rollout wins, then <see cref="AiProfile.BluffSet"/> may Set a decoy instead of moving on.</summary>
    private PlayerCommand Act(DuelEngine engine, int player, IReadOnlyList<PlayerCommand> legal)
    {
        PlayerCommand best = legal[0];
        double bestScore = double.NegativeInfinity;
        foreach (PlayerCommand command in legal)
        {
            if (Score(engine, player, command) is not { } score)
            {
                continue;
            }

            score += Profile.Jitter * _rng.NextGaussian();
            if (score > bestScore)
            {
                bestScore = score;
                best = command;
            }
        }

        if (best is EnterBattlePhase or Pass && Profile.BluffSet > 0 && _rng.NextDouble() < Profile.BluffSet)
        {
            DuelState s = engine.State;
            PlayerCommand? decoy = legal.OfType<SetSpellTrap>()
                .Select(set => (Command: set, Card: s.Find(set.Card)))
                .Where(pair => pair.Card is not null && !IsResponseCard(pair.Card))
                .OrderBy(pair => CardValue(pair.Card))
                .Select(pair => pair.Command)
                .FirstOrDefault();
            if (decoy is not null && s.Player(player).SpellTrapCount < PlayerState.ZoneCount - 1)
            {
                return decoy;
            }
        }

        return best;
    }

    /// <summary>
    /// The response table of systems.md §7: a response fires when, compared
    /// with passing, it saves one of the agent's monsters, prevents
    /// 1000 or more damage, negates a summon or an activation, or wins the
    /// duel, and the profile-weighted score does not drop; a response outside
    /// the table still fires when it gains about a card. Otherwise pass.
    /// </summary>
    private PlayerCommand Respond(DuelEngine engine, int player, IReadOnlyList<PlayerCommand> legal)
    {
        PlayerCommand pass = legal.FirstOrDefault(c => c is Pass) ?? legal[0];
        if (Rollout(engine, player, pass, BranchDepth) is not { } passed)
        {
            return pass;
        }

        double passScore = Evaluate(passed.State, player, Profile);
        PlayerCommand best = pass;
        double bestGain = 0.0;
        foreach (PlayerCommand command in legal)
        {
            if (command is Pass || Rollout(engine, player, command, BranchDepth) is not { } responded)
            {
                continue;
            }

            double gain = Evaluate(responded.State, player, Profile) - passScore + Profile.Jitter * _rng.NextGaussian();
            bool listed = InResponseTable(passed, responded, player);
            if ((listed && gain > 0.0 || gain > UnlistedResponseGain) && gain > bestGain)
            {
                bestGain = gain;
                best = command;
            }
        }

        return best;
    }

    private static bool InResponseTable(DuelEngine passed, DuelEngine responded, int player)
    {
        DuelState p = passed.State;
        DuelState r = responded.State;
        if (r.IsOver)
        {
            return r.Winner == player;
        }

        if (p.IsOver)
        {
            return true;
        }

        bool savesMonster = r.Player(player).MonsterCount > p.Player(player).MonsterCount;
        bool preventsDamage = r.Player(player).LifePoints - p.Player(player).LifePoints >= DamageWorthPreventing;
        bool negatesSummon = r.Opponent(player).MonsterCount < p.Opponent(player).MonsterCount;
        bool negatesActivation = responded.Events.OfType<Events.ChainLinkNegated>().Any();
        return savesMonster || preventsDamage || negatesSummon || negatesActivation;
    }

    /// <summary>Answers the pending prompt: every legal answer is rolled out and scored, unless the prompt is too wide, in which case the static policy answers.</summary>
    private PlayerCommand Answer(DuelEngine engine, int player, IReadOnlyList<PlayerCommand> legal)
    {
        if (legal.Count > MaxBranching)
        {
            AnswerChoice fallback = StaticAnswer(engine.State, engine.State.PendingChoice!);
            return engine.Validate(fallback) is null ? fallback : legal[0];
        }

        PlayerCommand best = legal[0];
        double bestScore = double.NegativeInfinity;
        foreach (PlayerCommand command in legal)
        {
            if (Score(engine, player, command) is not { } score)
            {
                continue;
            }

            score += Profile.Jitter * _rng.NextGaussian();
            if (score > bestScore)
            {
                bestScore = score;
                best = command;
            }
        }

        return best;
    }

    /// <summary>The evaluation of the quiet state <paramref name="command"/> leads to; null when the command is rejected.</summary>
    private double? Score(DuelEngine engine, int player, PlayerCommand command) =>
        Rollout(engine, player, command, BranchDepth) is { } sim ? Evaluate(sim.State, player, Profile) : null;

    /// <summary>
    /// Applies <paramref name="command"/> to a clone and plays on until the
    /// chain is empty and no window waits: both sides pass, the agent's own
    /// prompts take the best-scoring answer (branching at most
    /// <paramref name="depth"/> times), the opponent's prompts follow the
    /// static policy. Returns the clone, or null when the command is illegal.
    /// </summary>
    private DuelEngine? Rollout(DuelEngine engine, int player, PlayerCommand command, int depth)
    {
        DuelEngine sim = engine.Clone();
        if (!sim.Submit(command).Accepted)
        {
            return null;
        }

        for (int step = 0; step < RolloutBudget && !sim.State.IsOver; step++)
        {
            DuelState s = sim.State;
            if (s.PendingChoice is { } pending)
            {
                if (pending.Player == player && depth > 0)
                {
                    IReadOnlyList<PlayerCommand> answers = sim.LegalActions(player);
                    if (answers.Count > 1 && answers.Count <= MaxBranching)
                    {
                        return BestBranch(sim, player, answers, depth - 1);
                    }
                }

                AnswerChoice answer = StaticAnswer(s, pending);
                if (!sim.Submit(answer).Accepted)
                {
                    sim.Submit(sim.LegalActions(pending.Player)[0]);
                }

                continue;
            }

            if (s.Chain.Count == 0 && s.Window == Window.Open && !s.IsPaused)
            {
                break;
            }

            if (!sim.Submit(new Pass(s.Priority)).Accepted)
            {
                break;
            }
        }

        return sim;
    }

    private DuelEngine BestBranch(DuelEngine sim, int player, IReadOnlyList<PlayerCommand> answers, int depth)
    {
        DuelEngine best = sim;
        double bestScore = double.NegativeInfinity;
        foreach (PlayerCommand answer in answers)
        {
            if (Rollout(sim, player, answer, depth) is not { } branch)
            {
                continue;
            }

            double score = Evaluate(branch.State, player, Profile);
            if (score > bestScore)
            {
                bestScore = score;
                best = branch;
            }
        }

        return best;
    }

    /// <summary>
    /// What attacking into the opponent's Set cards is expected to cost this
    /// turn: the ATK of every monster that attacked, scaled by the chance
    /// that at least one of the Set cards answers an attack. Zero with no
    /// Set cards, so attacks are judged on their merit; the profile's risk
    /// weight says how much the agent plays around the rest.
    /// </summary>
    private static double AttackRisk(DuelState state, int player)
    {
        if (state.TurnPlayer != player)
        {
            return 0.0;
        }

        int sets = state.Opponent(player).SpellTraps.Count(c => c.IsFaceDown);
        if (sets == 0)
        {
            return 0.0;
        }

        double answered = 1.0 - Math.Pow(1.0 - BattleTrapShare, sets);
        return state.Player(player).Monsters.Where(m => m.AttackedThisTurn).Sum(m => m.Atk) / 1000.0 * answered;
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
        var attackers = state.Player(player).Monsters.Where(m => m.IsInAttackPosition && !m.AttackedThisTurn && !m.Has(Restriction.CannotAttack)).ToList();
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

    /// <summary>What a monster is worth on the board: its ATK face-up in attack, its DEF in defense; a face-down card counts half its DEF for its owner and a fixed guess for the opponent, who cannot see it.</summary>
    private static int BoardValue(CardInstance monster, bool known) =>
        monster.Pos switch
        {
            Position.FaceUpAttack => monster.Atk,
            Position.FaceUpDefense => monster.DefValue,
            _ => known ? monster.DefValue / 2 : UnknownMonsterValue,
        };

    /// <summary>A Trap or a Quick-Play Spell: Setting it opens activation windows.</summary>
    private static bool IsResponseCard(CardInstance card) => card.Def.IsTrap || card.Def.Spell?.Subtype == SpellSubtype.Quick;

    private static int HandValue(CardInstance card) => card.IsMonster ? card.Def.Monster!.Atk : SpellTrapValue;

    /// <summary>How much giving up (or taking out) a card hurts its controller: its ATK on the field, its printed ATK elsewhere, a fixed value for Spells and Traps.</summary>
    private static int CardValue(CardInstance? card) =>
        card is null ? 0 : card.IsMonster ? (card.IsOnField ? Math.Max(card.Atk, card.DefValue) : card.Def.Monster!.Atk) : SpellTrapValue;
}
