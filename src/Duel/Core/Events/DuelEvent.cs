using System;
using System.Collections.Generic;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Events;

/// <summary>What happened (systems.md §5.1). Presentation animates these; the AI never needs them.</summary>
public abstract record DuelEvent;

public sealed record DuelStarted(int FirstPlayer, ulong Seed) : DuelEvent;

public sealed record CardDrawn(int Player, Guid Card, string CardId) : DuelEvent;

public sealed record TurnStarted(int Player, int Turn) : DuelEvent;

public sealed record PhaseChanged(Phase Phase) : DuelEvent;

public sealed record BattleStepChanged(BattleStep Step) : DuelEvent;

public sealed record DamageSubstepChanged(DamageSubstep Substep) : DuelEvent;

/// <summary>A response window opened or closed (systems.md §5.5).</summary>
public sealed record WindowChanged(Window Window, Guid? Card) : DuelEvent;

public sealed record PriorityPassed(int Player) : DuelEvent;

public sealed record MonsterSummoned(int Player, Guid Card, string CardId, int Zone, Position Position, IReadOnlyList<Guid> Tributes) : DuelEvent;

public sealed record MonsterSet(int Player, Guid Card, int Zone, IReadOnlyList<Guid> Tributes) : DuelEvent;

public sealed record PositionChanged(int Player, Guid Card, Position From, Position To) : DuelEvent;

public sealed record MonsterFlipSummoned(int Player, Guid Card, string CardId) : DuelEvent;

/// <summary>A face-down monster was turned face-up by battle.</summary>
public sealed record MonsterFlipped(int Player, Guid Card, string CardId) : DuelEvent;

public sealed record SpellTrapSet(int Player, Guid Card, int Zone) : DuelEvent;

public sealed record SpellActivated(int Player, Guid Card, string CardId, int Zone) : DuelEvent;

public sealed record TrapActivated(int Player, Guid Card, string CardId, int Zone) : DuelEvent;

/// <summary>A monster's Ignition, Quick or Trigger effect was activated.</summary>
public sealed record EffectActivated(int Player, Guid Card, string CardId, string EffectId) : DuelEvent;

/// <summary>The engine asked <see cref="Player"/> a question; it waits for <c>AnswerChoice</c>.</summary>
public sealed record ChoiceRequested(int Player, ChoiceKind Kind, Guid? Source, string Prompt, IReadOnlyList<Guid> Options, int Min, int Max) : DuelEvent;

public sealed record ChoiceAnswered(int Player, ChoiceKind Kind, IReadOnlyList<Guid> Selected) : DuelEvent;

public sealed record ChainLinkAdded(int Link, int Player, Guid Card, string EffectId) : DuelEvent;

public sealed record ChainLinkNegated(int Link, Guid Card, string EffectId) : DuelEvent;

public sealed record ChainLinkResolved(int Link, Guid Card, string EffectId) : DuelEvent;

public sealed record BattlePhaseEntered(int Player) : DuelEvent;

public sealed record AttackDeclared(int Player, Guid Attacker, Guid? Target) : DuelEvent;

/// <summary>The attack ended before damage calculation because the attacker or its target left the field.</summary>
public sealed record AttackCancelled(Guid Attacker) : DuelEvent;

public sealed record BattleDamage(int Player, int Amount, Guid Source) : DuelEvent;

public sealed record LifePointsChanged(int Player, int From, int To) : DuelEvent;

public sealed record MonsterDestroyed(int Player, Guid Card, string CardId, DestroyReason Reason) : DuelEvent;

public sealed record SpellTrapDestroyed(int Player, Guid Card, string CardId) : DuelEvent;

/// <summary>A monster arrived on the field by an effect: from the hand, Deck, Graveyard or Fusion Deck.</summary>
public sealed record MonsterSpecialSummoned(int Player, Guid Card, string CardId, int Zone, Position Position, Location From) : DuelEvent;

/// <summary>A token was created on <see cref="Player"/>'s field.</summary>
public sealed record TokenCreated(int Player, Guid Card, string CardId, int Zone, Position Position) : DuelEvent;

/// <summary>A token left the field and with it the duel.</summary>
public sealed record TokenRemoved(int Player, Guid Card, string CardId) : DuelEvent;

public sealed record CardBanished(int Player, Guid Card, string CardId, Location From) : DuelEvent;

public sealed record CardReturnedToHand(int Player, Guid Card, string CardId, Location From) : DuelEvent;

/// <summary>A card went back to its owner's Deck, on top or at the bottom.</summary>
public sealed record CardReturnedToDeck(int Player, Guid Card, string CardId, Location From, bool Top) : DuelEvent;

public sealed record DeckShuffled(int Player) : DuelEvent;

/// <summary>A monster changed sides; <see cref="ReturnsAfterTurn"/> is set for a temporary change.</summary>
public sealed record ControlChanged(Guid Card, string CardId, int From, int To, int Zone, int? ReturnsAfterTurn) : DuelEvent;

public sealed record CardEquipped(int Player, Guid Equip, string EquipId, Guid Target) : DuelEvent;

/// <summary>A face-up monster was turned face-down (Book of Moon); its equips are destroyed.</summary>
public sealed record MonsterFlippedFaceDown(int Player, Guid Card, string CardId) : DuelEvent;

/// <summary>A Spirit monster went back to the hand at the End Phase.</summary>
public sealed record SpiritReturned(int Player, Guid Card, string CardId) : DuelEvent;

public sealed record CounterChanged(Guid Card, string CardId, string Counter, int Count) : DuelEvent;

public sealed record LifePointsPaid(int Player, int Amount, Guid Source) : DuelEvent;

public sealed record LifePointsGained(int Player, int Amount, Guid Source) : DuelEvent;

/// <summary>Damage from a card effect rather than battle.</summary>
public sealed record EffectDamage(int Player, int Amount, Guid Source) : DuelEvent;

public sealed record ModifierAdded(Modifier Modifier) : DuelEvent;

/// <summary>A timed modifier ran out at the End Phase, or lost its card.</summary>
public sealed record ModifierRemoved(Modifier Modifier) : DuelEvent;

public sealed record CardSentToGraveyard(int Player, Guid Card, string CardId, Location From) : DuelEvent;

public sealed record CardDiscarded(int Player, Guid Card, string CardId) : DuelEvent;

public sealed record DuelEnded(int? Winner, DuelOutcome Outcome) : DuelEvent;
