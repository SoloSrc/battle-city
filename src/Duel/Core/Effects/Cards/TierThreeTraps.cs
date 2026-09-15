using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects.Cards;

/// <summary>Ring of Destruction: target 1 face-up monster and destroy it; both players take damage equal to its ATK.</summary>
public sealed class RingOfDestructionEffect : EffectBase
{
    public const string EffectId = "ring_of_destruction";

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
        return new[] { new Choice("Destroy 1 face-up monster; both players take damage equal to its ATK", Field.Ids(Field.Monsters(state).Where(m => m.IsFaceUp)), 1, 1) };
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (link.Target is not { } id || Field.OnField(engine.State, id) is not { IsFaceUp: true } target)
        {
            return;
        }

        int atk = target.Atk;
        engine.Destroy(target, DestroyReason.Effect);
        engine.InflictDamage(1 - link.Player, atk, link.Source.Id);
        engine.InflictDamage(link.Player, atk, link.Source.Id);
    }
}

/// <summary>Call of the Haunted: target 1 monster in your Graveyard and Special Summon it in Attack Position; when this card leaves the field the monster is destroyed, and when the monster leaves this card is destroyed.</summary>
public sealed class CallOfTheHauntedEffect : EffectBase
{
    public const string EffectId = "call_of_the_haunted";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.Two;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        PlayerState p = state.Player(source.Controller);
        return Field.Revivable(p).Any() && p.MonsterCount < PlayerState.ZoneCount && !p.Has(PlayerRestriction.CannotSummon);
    }

    public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return new[] { new Choice("Special Summon 1 monster from your Graveyard", Field.Ids(Field.Revivable(state.Player(source.Controller))), 1, 1) };
    }

    /// <summary>The monster is attached to the Trap like an equip, so either leaving takes the other along; a summon that fails sends the Trap to the Graveyard.</summary>
    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (link.Target is { } id && engine.State.Find(id) is { Loc: Location.Graveyard } target && engine.SpecialSummon(target, link.Player, Position.FaceUpAttack))
        {
            engine.Equip(link.Source, target);
        }
        else
        {
            engine.Destroy(link.Source, DestroyReason.Effect);
        }
    }

    public override void OnLeftField(DuelEngine engine, CardInstance source, Location from, Guid? equippedTo)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (equippedTo is { } id && engine.State.Find(id) is { IsOnField: true, IsFaceUp: true } monster)
        {
            engine.Destroy(monster, DestroyReason.Effect);
        }
    }
}

/// <summary>Bottomless Trap Hole: when the opponent Summons a monster with 1500 or more ATK, destroy and banish it.</summary>
public sealed class BottomlessTrapHoleEffect : EffectBase
{
    public const string EffectId = "bottomless_trap_hole";

    public const int MinimumAtk = 1500;

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.Two;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return state.Window == Window.Summon
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
            engine.Emit(new MonsterDestroyed(summoned.Controller, summoned.Id, summoned.Def.Id, DestroyReason.Effect));
            engine.Banish(summoned);
        }
    }
}

/// <summary>Waboku: this turn its controller takes no battle damage and their monsters cannot be destroyed by battle.</summary>
public sealed class WabokuEffect : EffectBase
{
    public const string EffectId = "waboku";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.Two;

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        int turn = engine.State.TurnNumber;
        engine.AddModifier(Modifier.OnPlayer(ModifierKind.NoBattleDamage, link.Source.Id, link.Player, 0, turn));
        engine.AddModifier(Modifier.OnPlayer(ModifierKind.CannotBeDestroyedByBattle, link.Source.Id, link.Player, 0, turn));
    }
}
