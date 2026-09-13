using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Effects;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;
using BattleCity.Duel.Core.Rng;
using BattleCity.Duel.Core.Rules;

namespace BattleCity.Duel.Core;

/// <summary>
/// The duel engine (systems.md §5.1): <see cref="Submit"/> validates a
/// command against the rules, mutates <see cref="State"/> and appends to
/// <see cref="Events"/>. Presentation reads the event stream; the AI reads
/// the state and <see cref="LegalActions"/>. Tier 1: vanilla monsters and
/// Pot of Greed; the chain and priority structure is in place for tier 2+.
/// </summary>
public sealed class DuelEngine
{
    private readonly List<DuelEvent> _events = new();
    private readonly EffectRegistry _registry;
    private readonly Dictionary<string, IReadOnlyList<IEffect>> _effectsByCard = new(StringComparer.Ordinal);

    private DuelEngine(DuelState state, DuelOptions options, EffectRegistry registry)
    {
        State = state;
        Options = options;
        _registry = registry;
    }

    /// <summary>Raised for every event as it is appended.</summary>
    public event Action<DuelEvent>? EventRaised;

    public DuelState State { get; }

    public DuelOptions Options { get; }

    public IReadOnlyList<DuelEvent> Events => _events;

    /// <summary>Starts a duel: builds the decks, flips the coin, deals the opening hands and runs turn 1 up to the first decision.</summary>
    public static DuelEngine Start(Deck deck0, Deck deck1, DuelOptions? options = null, EffectRegistry? effects = null)
    {
        ArgumentNullException.ThrowIfNull(deck0);
        ArgumentNullException.ThrowIfNull(deck1);
        options ??= new DuelOptions();
        var state = new DuelState(new DuelRng(options.Seed));
        var engine = new DuelEngine(state, options, effects ?? EffectRegistry.CreateDefault());
        engine.Build(0, deck0);
        engine.Build(1, deck1);

        int first = options.FirstPlayer ?? (state.Rng.CoinFlip() ? 0 : 1);
        engine.Emit(new DuelStarted(first, options.Seed));
        engine.Draw(first, options.OpeningHandSize);
        engine.Draw(1 - first, options.OpeningHandSize);
        TurnFlow.StartTurn(engine, first);
        engine.RunAutoPass();
        return engine;
    }

    /// <summary>Every command <paramref name="player"/> may submit right now; empty when it is not their priority.</summary>
    public IReadOnlyList<PlayerCommand> LegalActions(int player) => ActionEnumerator.Enumerate(this, player);

    /// <summary>Validates and applies a command. Illegal commands are rejected with the rule that blocked them and leave the state untouched.</summary>
    public SubmitResult Submit(PlayerCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        string? error = Validate(command);
        if (error is not null)
        {
            return SubmitResult.Rejected(error);
        }

        Apply(command);
        RunAutoPass();
        return SubmitResult.Ok;
    }

    /// <summary>The reason <paramref name="command"/> is illegal now, or null when it is legal.</summary>
    public string? Validate(PlayerCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (State.IsOver)
        {
            return "the duel is over";
        }

        if (command.Player is < 0 or > 1)
        {
            return "unknown player";
        }

        if (command.Player != State.Priority)
        {
            return $"player {command.Player} does not have priority";
        }

        return command switch
        {
            Pass => TurnFlow.ValidatePass(State, command.Player, Options),
            NormalSummon c => SummonRules.ValidateNormalSummon(State, c.Player, c.Card, c.Tributes, set: false),
            SetMonster c => SummonRules.ValidateNormalSummon(State, c.Player, c.Card, c.Tributes, set: true),
            ChangePosition c => SummonRules.ValidateChangePosition(State, c.Player, c.Card),
            FlipSummon c => SummonRules.ValidateFlipSummon(State, c.Player, c.Card),
            ActivateSpell c => ChainResolver.ValidateActivateSpell(this, c.Player, c.Card),
            SetSpellTrap c => ChainResolver.ValidateSetSpellTrap(State, c.Player, c.Card),
            EnterBattlePhase c => BattleRules.ValidateEnterBattlePhase(State, c.Player),
            DeclareAttack c => BattleRules.ValidateAttack(State, c.Player, c.Attacker, c.Target),
            Discard c => TurnFlow.ValidateDiscard(State, c.Player, c.Card, Options),
            Surrender => null,
            _ => "unknown command",
        };
    }

    /// <summary>A deep copy for one-step lookahead (systems.md §7). Events and subscribers are not copied.</summary>
    public DuelEngine Clone() => new(State.Clone(), Options, _registry);

    /// <summary>The effect objects of a card, created once per card id.</summary>
    public IReadOnlyList<IEffect> EffectsOf(CardInstance card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!_effectsByCard.TryGetValue(card.Def.Id, out IReadOnlyList<IEffect>? effects))
        {
            effects = card.Def.Effects.Select(_registry.Create).ToList();
            _effectsByCard[card.Def.Id] = effects;
        }

