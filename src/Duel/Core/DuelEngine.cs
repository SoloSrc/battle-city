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
        var state = new DuelState(new DuelRng(options.Seed), options.StartingLifePoints);
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
            NormalSummon c => SummonRules.ValidateNormalSummon(this, c.Player, c.Card, c.Tributes, set: false),
            SetMonster c => SummonRules.ValidateNormalSummon(this, c.Player, c.Card, c.Tributes, set: true),
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

        Refresh();
    }

    /// <summary>Battle damage: subtracts life points (unless the player takes no battle damage) and ends the duel when they reach 0. Returns whether damage was inflicted.</summary>
    public bool Damage(int player, int amount, Guid source)
    {
        if (amount <= 0 || State.Player(player).Has(PlayerRestriction.NoBattleDamage))
        {
            return false;
        }

        Emit(new BattleDamage(player, amount, source));
        LoseLifePoints(player, amount);
        return true;
    }

    /// <summary>Effect damage (Ring of Destruction): subtracts life points and ends the duel when they reach 0.</summary>
    public void InflictDamage(int player, int amount, Guid source)
    {
        if (amount <= 0)
        {
            return;
        }

        Emit(new EffectDamage(player, amount, source));
        LoseLifePoints(player, amount);
    }

    /// <summary>Whether <paramref name="player"/> can pay <paramref name="amount"/> life points as a cost.</summary>
    public bool CanPayLifePoints(int player, int amount) => State.Player(player).LifePoints >= amount;

    /// <summary>Pays life points as a cost; paying the last point loses the duel.</summary>
    public void PayLifePoints(int player, int amount, Guid source)
    {
        if (amount <= 0)
        {
            return;
        }

        if (!CanPayLifePoints(player, amount))
        {
            throw new InvalidOperationException($"player {player} cannot pay {amount} life points");
        }

        Emit(new LifePointsPaid(player, amount, source));
        LoseLifePoints(player, amount);
    }

    public void GainLifePoints(int player, int amount, Guid source)
    {
        if (amount <= 0)
        {
            return;
        }

        PlayerState p = State.Player(player);
        int from = p.LifePoints;
        p.LifePoints = from + amount;
        Emit(new LifePointsGained(player, amount, source));
        Emit(new LifePointsChanged(player, from, p.LifePoints));
    }

    /// <summary>
    /// Destroys a card on the field: it goes to the Graveyard and its
    /// battle-destruction triggers fire when <paramref name="reason"/> is
    /// battle, with <paramref name="battled"/> the monster that destroyed it.
    /// With <paramref name="negateEffects"/> none of its triggers fire (Dark
    /// Balter the Terrible destroyed it).
    /// </summary>
    public void Destroy(CardInstance card, DestroyReason reason, Guid? battled = null, bool negateEffects = false)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!card.IsOnField)
        {
            return;
        }

        if (reason == DestroyReason.Battle && card.IsMonster && AbsorbedBy(card) is { } absorbed)
        {
            // Thousand-Eyes Restrict: the monster it absorbed is destroyed in its place.
            Destroy(absorbed, DestroyReason.Effect);
            return;
        }

        Location from = card.Loc;
        if (card.IsMonster)
        {
            Emit(new MonsterDestroyed(card.Controller, card.Id, card.Def.Id, reason));
        }
        else
        {
            Emit(new SpellTrapDestroyed(card.Controller, card.Id, card.Def.Id));
        }

        Zones.ToGraveyard(this, card, negateEffects);
        if (reason == DestroyReason.Battle && !negateEffects)
        {
            QueueTriggers(card, TriggerWindow.OnDestroyedByBattle, from, battled: battled);
        }
    }

    /// <summary>Sends a card from anywhere to its owner's Graveyard (tributes, costs, mills).</summary>
    public void SendToGraveyard(CardInstance card)
    {
        ArgumentNullException.ThrowIfNull(card);
        Zones.ToGraveyard(this, card);
    }

    /// <summary>Discards a card from the hand by an effect or cost.</summary>
    public void Discard(CardInstance card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card.Loc != Location.Hand)
        {
            return;
        }

        Emit(new CardDiscarded(card.Owner, card.Id, card.Def.Id));
        Zones.ToGraveyard(this, card);
    }

    /// <summary>Discards <paramref name="count"/> cards chosen at random from <paramref name="player"/>'s hand through <see cref="DuelState.Rng"/>, so a replay picks the same cards.</summary>
    public void DiscardRandom(int player, int count)
    {
        PlayerState p = State.Player(player);
        for (int i = 0; i < count && p.Hand.Count > 0; i++)
        {
            Discard(p.Hand[State.Rng.Next(p.Hand.Count)]);
        }
    }

    /// <summary>Banishes a card from anywhere; a token is removed instead.</summary>
    public void Banish(CardInstance card)
    {
        ArgumentNullException.ThrowIfNull(card);
        Zones.ToBanished(this, card);
    }

    /// <summary>Returns a card from the field, Graveyard or Banished pile to its owner's hand; a token is removed instead.</summary>
    public void ReturnToHand(CardInstance card)
    {
        ArgumentNullException.ThrowIfNull(card);
        Zones.ToHand(this, card);
    }

    /// <summary>Returns a card to its owner's Deck, on top or at the bottom; a token is removed instead.</summary>
    public void ReturnToDeck(CardInstance card, bool top)
    {
        ArgumentNullException.ThrowIfNull(card);
        Zones.ToDeck(this, card, top);
    }

    public void ShuffleDeck(int player)
    {
        State.Rng.Shuffle(State.Player(player).Deck);
        Emit(new DeckShuffled(player));
    }

    /// <summary>
    /// Special Summons a monster from the hand, Deck, Graveyard, Banished pile
    /// or Fusion Deck to <paramref name="player"/>'s field. Returns false, with
    /// nothing changed, when there is no free zone, the card is not a monster,
    /// it is a Spirit or the player cannot Summon this turn (systems.md §5.5).
    /// </summary>
    public bool SpecialSummon(CardInstance card, int player, Position position)
    {
        ArgumentNullException.ThrowIfNull(card);
        int zone = State.Player(player).FirstFreeMonsterZone();
        if (zone < 0 || !card.IsMonster || card.IsOnField || card.Def.Monster!.Category == MonsterCategory.Spirit || State.Player(player).Has(PlayerRestriction.CannotSummon))
        {
            return false;
        }

        Location from = card.Loc;
        Zones.PlaceMonster(this, card, player, zone, position);
        card.ArrivedThisTurn = true;
        Emit(new MonsterSpecialSummoned(player, card.Id, card.Def.Id, zone, position, from));
        if (card.IsFaceUp)
        {
            SummonRules.Summoned(this, card, SummonKind.Special);
        }

        return true;
    }

    /// <summary>Creates a token on <paramref name="player"/>'s field; null when there is no free zone.</summary>
    public CardInstance? CreateToken(int player, CardDefinition definition, Position position)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!definition.IsToken)
        {
            throw new ArgumentException($"{definition.Name} is not a token.", nameof(definition));
        }

        int zone = State.Player(player).FirstFreeMonsterZone();
        if (zone < 0)
        {
            return null;
        }

        var token = new CardInstance(Guid.NewGuid(), definition, player);
        Zones.PlaceMonster(this, token, player, zone, position);
        token.ArrivedThisTurn = true;
        Emit(new TokenCreated(player, token.Id, definition.Id, zone, position));
        SummonRules.Summoned(this, token, SummonKind.Special);
        return token;
    }

    /// <summary>
    /// Gives <paramref name="player"/> control of a monster; with
    /// <paramref name="untilEndOfTurn"/> it goes back to its owner at the End
    /// Phase of that turn number. Control always returns when the monster
    /// leaves the field. Returns false, with nothing changed, when the new
    /// controller has no free zone.
    /// </summary>
    public bool ChangeControl(CardInstance card, int player, int? untilEndOfTurn = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card.Loc != Location.MonsterZone)
        {
            return false;
        }

        int from = card.Controller;
        if (from == player)
        {
            card.ControlReturnsAfterTurn = player == card.Owner ? null : untilEndOfTurn;
            return true;
        }

        if (!Zones.MoveToSide(this, card, player))
        {
            return false;
        }

        card.ControlReturnsAfterTurn = player == card.Owner ? null : untilEndOfTurn;
        Emit(new ControlChanged(card.Id, card.Def.Id, from, player, card.ZoneIndex, card.ControlReturnsAfterTurn));
        return true;
    }

    /// <summary>Attaches an Equip Spell on the field to a face-up monster; false when the target is not a face-up monster on the field.</summary>
    public bool Equip(CardInstance equip, CardInstance target)
    {
        ArgumentNullException.ThrowIfNull(equip);
        ArgumentNullException.ThrowIfNull(target);
        if (equip.Loc != Location.SpellTrapZone || target.Loc != Location.MonsterZone || !target.IsFaceUp)
        {
            return false;
        }

        equip.EquippedTo = target.Id;
        Emit(new CardEquipped(equip.Controller, equip.Id, equip.Def.Id, target.Id));
        Refresh();
        return true;
    }

    /// <summary>Turns a face-up monster face-down in Defense Position (Book of Moon): its modifiers drop and its equips are destroyed once it is face-down, so an equip's leave hook (Premature Burial, Call of the Haunted) finds the monster face-down and leaves it alone.</summary>
    public void FlipFaceDown(CardInstance card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card.Loc != Location.MonsterZone || card.IsFaceDown)
        {
            return;
        }

        Modifiers.RemoveFor(this, card);
        card.Pos = Position.FaceDownDefense;
        Emit(new MonsterFlippedFaceDown(card.Controller, card.Id, card.Def.Id));
        Refresh();
        foreach (CardInstance equip in State.Players.SelectMany(p => p.SpellTraps).Where(e => e.EquippedTo == card.Id).ToList())
        {
            Destroy(equip, DestroyReason.Effect);
        }
    }

    /// <summary>Turns a face-down monster face-up in Defense Position by an effect (Swords of Revealing Light): its Flip Effect fires.</summary>
    public void FlipFaceUp(CardInstance card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card.Loc != Location.MonsterZone || card.IsFaceUp)
        {
            return;
        }

        card.Pos = Position.FaceUpDefense;
        card.FlippedThisTurn = true;
        Emit(new MonsterFlipped(card.Controller, card.Id, card.Def.Id));
        Refresh();
        QueueTriggers(card, TriggerWindow.OnFlip);
    }

    /// <summary>Switches a face-up monster between Attack and Defense Position, by a command or an effect (Enemy Controller); a monster destroyed in Defense Position (Berserk Gorilla) is destroyed.</summary>
    public void SwitchPosition(CardInstance card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (card.Loc != Location.MonsterZone || card.IsFaceDown)
        {
            return;
        }

        Position from = card.Pos;
        card.Pos = from == Position.FaceUpAttack ? Position.FaceUpDefense : Position.FaceUpAttack;
        Emit(new PositionChanged(card.Controller, card.Id, from, card.Pos));
        Refresh();
        if (card.IsInDefensePosition && card.Has(Restriction.DestroyedInDefensePosition))
        {
            Destroy(card, DestroyReason.Effect);
        }
    }

    /// <summary>Exchanges control of two monsters on opposite sides of the field for good (Creature Swap); each takes the other's zone, so no free zone is needed.</summary>
    public bool SwapControl(CardInstance first, CardInstance second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (first.Loc != Location.MonsterZone || second.Loc != Location.MonsterZone || first.Controller == second.Controller)
        {
            return false;
        }

        (int firstPlayer, int firstZone) = (first.Controller, first.ZoneIndex);
        (int secondPlayer, int secondZone) = (second.Controller, second.ZoneIndex);
        State.Player(firstPlayer).MonsterZones[firstZone] = second;
        State.Player(secondPlayer).MonsterZones[secondZone] = first;
        (first.Controller, first.ZoneIndex, first.ControlReturnsAfterTurn) = (secondPlayer, secondZone, null);
        (second.Controller, second.ZoneIndex, second.ControlReturnsAfterTurn) = (firstPlayer, firstZone, null);
        Emit(new ControlChanged(first.Id, first.Def.Id, firstPlayer, secondPlayer, secondZone, null));
        Emit(new ControlChanged(second.Id, second.Def.Id, secondPlayer, firstPlayer, firstZone, null));
        Refresh();
        return true;
    }

    /// <summary>Registers a timed modifier from a resolved effect (systems.md §5.4).</summary>
    public void AddModifier(Modifier modifier)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        if (modifier.Card is { } id && State.Find(id) is not { IsOnField: true })
        {
            return;
        }

        State.Modifiers.Add(modifier);
        Emit(new ModifierAdded(modifier));
        Refresh();
    }

    /// <summary>Changes a counter on a card on the field; the count never drops below 0 and the counter disappears at 0.</summary>
    public void AddCounter(CardInstance card, string counter, int delta)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentException.ThrowIfNullOrEmpty(counter);
        if (!card.IsOnField)
        {
            return;
        }

        int count = Math.Max(0, card.Counter(counter) + delta);
        if (count == 0)
        {
            card.Counters.Remove(counter);
        }
        else
        {
            card.Counters[counter] = count;
        }

        Emit(new CounterChanged(card.Id, card.Def.Id, counter, count));
        Refresh();
    }

    /// <summary>Sets a Spell or Trap from the hand face-down by an effect (Dust Tornado); false when the card is not a Spell or Trap in the hand or there is no free zone.</summary>
    public bool SetSpellTrap(CardInstance card)
    {
        ArgumentNullException.ThrowIfNull(card);
        int zone = State.Player(card.Owner).FirstFreeSpellTrapZone();
        if (card.Loc != Location.Hand || (!card.Def.IsSpell && !card.Def.IsTrap) || card.Def.Spell?.Subtype == SpellSubtype.Field || zone < 0)
        {
            return false;
        }

        Zones.PlaceSpellTrap(this, card, card.Owner, zone, Position.FaceDown);
        card.SetThisTurn = true;
        card.ArrivedThisTurn = true;
        Emit(new SpellTrapSet(card.Owner, card.Id, zone));
        return true;
    }

    /// <summary>
    /// Asks a question while <paramref name="link"/> resolves, to its player
    /// unless <paramref name="player"/> names the other one (Delinquent Duo's
    /// discard, Creature Swap's second pick). Returns the answer when it is
    /// already known; otherwise the question becomes the pending choice and
    /// null comes back, and the effect must return at once. Once answered,
    /// <see cref="IEffect.Resolve"/> runs again from the top and the same call
    /// returns the answer, so an effect guards the work it did before asking
    /// with <see cref="ChainLink.Stage"/>. A question with no options answers
    /// itself with an empty selection.
    /// </summary>
    public IReadOnlyList<Guid>? Ask(ChainLink link, Choice choice, int? player = null)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(choice);
        if (link.NextAnswer < link.Answers.Count)
        {
            return link.Answers[link.NextAnswer++];
        }

        if (choice.Options.Count == 0)
        {
            link.Answers.Add(Array.Empty<Guid>());
            link.NextAnswer++;
            return Array.Empty<Guid>();
        }

        State.ResolvingLink = link;
        Ask(player ?? link.Player, ChoiceKind.Resolution, link.Source.Id, choice);
        return null;
    }

    /// <summary>Recomputes the modifiers (systems.md §5.4); the engine calls it after every state change.</summary>
    public void Refresh() => Modifiers.Recompute(this);

    private void LoseLifePoints(int player, int amount)
    {
        PlayerState p = State.Player(player);
        int from = p.LifePoints;
        p.LifePoints = Math.Max(0, from - amount);
        Emit(new LifePointsChanged(player, from, p.LifePoints));
        if (p.LifePoints == 0)
        {
            Lose(player, DuelOutcome.LifePoints);
        }
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
    internal void QueueTriggers(CardInstance card, TriggerWindow window, Location? from = null, SummonKind? summon = null, Guid? battled = null)
    {
        var context = new ActivationContext(window, from, summon, battled);
        foreach (IEffect effect in EffectsOf(card))
        {
            if (effect.Kind is EffectKind.Trigger or EffectKind.Flip && effect.FiresIn(window) && Usable(card, effect) && effect.CanActivate(State, card, context))
            {
                State.Triggers.Add(new PendingTrigger(card.Controller, card.Id, effect.Id, context, effect.IsMandatory));
            }
        }
    }

    /// <summary>Once-per-turn and negation checks shared by activations and triggers.</summary>
    internal bool Usable(CardInstance card, IEffect effect) =>
        !(effect.OncePerTurn && card.Activations(effect.Id) > 0) && !(card.IsOnField && card.Has(Restriction.EffectsNegated)) && !IsAbsorbed(card);

    /// <summary>A monster sitting in a Spell &amp; Trap Zone as an Equip Card (Thousand-Eyes Restrict): its own effects are off.</summary>
    public static bool IsAbsorbed(CardInstance card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card.IsMonster && card.Loc == Location.SpellTrapZone;
    }

    /// <summary>The monster equipped to <paramref name="card"/> as an Equip Card (Thousand-Eyes Restrict), or null.</summary>
    public CardInstance? AbsorbedBy(CardInstance card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return State.Players.SelectMany(p => p.SpellTraps).FirstOrDefault(e => e.IsMonster && e.EquippedTo == card.Id);
    }

    /// <summary>
    /// Turns a monster on the field into an Equip Card attached to <paramref name="target"/>
    /// (Thousand-Eyes Restrict): it moves to a free Spell &amp; Trap Zone of the target's
    /// controller, face-up, and is destroyed in the target's place when battle would destroy
    /// it. False, with nothing changed, for a token or without a free zone.
    /// </summary>
    public bool AbsorbMonster(CardInstance monster, CardInstance target)
    {
        ArgumentNullException.ThrowIfNull(monster);
        ArgumentNullException.ThrowIfNull(target);
        int zone = State.Player(target.Controller).FirstFreeSpellTrapZone();
        if (monster.Loc != Location.MonsterZone || target.Loc != Location.MonsterZone || monster == target || monster.IsToken || zone < 0)
        {
            return false;
        }

        Zones.PlaceSpellTrap(this, monster, target.Controller, zone, Position.FaceUp);
        monster.EquippedTo = target.Id;
        Emit(new MonsterAbsorbed(target.Controller, monster.Id, monster.Def.Id, zone, target.Id));
        Refresh();
        return true;
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
            State.ResolvingLink = null;
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

        if (link.Effect.Kind == EffectKind.SummonProcedure)
        {
            // An inherent Special Summon: the costs were its materials, the monster arrives, no chain link.
            State.PendingLink = null;
            SpecialSummon(link.Source, link.Player, Position.FaceUpAttack);
            TurnFlow.GivePriorityToTurnPlayer(State);
            return;
        }

        IReadOnlyList<Choice> targets = link.Effect.Targets(State, link.Source);
        if (link.Targets.Count < targets.Count)
        {
            Ask(link.Player, ChoiceKind.Target, link.Source.Id, targets[link.Targets.Count]);
            return;
        }

        State.PendingLink = null;
        State.Chain.Add(link);
        link.Source.ActivationsThisTurn[link.Effect.Id] = link.Source.Activations(link.Effect.Id) + 1;
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
        BeginActivation(trigger.Player, card, effect, trigger.Context);
    }

    private bool StillFires(PendingTrigger trigger, CardInstance card)
    {
        IEffect? effect = EffectsOf(card).FirstOrDefault(e => e.Id == trigger.EffectId);
        return effect is not null && Usable(card, effect) && effect.CanActivate(State, card, trigger.Context);
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
                foreach (Guid id in answer.Selected)
                {
                    if (State.Find(id) is { IsOnField: true } target && target.Has(Restriction.DestroyedWhenTargeted))
                    {
                        Destroy(target, DestroyReason.Effect);
                    }
                }

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

            case ChoiceKind.Resolution:
                State.ResolvingLink!.Answers.Add(answer.Selected);
                ChainResolver.Resume(this);
                break;
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
