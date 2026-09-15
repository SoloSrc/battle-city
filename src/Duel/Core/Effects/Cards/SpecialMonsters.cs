using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects.Cards;

/// <summary>Breaker the Magical Warrior: when Normal Summoned, place 1 Spell Counter on it; each counter gives 300 ATK.</summary>
public sealed class BreakerEffect : EffectBase
{
    public const string EffectId = "breaker_the_magical_warrior";

    public const string Counter = "spell";

    public const int AtkPerCounter = 300;

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Trigger;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool IsMandatory => true;

    public override TriggerWindow? Trigger => TriggerWindow.OnSummon;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        return context.Summon == SummonKind.Normal && source.IsOnField;
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        engine.AddCounter(link.Source, Counter, 1);
    }

    public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Counter(Counter) > 0)
        {
            yield return Modifier.OnCard(ModifierKind.Atk, source.Id, source.Id, AtkPerCounter * source.Counter(Counter));
        }
    }
}

/// <summary>Breaker the Magical Warrior's second effect: remove 1 Spell Counter from it to target and destroy 1 Spell or Trap Card on the field.</summary>
public sealed class BreakerDestroyEffect : EffectBase
{
    public const string EffectId = "breaker_the_magical_warrior_destroy";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Ignition;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return source.IsOnField && source.Counter(BreakerEffect.Counter) > 0 && Field.SpellTraps(state).Any();
    }

    public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new[] { new Choice("Destroy 1 Spell or Trap Card on the field", Field.Ids(Field.SpellTraps(state)), 1, 1) };
    }

    public override void PayCosts(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        engine.AddCounter(link.Source, BreakerEffect.Counter, -1);
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (link.Target is { } id && Field.OnField(engine.State, id) is { } target)
        {
            engine.Destroy(target, DestroyReason.Effect);
        }
    }
}

/// <summary>Chaos Sorcerer's summon: cannot be Normal Summoned or Set; Special Summoned from the hand by banishing 1 LIGHT and 1 DARK monster from your Graveyard.</summary>
public sealed class ChaosSorcererSummonEffect : EffectBase
{
    public const string EffectId = "chaos_sorcerer";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.SummonProcedure;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override SummonLimit Limits => SummonLimit.CannotBeNormalSummoned | SummonLimit.CannotBeSet;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        PlayerState p = state.Player(source.Owner);
        return source.Loc == Location.Hand && !p.Has(PlayerRestriction.CannotBanishFromGraveyard) && Candidates(p, MonsterAttribute.Light).Any() && Candidates(p, MonsterAttribute.Dark).Any();
    }

    public override IReadOnlyList<Choice> Costs(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        PlayerState p = state.Player(source.Owner);
        return new[]
        {
            new Choice("Banish 1 LIGHT monster from your Graveyard", Field.Ids(Candidates(p, MonsterAttribute.Light)), 1, 1),
            new Choice("Banish 1 DARK monster from your Graveyard", Field.Ids(Candidates(p, MonsterAttribute.Dark)), 1, 1),
        };
    }

    public override void PayCosts(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        foreach (Guid id in link.Costs.SelectMany(c => c))
        {
            if (engine.State.Find(id) is { Loc: Location.Graveyard } card)
            {
                engine.Banish(card);
            }
        }
    }

    private static IEnumerable<CardInstance> Candidates(PlayerState p, MonsterAttribute attribute) =>
        p.Graveyard.Where(c => c.IsMonster && c.Def.Monster!.Attribute == attribute);
}

/// <summary>Chaos Sorcerer's Ignition effect, once per turn: target 1 face-up monster on the field and banish it; Chaos Sorcerer cannot attack this turn.</summary>
public sealed class ChaosSorcererBanishEffect : EffectBase
{
    public const string EffectId = "chaos_sorcerer_banish";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Ignition;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool OncePerTurn => true;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return source.IsOnField && Field.Monsters(state).Any(m => m.IsFaceUp);
    }

    public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new[] { new Choice("Banish 1 face-up monster on the field", Field.Ids(Field.Monsters(state).Where(m => m.IsFaceUp)), 1, 1) };
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        engine.AddModifier(Modifier.OnCard(ModifierKind.CannotAttack, link.Source.Id, link.Source.Id, 0, engine.State.TurnNumber));
        if (link.Target is { } id && Field.OnField(engine.State, id) is { IsFaceUp: true } target)
        {
            engine.Banish(target);
        }
    }
}

/// <summary>Skilled Dark Magician: whenever a Spell Card is activated, place 1 Spell Counter on it (up to 3).</summary>
public sealed class SkilledDarkMagicianCounterEffect : EffectBase
{
    public const string EffectId = "skilled_dark_magician";

    public const string Counter = "spell";

    public const int MaximumCounters = 3;

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Trigger;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool IsMandatory => true;

    public override TriggerWindow? Trigger => TriggerWindow.SpellActivated;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return source.IsOnField && source.IsFaceUp && source.Counter(Counter) < MaximumCounters;
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (link.Source.Counter(Counter) < MaximumCounters)
        {
            engine.AddCounter(link.Source, Counter, 1);
        }
    }
}

