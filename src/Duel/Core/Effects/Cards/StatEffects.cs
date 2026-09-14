using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects.Cards;

/// <summary>Axe of Despair: the equipped monster gains 1000 ATK.</summary>
public sealed class AxeOfDespairEffect : EffectBase
{
    public const string EffectId = "axe_of_despair";

    public const int Amount = 1000;

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return Field.Monsters(state).Any(m => m.IsFaceUp);
    }

    public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new[] { new Choice("Equip to 1 face-up monster", Field.Ids(Field.Monsters(state).Where(m => m.IsFaceUp)), 1, 1) };
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (link.Target is { } id && Field.OnField(engine.State, id) is { } target)
        {
            engine.Equip(link.Source, target);
        }
    }

    public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.EquippedTo is { } id)
        {
            yield return Modifier.OnCard(ModifierKind.Atk, source.Id, id, Amount);
        }
    }
}

/// <summary>Axe of Despair's second effect: when it is sent from the field to the Graveyard, you may tribute 1 monster to return it to the top of your Deck.</summary>
public sealed class AxeOfDespairRecycleEffect : EffectBase
{
    public const string EffectId = "axe_of_despair_recycle";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Trigger;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override TriggerWindow? Trigger => TriggerWindow.OnSentToGrave;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        return source.Loc == Location.Graveyard && context.From == Location.SpellTrapZone && state.Player(source.Owner).Monsters.Any(m => !m.Has(Restriction.CannotBeTributed));
    }

    public override IReadOnlyList<Choice> Costs(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return new[] { new Choice("Tribute 1 monster", Field.Ids(state.Player(source.Owner).Monsters.Where(m => !m.Has(Restriction.CannotBeTributed))), 1, 1) };
    }

    public override void PayCosts(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (Field.OnField(engine.State, link.Costs[0][0]) is { } tribute)
        {
            engine.SendToGraveyard(tribute);
        }
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (link.Source.Loc == Location.Graveyard)
        {
            engine.ReturnToDeck(link.Source, top: true);
        }
    }
}

/// <summary>Book of Moon: Quick-Play; target 1 face-up monster on the field and flip it into face-down Defense Position.</summary>
public sealed class BookOfMoonEffect : EffectBase
{
    public const string EffectId = "book_of_moon";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.Two;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return Field.Monsters(state).Any(m => m.IsFaceUp);
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

/// <summary>Berserk Gorilla: must attack if able; destroyed when switched to Defense Position. Both are flags the rules read.</summary>
public sealed class BerserkGorillaEffect : EffectBase
{
    public const string EffectId = "berserk_gorilla";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Continuous;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(source);
        yield return Modifier.OnCard(ModifierKind.MustAttack, source.Id, source.Id);
        yield return Modifier.OnCard(ModifierKind.DestroyedInDefensePosition, source.Id, source.Id);
    }
}

/// <summary>Goblin Attack Force and Giant Orc: after attacking, switched to Defense Position at the end of the Battle Phase and locked there until the end of the controller's next turn.</summary>
public sealed class DefenseAfterAttackEffect : EffectBase
{
    public const string GoblinAttackForceId = "goblin_attack_force";

    public const string GiantOrcId = "giant_orc";

    public DefenseAfterAttackEffect(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        Id = id;
    }

    public override string Id { get; }

    public override EffectKind Kind => EffectKind.Continuous;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(source);
        yield return Modifier.OnCard(ModifierKind.DefenseAfterAttack, source.Id, source.Id);
    }
}
