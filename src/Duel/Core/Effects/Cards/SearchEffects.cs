using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects.Cards;

/// <summary>Sangan: when sent from the field to the Graveyard, add 1 monster with 1500 or less ATK from your Deck to your hand.</summary>
public sealed class SanganEffect : EffectBase
{
    public const string EffectId = "sangan";

    public const int MaximumAtk = 1500;

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Trigger;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool IsMandatory => true;

    public override TriggerWindow? Trigger => TriggerWindow.OnSentToGrave;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        return source.Loc == Location.Graveyard && context.From == Location.MonsterZone && Candidates(state, source.Owner).Any();
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (Field.PickOne(engine, link, Candidates(engine.State, link.Player).ToList(), "Add 1 monster with 1500 or less ATK from your Deck to your hand") is { } card)
        {
            engine.ReturnToHand(card);
            engine.ShuffleDeck(link.Player);
        }
    }

    private static IEnumerable<CardInstance> Candidates(DuelState state, int player) =>
        state.Player(player).Deck.Where(c => c.Def.Kind == CardKind.Monster && c.Def.Monster!.Atk <= MaximumAtk);
}

/// <summary>Reinforcement of the Army: add 1 Level 4 or lower Warrior monster from your Deck to your hand.</summary>
public sealed class ReinforcementOfTheArmyEffect : EffectBase
{
    public const string EffectId = "reinforcement_of_the_army";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return Candidates(state, source.Controller).Any();
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (Field.PickOne(engine, link, Candidates(engine.State, link.Player).ToList(), "Add 1 Level 4 or lower Warrior from your Deck to your hand") is { } card)
        {
            engine.ReturnToHand(card);
            engine.ShuffleDeck(link.Player);
        }
    }

    private static IEnumerable<CardInstance> Candidates(DuelState state, int player) =>
        state.Player(player).Deck.Where(c => c.Def.Kind == CardKind.Monster && Field.IsWarrior(c) && c.Def.Monster!.Level <= 4);
}

/// <summary>The Warrior Returning Alive: target 1 Warrior monster in your Graveyard and add it to your hand.</summary>
public sealed class WarriorReturningAliveEffect : EffectBase
{
    public const string EffectId = "the_warrior_returning_alive";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return Candidates(state, source.Controller).Any();
    }

    public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return new[] { new Choice("Return 1 Warrior monster from your Graveyard to your hand", Field.Ids(Candidates(state, source.Controller)), 1, 1) };
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (link.Target is { } id && engine.State.Find(id) is { Loc: Location.Graveyard } card)
        {
            engine.ReturnToHand(card);
        }
    }

    private static IEnumerable<CardInstance> Candidates(DuelState state, int player) =>
        state.Player(player).Graveyard.Where(c => c.Def.Kind == CardKind.Monster && Field.IsWarrior(c));
}

/// <summary>Gravekeeper's Spy: FLIP: Special Summon 1 "Gravekeeper's" monster with 1500 or less ATK from your Deck in face-up Defense Position.</summary>
public sealed class GravekeepersSpyEffect : EffectBase
{
    public const string EffectId = "gravekeepers_spy";

    public const int MaximumAtk = 1500;

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Flip;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool IsMandatory => true;

    public override TriggerWindow? Trigger => TriggerWindow.OnFlip;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return Candidates(state, source.Controller).Any() && state.Player(source.Controller).MonsterCount < PlayerState.ZoneCount;
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (Field.PickOne(engine, link, Candidates(engine.State, link.Player).ToList(), "Special Summon 1 Gravekeeper's monster with 1500 or less ATK from your Deck") is { } card)
        {
            engine.SpecialSummon(card, link.Player, Position.FaceUpDefense);
            engine.ShuffleDeck(link.Player);
        }
    }

    private static IEnumerable<CardInstance> Candidates(DuelState state, int player) =>
        state.Player(player).Deck.Where(c => c.Def.Kind == CardKind.Monster && Field.IsGravekeeper(c) && c.Def.Monster!.Atk <= MaximumAtk);
}

/// <summary>Magician of Faith: FLIP: target 1 Spell Card in your Graveyard and add it to your hand.</summary>
public sealed class MagicianOfFaithEffect : EffectBase
{
    public const string EffectId = "magician_of_faith";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Flip;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool IsMandatory => true;

    public override TriggerWindow? Trigger => TriggerWindow.OnFlip;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return Candidates(state, source.Controller).Any();
    }

    public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return new[] { new Choice("Return 1 Spell Card from your Graveyard to your hand", Field.Ids(Candidates(state, source.Controller)), 1, 1) };
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (link.Target is { } id && engine.State.Find(id) is { Loc: Location.Graveyard } card)
        {
            engine.ReturnToHand(card);
        }
    }

    private static IEnumerable<CardInstance> Candidates(DuelState state, int player) =>
        state.Player(player).Graveyard.Where(c => c.Def.IsSpell);
}

/// <summary>Gravekeeper's Guard: FLIP: target 1 monster the opponent controls and return it to the hand.</summary>
public sealed class GravekeepersGuardEffect : EffectBase
{
    public const string EffectId = "gravekeepers_guard";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Flip;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool IsMandatory => true;

    public override TriggerWindow? Trigger => TriggerWindow.OnFlip;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return state.Opponent(source.Controller).MonsterCount > 0;
    }

    public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return new[] { new Choice("Return 1 monster the opponent controls to the hand", Field.Ids(state.Opponent(source.Controller).Monsters), 1, 1) };
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (link.Target is { } id && engine.State.Find(id) is { Loc: Location.MonsterZone } target)
        {
            engine.ReturnToHand(target);
        }
    }
}