/// <summary>Skilled Dark Magician's second effect: tribute it with 3 Spell Counters to Special Summon 1 "Dark Magician" from your hand, Deck or Graveyard.</summary>
public sealed class SkilledDarkMagicianSummonEffect : EffectBase
{
    public const string EffectId = "skilled_dark_magician_summon";

    public const string DarkMagician = "Dark Magician";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Ignition;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return source.IsOnField && source.Counter(SkilledDarkMagicianCounterEffect.Counter) >= SkilledDarkMagicianCounterEffect.MaximumCounters
            && Candidates(state, source.Controller).Any() && !state.Player(source.Controller).Has(PlayerRestriction.CannotSummon);
    }

    public override void PayCosts(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        engine.SendToGraveyard(link.Source);
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (Field.PickOne(engine, link, Candidates(engine.State, link.Player).ToList(), "Special Summon 1 Dark Magician from your hand, Deck or Graveyard") is { } card)
        {
            bool fromDeck = card.Loc == Location.Deck;
            engine.SpecialSummon(card, link.Player, Position.FaceUpAttack);
            if (fromDeck)
            {
                engine.ShuffleDeck(link.Player);
            }
        }
    }

    private static IEnumerable<CardInstance> Candidates(DuelState state, int player)
    {
        PlayerState p = state.Player(player);
        return p.Hand.Concat(p.Deck).Concat(p.Graveyard).Where(c => c.IsMonster && c.Def.Name == DarkMagician);
    }
}

/// <summary>Tribe-Infecting Virus: discard 1 card and declare 1 Type (by pointing at a face-up monster of that Type) to destroy all face-up monsters of that Type.</summary>
public sealed class TribeInfectingVirusEffect : EffectBase
{
    public const string EffectId = "tribe_infecting_virus";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Ignition;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return source.IsOnField && state.Player(source.Controller).Hand.Count > 0 && Field.Monsters(state).Any(m => m.IsFaceUp);
    }

    public override IReadOnlyList<Choice> Costs(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        // One representative per Type stands for the declaration.
        var representatives = Field.Monsters(state).Where(m => m.IsFaceUp).GroupBy(m => m.Def.Monster!.Type, StringComparer.Ordinal).Select(g => g.First());
        return new[]
        {
            new Choice("Discard 1 card", Field.Ids(state.Player(source.Controller).Hand), 1, 1),
            new Choice("Declare 1 Type: choose a face-up monster of that Type", Field.Ids(representatives), 1, 1),
        };
    }

    public override void PayCosts(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (engine.State.Find(link.Costs[0][0]) is { Loc: Location.Hand } card)
        {
            engine.Discard(card);
        }
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        string? type = engine.State.Find(link.Costs[1][0])?.Def.Monster?.Type;
        if (type is null)
        {
            return;
        }

        foreach (CardInstance monster in Field.Monsters(engine.State).Where(m => m.IsFaceUp && m.Def.Monster!.Type == type).ToList())
        {
            engine.Destroy(monster, DestroyReason.Effect);
        }
    }
}

/// <summary>Sinister Serpent: during its owner's Standby Phase, if it is in the Graveyard, you may return it to the hand.</summary>
public sealed class SinisterSerpentEffect : EffectBase
{
    public const string EffectId = "sinister_serpent";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Trigger;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override TriggerWindow? Trigger => TriggerWindow.Standby;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return source.Loc == Location.Graveyard && state.TurnPlayer == source.Owner;
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (link.Source.Loc == Location.Graveyard)
        {
            engine.ReturnToHand(link.Source);
        }
    }
}

/// <summary>Tsukuyomi: a Spirit; when Normal Summoned, Flip Summoned or flipped by battle, target 1 face-up monster on the field and flip it face-down.</summary>
public sealed class TsukuyomiEffect : EffectBase
{
    public const string EffectId = "tsukuyomi";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Trigger;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool IsMandatory => true;

    public override TriggerWindow? Trigger => TriggerWindow.OnSummon;

    /// <summary>A Flip Summon fires both windows; the <see cref="TriggerWindow.OnFlip"/> of a Flip Summon is ignored in <see cref="CanActivate"/> so the effect fires once.</summary>
    public override bool FiresIn(TriggerWindow window) => window is TriggerWindow.OnSummon or TriggerWindow.OnFlip;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        bool flipSummon = context.Trigger == TriggerWindow.OnFlip && context.Summon == SummonKind.Flip;
        return !flipSummon && source.IsOnField && source.IsFaceUp && Field.Monsters(state).Any(m => m.IsFaceUp);
    }

    public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new[] { new Choice("Flip 1 face-up monster into face-down Defense Position", Field.Ids(Field.Monsters(state).Where(m => m.IsFaceUp)), 1, 1) };
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (link.Target is { } id && Field.OnField(engine.State, id) is { IsFaceUp: true } target)
        {
            engine.FlipFaceDown(target);
        }
    }
}
