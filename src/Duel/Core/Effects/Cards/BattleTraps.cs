using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects.Cards;

/// <summary>A Normal Trap that answers an attack declared by an opponent's monster.</summary>
public abstract class AttackResponseTrap : EffectBase
{
    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.Two;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return state.Window == Window.AttackDeclared && state.AttackingMonster is { } attacker && attacker.Controller != source.Controller;
    }
}

/// <summary>Mirror Force: when an opponent's monster declares an attack, destroy all Attack Position monsters the opponent controls.</summary>
public sealed class MirrorForceEffect : AttackResponseTrap
{
    public const string EffectId = "mirror_force";

    public override string Id => EffectId;

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        foreach (CardInstance monster in engine.State.Opponent(link.Player).Monsters.Where(m => m.IsInAttackPosition).ToList())
        {
            engine.Destroy(monster, DestroyReason.Effect);
        }
    }
}

/// <summary>Sakuretsu Armor: when an opponent's monster declares an attack, destroy the attacking monster.</summary>
public sealed class SakuretsuArmorEffect : AttackResponseTrap
{
    public const string EffectId = "sakuretsu_armor";

    public override string Id => EffectId;

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (engine.State.AttackingMonster is { } attacker)
        {
            engine.Destroy(attacker, DestroyReason.Effect);
        }
    }
}

/// <summary>Widespread Ruin: when an opponent's monster declares an attack, destroy the Attack Position monster the opponent controls with the highest ATK.</summary>
public sealed class WidespreadRuinEffect : AttackResponseTrap
{
    public const string EffectId = "widespread_ruin";

    public override string Id => EffectId;

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        List<CardInstance> strongest = Field.Extremes(engine.State.Opponent(link.Player).Monsters.Where(m => m.IsInAttackPosition), m => m.Atk, highest: true);
        if (Field.PickOne(engine, link, strongest, "Destroy the Attack Position monster with the highest ATK") is { } target)
        {
            engine.Destroy(target, DestroyReason.Effect);
        }
    }
}

/// <summary>Torrential Tribute: when a monster is Summoned, destroy all monsters on the field.</summary>
public sealed class TorrentialTributeEffect : EffectBase
{
    public const string EffectId = "torrential_tribute";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.Two;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return state.Window == Window.Summon && state.WindowCard is { } id && Field.OnField(state, id) is not null;
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        foreach (CardInstance monster in Field.Monsters(engine.State).ToList())
        {
            engine.Destroy(monster, DestroyReason.Effect);
        }
    }
}

/// <summary>Trap Hole: when the opponent Normal or Flip Summons a monster with 1000 or more ATK, destroy it.</summary>
public sealed class TrapHoleEffect : EffectBase
{
    public const string EffectId = "trap_hole";

    public const int MinimumAtk = 1000;

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.Two;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return state.Window == Window.Summon
            && state.LastSummon is SummonKind.Normal or SummonKind.Tribute or SummonKind.Flip
            && state.WindowCard is { } id
            && Field.OnField(state, id) is { } summoned
            && summoned.Controller != source.Controller
            && summoned.Atk >= MinimumAtk;
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (engine.State.WindowCard is { } id && Field.OnField(engine.State, id) is { } summoned)
        {
            engine.Destroy(summoned, DestroyReason.Effect);
        }
    }
}
