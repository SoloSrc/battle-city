using System;
using BattleCity.Duel.Core.Effects;

namespace BattleCity.Duel.Core.Model;

/// <summary>What a pending <see cref="Choice"/> decides.</summary>
public enum ChoiceKind
{
    /// <summary>A cost of the effect being activated (<see cref="DuelState.PendingLink"/>).</summary>
    Cost,

    /// <summary>A target of the effect being activated.</summary>
    Target,

    /// <summary>Whether to activate an optional Trigger effect: one option, select it to activate.</summary>
    OptionalTrigger,

    /// <summary>Which of several mandatory Trigger effects goes on the chain next.</summary>
    TriggerOrder,
}

/// <summary>
/// A question the engine is waiting on (systems.md §5.4): <see cref="Player"/>
/// answers with <c>AnswerChoice</c>. While one is pending, nobody else may act.
/// </summary>
public sealed record PendingChoice(int Player, ChoiceKind Kind, Guid? Source, Choice Choice);
