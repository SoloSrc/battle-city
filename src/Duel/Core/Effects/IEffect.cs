using System;
using System.Collections.Generic;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects;

/// <summary>
/// A card effect keyed by id (systems.md §5.4). The engine owns timing,
/// speed and priority; an effect only states its own conditions, asks its
/// questions through <see cref="Choice"/> and mutates the duel in
/// <see cref="PayCosts"/> and <see cref="Resolve"/>. <see cref="EffectBase"/>
/// supplies defaults for everything an effect does not use.
/// </summary>
public interface IEffect
{
    /// <summary>Registry id, also the value used in <c>data/cards/*.json</c> <c>effects</c>.</summary>
    string Id { get; }

    EffectKind Kind { get; }

    SpellSpeed Speed { get; }

    /// <summary>A mandatory Trigger effect activates without asking; an optional one asks its controller first.</summary>
    bool IsMandatory { get; }

    /// <summary>The window a Trigger effect fires in; null for every other kind.</summary>
    TriggerWindow? Trigger { get; }

    /// <summary>A speed 2 effect that changes ATK or DEF may also be activated before damage calculation (systems.md §5.5 Damage Step row).</summary>
    bool UsableInDamageStep { get; }

    /// <summary>The effect activates once per turn per card; the engine counts activations in <see cref="CardInstance.ActivationsThisTurn"/>.</summary>
    bool OncePerTurn { get; }

    /// <summary>The card-specific condition: whether the effect may be activated now by its controller. Timing shared by every effect (speed, priority, Set turn) is checked by the engine.</summary>
    bool CanActivate(DuelState state, CardInstance source, ActivationContext context);

    /// <summary>Costs to choose at activation, asked in order; the answers land in <see cref="ChainLink.Costs"/>.</summary>
    IReadOnlyList<Choice> Costs(DuelState state, CardInstance source);

    /// <summary>Targets declared at activation, asked after the costs are paid; the answers land in <see cref="ChainLink.Targets"/>.</summary>
    IReadOnlyList<Choice> Targets(DuelState state, CardInstance source);

    /// <summary>Pays the chosen costs; runs once, before targets are asked and before the link joins the chain.</summary>
    void PayCosts(DuelEngine engine, ChainLink link);

    /// <summary>Applies the effect when its chain link resolves; not called for a negated link.</summary>
    void Resolve(DuelEngine engine, ChainLink link);

    /// <summary>The modifiers this card grants while face-up on the field (Continuous effects, equips); recomputed by the engine after every state change, never stored.</summary>
    IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source);

    /// <summary>Runs as <paramref name="source"/> leaves the field, before its field state is cleared: <paramref name="equippedTo"/> is the monster it was attached to. Continuous consequences only (return control, destroy the attached monster); it does not use the chain.</summary>
    void OnLeftField(DuelEngine engine, CardInstance source, Location from, Guid? equippedTo);
}
