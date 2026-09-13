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
/// the state and <see cref="LegalActions"/>. Everything that asks a player
/// something goes through a <see cref="PendingChoice"/> answered with
/// <see cref="AnswerChoice"/>, so the UI and the AI share one protocol.
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

    /// <summary>The player expected to submit the next command: whoever owes an answer, else the priority holder.</summary>
    public int ActingPlayer => State.PendingChoice?.Player ?? State.Priority;

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
        engine.Settle();
        engine.RunAutoPass();
        return engine;
    }

    /// <summary>Every command <paramref name="player"/> may submit right now; empty when it is not their turn to act.</summary>
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
        Settle();
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

        if (State.PendingChoice is { } pending)
        {
            return command is AnswerChoice answer ? ValidateAnswer(pending, answer) : "answer the pending choice first";
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
            ActivateTrap c => ChainResolver.ValidateActivateTrap(this, c.Player, c.Card),
            ActivateEffect c => ChainResolver.ValidateActivateEffect(this, c.Player, c.Card, c.EffectIndex),
            AnswerChoice => "no choice is pending",
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

    /// <summary>Destroys a card on the field: it goes to the Graveyard and its battle-destruction triggers fire when <paramref name="reason"/> is battle.</summary>
    public void Destroy(CardInstance card, DestroyReason reason)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!card.IsOnField)
        {
            return;
        }

        Location from = card.Loc;
        if (card.IsMonster)
        {
            Emit(new MonsterDestroyed(card.Controller, card.Id, card.Def.Id, reason));
        }

        Zones.ToGraveyard(this, card);
        if (reason == DestroyReason.Battle)
        {
            QueueTriggers(card, TriggerWindow.OnDestroyedByBattle, from);
        }
    }

    /// <summary>Sends a card from anywhere to its owner's Graveyard (discards, tributes, costs).</summary>
    public void SendToGraveyard(CardInstance card)
    {
        ArgumentNullException.ThrowIfNull(card);
        Zones.ToGraveyard(this, card);
    }

    /// <summary>Negates a chain link: it is skipped when the chain resolves.</summary>
    public void Negate(ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(link);
        if (link.Negated)
        {
            return;
        }

        link.Negated = true;
        Emit(new ChainLinkNegated(link.Index, link.Source.Id, link.Effect.Id));
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

    /// <summary>Queues every Trigger effect of <paramref name="card"/> that fires in <paramref name="window"/> and whose condition holds.</summary>
    internal void QueueTriggers(CardInstance card, TriggerWindow window, Location? from = null)
    {
        var context = new ActivationContext(window, from);
        foreach (IEffect effect in EffectsOf(card))
        {
            if (effect.Kind is EffectKind.Trigger or EffectKind.Flip && effect.Trigger == window && effect.CanActivate(State, card, context))
            {
                State.Triggers.Add(new PendingTrigger(card.Controller, card.Id, effect.Id, window, from, effect.IsMandatory));
            }
        }
    }

    /// <summary>Starts an activation: the link collects its cost and target answers in <see cref="Settle"/> before joining the chain.</summary>
    internal void BeginActivation(int player, CardInstance card, IEffect effect, ActivationContext context)
    {
        State.PendingLink = new ChainLink(State.Chain.Count + 1, player, card, effect, context);
    }

    /// <summary>
    /// Runs everything that needs no command: finishes the pending activation,
    /// puts fired triggers on the chain (mandatory first, turn player first,
    /// then optional; systems.md §5.5) and stops at the first question. When
    /// links joined the chain, the opponent of the last one gets priority.
    /// </summary>
    internal void Settle()
    {
        int before = State.Chain.Count;
        while (!State.IsOver && State.PendingChoice is null)
        {
            if (State.PendingLink is not null)
            {
                AdvancePendingLink();
            }
            else if (State.Triggers.Count > 0)
            {
                StartNextTrigger();
            }
            else
            {
                break;
            }
        }

        if (State.IsOver)
        {
            State.PendingChoice = null;
            State.PendingLink = null;
            State.Triggers.Clear();
            return;
        }

        if (State.PendingChoice is null && State.Chain.Count > before)
        {
            State.Priority = 1 - State.Chain[^1].Player;
            State.ConsecutivePasses = 0;
        }
    }

    private void AdvancePendingLink()
    {
        ChainLink link = State.PendingLink!;
        IReadOnlyList<Choice> costs = link.Effect.Costs(State, link.Source);
        if (link.Costs.Count < costs.Count)
        {
            Ask(link.Player, ChoiceKind.Cost, link.Source.Id, costs[link.Costs.Count]);
            return;
        }

        if (!link.CostsPaid)
        {
            link.Effect.PayCosts(this, link);
            link.CostsPaid = true;
        }

        IReadOnlyList<Choice> targets = link.Effect.Targets(State, link.Source);
        if (link.Targets.Count < targets.Count)
        {
            Ask(link.Player, ChoiceKind.Target, link.Source.Id, targets[link.Targets.Count]);
            return;
        }

        State.PendingLink = null;
        State.Chain.Add(link);
        Emit(new ChainLinkAdded(link.Index, link.Player, link.Source.Id, link.Effect.Id));
    }

    private void StartNextTrigger()
    {
        PendingTrigger first = State.Triggers
            .OrderByDescending(t => t.Mandatory)
            .ThenByDescending(t => t.Player == State.TurnPlayer)
            .First();
        if (!first.Mandatory)
        {
            CardInstance? card = State.Find(first.Card);
            if (card is null || !StillFires(first, card))
            {
                State.Triggers.Remove(first);
                return;
            }

            Ask(first.Player, ChoiceKind.OptionalTrigger, first.Card, new Choice($"Activate {card.Def.Name}?", new[] { first.Card }, 0, 1));
            return;
        }

        var group = State.Triggers.Where(t => t.Mandatory && t.Player == first.Player).ToList();
        if (group.Count > 1)
        {
            Ask(first.Player, ChoiceKind.TriggerOrder, null, new Choice("Choose the effect to activate next", group.Select(t => t.Card).Distinct().ToList(), 1, 1));
            return;
        }

        State.Triggers.Remove(first);
        Activate(first);
    }

    /// <summary>Puts a fired trigger on the chain unless its condition stopped holding (its card moved) since it fired.</summary>
    private void Activate(PendingTrigger trigger)
    {
        CardInstance? card = State.Find(trigger.Card);
        if (card is null || !StillFires(trigger, card))
        {
            return;
        }

        IEffect effect = EffectsOf(card).First(e => e.Id == trigger.EffectId);
        Emit(new EffectActivated(trigger.Player, card.Id, card.Def.Id, effect.Id));
        BeginActivation(trigger.Player, card, effect, new ActivationContext(trigger.Window, trigger.From));
    }

    private bool StillFires(PendingTrigger trigger, CardInstance card)
    {
        IEffect? effect = EffectsOf(card).FirstOrDefault(e => e.Id == trigger.EffectId);
        return effect is not null && effect.CanActivate(State, card, new ActivationContext(trigger.Window, trigger.From));
    }

    private void Ask(int player, ChoiceKind kind, Guid? source, Choice choice)
    {
        State.PendingChoice = new PendingChoice(player, kind, source, choice);
        Emit(new ChoiceRequested(player, kind, source, choice.Prompt, choice.Options, choice.Min, choice.Max));
    }

    private static string? ValidateAnswer(PendingChoice pending, AnswerChoice answer)
    {
        if (answer.Player != pending.Player)
        {
            return $"player {pending.Player} owes the answer";
        }

        Choice choice = pending.Choice;
        if (answer.Selected.Count < choice.Min || answer.Selected.Count > choice.Max)
        {
            return $"select {choice.Min}–{choice.Max} option(s), {answer.Selected.Count} given";
        }

        if (answer.Selected.Distinct().Count() != answer.Selected.Count)
        {
            return "an option cannot be selected twice";
        }

        return answer.Selected.All(choice.Options.Contains) ? null : "an option is not among the choices";
    }

    private void Answer(AnswerChoice answer)
    {
        PendingChoice pending = State.PendingChoice!;
        State.PendingChoice = null;
        Emit(new ChoiceAnswered(pending.Player, pending.Kind, answer.Selected));
        switch (pending.Kind)
        {
            case ChoiceKind.Cost:
                State.PendingLink!.Costs.Add(answer.Selected);
                break;
            case ChoiceKind.Target:
                State.PendingLink!.Targets.Add(answer.Selected);
                break;
            case ChoiceKind.OptionalTrigger:
                {
                    PendingTrigger trigger = State.Triggers.First(t => !t.Mandatory && t.Player == pending.Player && t.Card == pending.Source);
                    State.Triggers.Remove(trigger);
                    if (answer.Selected.Count > 0)
                    {
                        Activate(trigger);
                    }

                    break;
                }

            case ChoiceKind.TriggerOrder:
                {
                    PendingTrigger trigger = State.Triggers.First(t => t.Mandatory && t.Player == pending.Player && t.Card == answer.Selected[0]);
                    State.Triggers.Remove(trigger);
                    Activate(trigger);
                    break;
                }
        }
    }

    private void Build(int player, Deck deck)
    {
        if (deck.Main.Count < Options.MinDeckSize || deck.Main.Count > Options.MaxDeckSize)
        {
            throw new ArgumentException($"Player {player}'s deck has {deck.Main.Count} cards; {Options.MinDeckSize}–{Options.MaxDeckSize} required.", nameof(deck));
        }

        var missing = deck.Main.Concat(deck.Fusion).SelectMany(d => d.Effects).Distinct().Where(e => !_registry.Contains(e)).ToList();
        if (missing.Count > 0)
        {
            throw new ArgumentException($"Player {player}'s deck uses effects that are not implemented: {string.Join(", ", missing)}.", nameof(deck));
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
            case ActivateTrap c:
                ChainResolver.ActivateTrap(this, c.Player, c.Card);
                break;
            case ActivateEffect c:
                ChainResolver.ActivateEffect(this, c.Player, c.Card, c.EffectIndex);
                break;
            case AnswerChoice c:
                Answer(c);
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
    /// Steps. The turn player is never passed for in an open Main Phase or
    /// Battle Step; ending the phase is their call.
    /// </summary>
    private void RunAutoPass()
    {
        if (!Options.AutoPass)
        {
            return;
        }

        while (!State.IsOver && State.PendingChoice is null && CanAutoPass(State.Priority))
        {
            IReadOnlyList<PlayerCommand> legal = LegalActions(State.Priority);
            if (legal.Count != 1 || legal[0] is not Pass pass)
            {
                return;
            }

            TurnFlow.Pass(this, pass.Player);
            Settle();
        }
    }

    private bool CanAutoPass(int player)
    {
        DuelState s = State;
        if (player != s.TurnPlayer || s.Chain.Count > 0 || s.Window != Window.Open)
        {
            return true;
        }

        return s.Phase is Phase.Draw or Phase.Standby or Phase.End || s.BattleStep is BattleStep.Start or BattleStep.End;
    }
}
