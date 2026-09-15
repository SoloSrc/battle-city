using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects.Cards;

/// <summary>Don Zaloog: when it inflicts battle damage, either the opponent discards 1 random card or the top 2 cards of their Deck go to the Graveyard.</summary>
public sealed class DonZaloogEffect : EffectBase
{
    public const string EffectId = "don_zaloog";

    public const int MillCount = 2;

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Trigger;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override TriggerWindow? Trigger => TriggerWindow.OnBattleDamage;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        PlayerState opponent = state.Opponent(source.Controller);
        return source.IsOnField && (opponent.Hand.Count > 0 || opponent.Deck.Count > 0);
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        PlayerState opponent = engine.State.Opponent(link.Player);
        // The two modes are offered as cards: Don Zaloog itself for the discard, the top of the opponent's Deck for the mill.
        var options = new List<Guid>();
        if (opponent.Hand.Count > 0)
        {
            options.Add(link.Source.Id);
        }

        if (opponent.Deck.Count > 0)
        {
            options.Add(opponent.Deck[^1].Id);
        }

        if (engine.Ask(link, new Choice("Select Don Zaloog to discard 1 random card from the opponent's hand, or the top of their Deck to send its top 2 cards to the Graveyard", options, 1, 1)) is not { Count: 1 } answer)
        {
            return;
        }

        if (answer[0] == link.Source.Id)
        {
            engine.DiscardRandom(1 - link.Player, 1);
            return;
        }

        for (int i = 0; i < MillCount && opponent.Deck.Count > 0; i++)
        {
            engine.SendToGraveyard(opponent.Deck[^1]);
        }
    }
}

/// <summary>D.D. Warrior Lady: when it battles an opponent's monster, after damage calculation you may banish both.</summary>
public sealed class DDWarriorLadyEffect : EffectBase
{
    public const string EffectId = "dd_warrior_lady";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Trigger;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override TriggerWindow? Trigger => TriggerWindow.OnBattle;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        return Field.OnFieldOrInGraveyard(source) && context.Battled is { } id && state.Find(id) is { } other && Field.OnFieldOrInGraveyard(other) && other.Owner != source.Owner;
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (link.Context.Battled is { } id && engine.State.Find(id) is { } other && Field.OnFieldOrInGraveyard(other))
        {
            engine.Banish(other);
        }

        if (Field.OnFieldOrInGraveyard(link.Source))
        {
            engine.Banish(link.Source);
        }
    }
}

/// <summary>D.D. Assailant: when destroyed by battle, banish it and the monster that destroyed it.</summary>
public sealed class DDAssailantEffect : EffectBase
{
    public const string EffectId = "dd_assailant";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Trigger;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool IsMandatory => true;

    public override TriggerWindow? Trigger => TriggerWindow.OnDestroyedByBattle;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        return source.Loc == Location.Graveyard && context.Battled is { } id && state.Find(id) is { } other && Field.OnFieldOrInGraveyard(other);
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (link.Context.Battled is { } id && engine.State.Find(id) is { } other && Field.OnFieldOrInGraveyard(other))
        {
            engine.Banish(other);
        }

        if (link.Source.Loc == Location.Graveyard)
        {
            engine.Banish(link.Source);
        }
    }
}

/// <summary>Airknight Parshath: inflicts piercing battle damage; when it inflicts battle damage, draw 1 card.</summary>
public sealed class AirknightParshathEffect : EffectBase
{
    public const string EffectId = "airknight_parshath";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Trigger;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool IsMandatory => true;

    public override TriggerWindow? Trigger => TriggerWindow.OnBattleDamage;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return source.IsOnField;
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        engine.Draw(link.Player, 1);
    }

    public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(source);
        yield return Modifier.OnCard(ModifierKind.Piercing, source.Id, source.Id);
    }
}

/// <summary>Kycoo the Ghost Destroyer: when it inflicts battle damage, you may banish up to 2 monsters from the opponent's Graveyard; the opponent cannot banish cards from either Graveyard.</summary>
public sealed class KycooEffect : EffectBase
{
    public const string EffectId = "kycoo_the_ghost_destroyer";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Trigger;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override TriggerWindow? Trigger => TriggerWindow.OnBattleDamage;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return source.IsOnField && Candidates(state, source.Controller).Any();
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        var candidates = Candidates(engine.State, link.Player).ToList();
        if (engine.Ask(link, new Choice("Banish up to 2 monsters from the opponent's Graveyard", Field.Ids(candidates), Math.Min(1, candidates.Count), Math.Min(2, candidates.Count))) is not { } answer)
        {
            return;
        }

        foreach (Guid id in answer)
        {
            if (engine.State.Find(id) is { Loc: Location.Graveyard } card)
            {
                engine.Banish(card);
            }
        }
    }

    public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(source);
        yield return Modifier.OnPlayer(ModifierKind.CannotBanishFromGraveyard, source.Id, 1 - source.Controller);
    }

    private static IEnumerable<CardInstance> Candidates(DuelState state, int player) => state.Opponent(player).Graveyard.Where(c => c.IsMonster);
}

/// <summary>Mystic Tomato and Shining Angel: when destroyed by battle, you may Special Summon 1 monster of the attribute with 1500 or less ATK from your Deck in Attack Position.</summary>
public sealed class RecruiterEffect : EffectBase
{
    public const string MysticTomatoId = "mystic_tomato";

    public const string ShiningAngelId = "shining_angel";

    public const int MaximumAtk = 1500;

    public RecruiterEffect(string id, MonsterAttribute attribute)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        Id = id;
        Attribute = attribute;
    }

    public override string Id { get; }

    public MonsterAttribute Attribute { get; }

    public override EffectKind Kind => EffectKind.Trigger;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override TriggerWindow? Trigger => TriggerWindow.OnDestroyedByBattle;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        PlayerState p = state.Player(source.Owner);
        return source.Loc == Location.Graveyard && Candidates(p).Any() && p.MonsterCount < PlayerState.ZoneCount && !p.Has(PlayerRestriction.CannotSummon);
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (Field.PickOne(engine, link, Candidates(engine.State.Player(link.Player)).ToList(), $"Special Summon 1 {Attribute.ToString().ToUpperInvariant()} monster with 1500 or less ATK from your Deck") is { } card)
        {
            engine.SpecialSummon(card, link.Player, Position.FaceUpAttack);
            engine.ShuffleDeck(link.Player);
        }
    }

    private IEnumerable<CardInstance> Candidates(PlayerState p) =>
        p.Deck.Where(c => c.Def.Kind == CardKind.Monster && c.Def.Monster!.Attribute == Attribute && c.Def.Monster.Atk <= MaximumAtk && c.Def.Monster.Category != MonsterCategory.Spirit);
}
