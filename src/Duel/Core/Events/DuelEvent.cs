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

public sealed record CardSentToGraveyard(int Player, Guid Card, string CardId, Location From) : DuelEvent;

public sealed record CardDiscarded(int Player, Guid Card, string CardId) : DuelEvent;

public sealed record DuelEnded(int? Winner, DuelOutcome Outcome) : DuelEvent;
