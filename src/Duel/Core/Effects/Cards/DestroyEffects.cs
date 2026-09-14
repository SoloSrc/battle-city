using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects.Cards;

/// <summary>Fissure: destroy the face-up monster the opponent controls with the lowest ATK; the activating player breaks a tie.</summary>
public sealed class FissureEffect : EffectBase
{
    public const string EffectId = "fissure";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return Field.FaceUpMonsters(state.Opponent(source.Controller)).Any();
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        List<CardInstance> weakest = Field.Extremes(Field.FaceUpMonsters(engine.State.Opponent(link.Player)), m => m.Atk, highest: false);
        if (Field.PickOne(engine, link, weakest, "Destroy the monster with the lowest ATK") is { } target)
        {
            engine.Destroy(target, DestroyReason.Effect);
        }
    }
}

/// <summary>Smashing Ground: destroy the face-up monster the opponent controls with the highest DEF; the activating player breaks a tie.</summary>
public sealed class SmashingGroundEffect : EffectBase
{
    public const string EffectId = "smashing_ground";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return Field.FaceUpMonsters(state.Opponent(source.Controller)).Any();
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        List<CardInstance> sturdiest = Field.Extremes(Field.FaceUpMonsters(engine.State.Opponent(link.Player)), m => m.DefValue, highest: true);
        if (Field.PickOne(engine, link, sturdiest, "Destroy the monster with the highest DEF") is { } target)
        {
            engine.Destroy(target, DestroyReason.Effect);
        }
    }
}

/// <summary>Heavy Storm: destroy all Spell and Trap Cards on the field.</summary>
public sealed class HeavyStormEffect : EffectBase
{
    public const string EffectId = "heavy_storm";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return Field.SpellTraps(state).Any(c => c != source);
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        foreach (CardInstance card in Field.SpellTraps(engine.State).Where(c => c != link.Source).ToList())
        {
            engine.Destroy(card, DestroyReason.Effect);
        }
    }
}

/// <summary>Mystical Space Typhoon: Quick-Play; target 1 Spell or Trap Card on the field and destroy it.</summary>
public sealed class MysticalSpaceTyphoonEffect : EffectBase
{
    public const string EffectId = "mystical_space_typhoon";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.Two;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return Field.SpellTraps(state).Any(c => c != source);
    }

    public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new[] { new Choice("Destroy 1 Spell or Trap Card on the field", Field.Ids(Field.SpellTraps(state).Where(c => c != source)), 1, 1) };
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

/// <summary>Lightning Vortex: discard 1 card as a cost; destroy all face-up monsters the opponent controls.</summary>
public sealed class LightningVortexEffect : EffectBase
{
    public const string EffectId = "lightning_vortex";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return state.Player(source.Controller).Hand.Any(c => c != source) && Field.FaceUpMonsters(state.Opponent(source.Controller)).Any();
    }

    public override IReadOnlyList<Choice> Costs(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return new[] { new Choice("Discard 1 card", Field.Ids(state.Player(source.Controller).Hand.Where(c => c != source)), 1, 1) };
    }

    public override void PayCosts(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        foreach (Guid id in link.Costs[0])
        {
            if (engine.State.Find(id) is { Loc: Location.Hand } card)
            {
                engine.Discard(card);
            }
        }
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        foreach (CardInstance monster in Field.FaceUpMonsters(engine.State.Opponent(link.Player)).ToList())
        {
            engine.Destroy(monster, DestroyReason.Effect);
        }
    }
}

/// <summary>Dust Tornado: target 1 Spell or Trap Card the opponent controls and destroy it; then you may Set 1 Spell or Trap Card from your hand.</summary>
public sealed class DustTornadoEffect : EffectBase
{
    public const string EffectId = "dust_tornado";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.Two;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return state.Opponent(source.Controller).SpellTrapCount > 0;
    }

    public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return new[] { new Choice("Destroy 1 Spell or Trap Card the opponent controls", Field.Ids(state.Opponent(source.Controller).SpellTraps), 1, 1) };
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (link.Stage == 0)
        {
            link.Stage = 1;
            if (link.Target is { } id && Field.OnField(engine.State, id) is { } target)
            {
                engine.Destroy(target, DestroyReason.Effect);
            }
        }

        PlayerState p = engine.State.Player(link.Player);
        if (p.SpellTrapCount >= PlayerState.ZoneCount)
        {
            return;
        }

        if (engine.Ask(link, new Choice("Set 1 Spell or Trap Card from your hand?", Field.Ids(Field.SettableInHand(p)), 0, 1)) is { Count: > 0 } chosen
            && engine.State.Find(chosen[0]) is { Loc: Location.Hand } card)
        {
            engine.SetSpellTrap(card);
        }
    }
}
