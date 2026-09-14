using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects.Cards;

/// <summary>Zaborg the Thunder Monarch: when Tribute Summoned, target 1 monster on the field and destroy it.</summary>
public sealed class ZaborgEffect : EffectBase
{
    public const string EffectId = "zaborg_the_thunder_monarch";

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
        return context.Summon == SummonKind.Tribute && source.IsOnField && Field.Monsters(state).Any();
    }

    public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new[] { new Choice("Destroy 1 monster on the field", Field.Ids(Field.Monsters(state)), 1, 1) };
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

/// <summary>Mobius the Frost Monarch: when Tribute Summoned, you may target up to 2 Spell or Trap Cards on the field and destroy them.</summary>
public sealed class MobiusEffect : EffectBase
{
    public const string EffectId = "mobius_the_frost_monarch";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Trigger;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override TriggerWindow? Trigger => TriggerWindow.OnSummon;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        return context.Summon == SummonKind.Tribute && source.IsOnField && Field.SpellTraps(state).Any();
    }

    public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new[] { new Choice("Destroy up to 2 Spell or Trap Cards on the field", Field.Ids(Field.SpellTraps(state)), 1, 2) };
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        foreach (Guid id in link.Targets[0])
        {
            if (Field.OnField(engine.State, id) is { } target)
            {
                engine.Destroy(target, DestroyReason.Effect);
            }
        }
    }
}

/// <summary>Exiled Force: tribute this card as a cost; target 1 monster on the field and destroy it.</summary>
public sealed class ExiledForceEffect : EffectBase
{
    public const string EffectId = "exiled_force";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Ignition;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return source.IsOnField && Field.Monsters(state).Any(m => m != source);
    }

    public override void PayCosts(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        engine.SendToGraveyard(link.Source);
    }

    public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new[] { new Choice("Destroy 1 monster on the field", Field.Ids(Field.Monsters(state).Where(m => m != source)), 1, 1) };
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