        return effects;
    }

    /// <summary>Draws <paramref name="count"/> cards; drawing from an empty deck loses the duel (GDD §3.1).</summary>
    public void Draw(int player, int count)
    {
        PlayerState p = State.Player(player);
        for (int i = 0; i < count; i++)
        {
            if (State.IsOver)
            {
                return;
            }

            if (p.Deck.Count == 0)
            {
                Lose(player, DuelOutcome.DeckOut);
                return;
            }

            CardInstance card = p.Deck[^1];
            p.Deck.RemoveAt(p.Deck.Count - 1);
            card.Loc = Location.Hand;
            p.Hand.Add(card);
            Emit(new CardDrawn(player, card.Id, card.Def.Id));
        }
    }

    /// <summary>Subtracts life points and ends the duel when they reach 0.</summary>
    public void Damage(int player, int amount, Guid source)
    {
        if (amount <= 0)
        {
            return;
        }

        PlayerState p = State.Player(player);
        int from = p.LifePoints;
        p.LifePoints = Math.Max(0, from - amount);
        Emit(new BattleDamage(player, amount, source));
        Emit(new LifePointsChanged(player, from, p.LifePoints));
        if (p.LifePoints == 0)
        {
            Lose(player, DuelOutcome.LifePoints);
        }
    }

    /// <summary><paramref name="player"/> loses; a second loss in the same resolution turns the result into a draw.</summary>
    public void Lose(int player, DuelOutcome outcome)
    {
        if (State.IsOver)
        {
            if (State.Winner == player)
            {
                State.Winner = null;
                State.Outcome = DuelOutcome.Draw;
                Emit(new DuelEnded(null, DuelOutcome.Draw));
            }

            return;
        }

        State.Winner = 1 - player;
        State.Outcome = outcome;
        Emit(new DuelEnded(State.Winner, outcome));
    }

    internal void Emit(DuelEvent e)
    {
        _events.Add(e);
        EventRaised?.Invoke(e);
    }

    private void Build(int player, Deck deck)
    {
        if (deck.Main.Count < Options.MinDeckSize || deck.Main.Count > Options.MaxDeckSize)
        {
            throw new ArgumentException($"Player {player}'s deck has {deck.Main.Count} cards; {Options.MinDeckSize}–{Options.MaxDeckSize} required.", nameof(deck));
        }

        PlayerState p = State.Player(player);
        foreach (CardDefinition def in deck.Main)
        {
            if (def.Kind == CardKind.Fusion)
            {
                throw new ArgumentException($"{def.Name} is a Fusion monster and belongs in the Fusion Deck.", nameof(deck));
            }

            p.Deck.Add(new CardInstance(Guid.NewGuid(), def, player));
        }

        foreach (CardDefinition def in deck.Fusion)
        {
            var card = new CardInstance(Guid.NewGuid(), def, player) { Loc = Location.FusionDeck };
            p.FusionDeck.Add(card);
        }

        if (Options.Shuffle)
        {
            State.Rng.Shuffle(p.Deck);
        }
    }

    private void Apply(PlayerCommand command)
    {
        switch (command)
        {
            case Pass c:
                TurnFlow.Pass(this, c.Player);
                break;
            case NormalSummon c:
                SummonRules.NormalSummon(this, c.Player, c.Card, c.Tributes, set: false);
                break;
            case SetMonster c:
                SummonRules.NormalSummon(this, c.Player, c.Card, c.Tributes, set: true);
                break;
            case ChangePosition c:
                SummonRules.ChangePosition(this, c.Player, c.Card);
                break;
            case FlipSummon c:
                SummonRules.FlipSummon(this, c.Player, c.Card);
                break;
            case ActivateSpell c:
                ChainResolver.ActivateSpell(this, c.Player, c.Card);
                break;
            case SetSpellTrap c:
                ChainResolver.SetSpellTrap(this, c.Player, c.Card);
                break;
            case EnterBattlePhase c:
                BattleRules.EnterBattlePhase(this, c.Player);
                break;
            case DeclareAttack c:
                BattleRules.DeclareAttack(this, c.Player, c.Attacker, c.Target);
                break;
            case Discard c:
                TurnFlow.Discard(this, c.Player, c.Card);
                break;
            case Surrender c:
                Lose(c.Player, DuelOutcome.Surrender);
                break;
        }
    }

    /// <summary>
    /// Passes for a player whose only legal action is <see cref="Pass"/> in a
    /// window with no decision to make: response windows with nothing to
    /// respond with, the Draw, Standby and End Phases, the Start and End
    /// Steps. The turn player is never passed for in a Main Phase or the
    /// Battle Step; ending the turn is their call.
    /// </summary>
    private void RunAutoPass()
    {
        if (!Options.AutoPass)
        {
            return;
        }

        while (!State.IsOver && CanAutoPass(State.Priority))
        {
            IReadOnlyList<PlayerCommand> legal = LegalActions(State.Priority);
            if (legal.Count != 1 || legal[0] is not Pass pass)
            {
                return;
            }

            TurnFlow.Pass(this, pass.Player);
        }
    }

    private bool CanAutoPass(int player)
    {
        DuelState s = State;
        if (player != s.TurnPlayer || s.Chain.Count > 0 || s.Attacker is not null)
        {
            return true;
        }

        return s.Phase is Phase.Draw or Phase.Standby or Phase.End || s.BattleStep is BattleStep.Start or BattleStep.End;
    }
}
