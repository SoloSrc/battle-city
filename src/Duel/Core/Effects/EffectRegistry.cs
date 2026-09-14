using System;
using System.Collections.Generic;
using BattleCity.Duel.Core.Effects.Cards;

namespace BattleCity.Duel.Core.Effects;

/// <summary>
/// Maps effect ids from card data to <see cref="IEffect"/> factories
/// (systems.md §5.3). The card loader refuses ids that are not registered,
/// so an unimplemented tier 2+ card fails at load time rather than mid-duel.
/// </summary>
public sealed class EffectRegistry
{
    private readonly Dictionary<string, Func<IEffect>> _factories = new(StringComparer.Ordinal);

    /// <summary>Every effect implemented so far: tier 1 (Pot of Greed) and tier 2 (issue #56, the 27 cards of systems.md §5.6).</summary>
    public static EffectRegistry CreateDefault()
    {
        var registry = new EffectRegistry();
        registry.Register(PotOfGreedEffect.EffectId, static () => new PotOfGreedEffect());

        // Tier 2: draw and discard.
        registry.Register(GracefulCharityEffect.EffectId, static () => new GracefulCharityEffect());
        registry.Register(MorphingJarEffect.EffectId, static () => new MorphingJarEffect());

        // Tier 2: destroy.
        registry.Register(FissureEffect.EffectId, static () => new FissureEffect());
        registry.Register(SmashingGroundEffect.EffectId, static () => new SmashingGroundEffect());
        registry.Register(HeavyStormEffect.EffectId, static () => new HeavyStormEffect());
        registry.Register(MysticalSpaceTyphoonEffect.EffectId, static () => new MysticalSpaceTyphoonEffect());
        registry.Register(LightningVortexEffect.EffectId, static () => new LightningVortexEffect());
        registry.Register(DustTornadoEffect.EffectId, static () => new DustTornadoEffect());
        registry.Register(MirrorForceEffect.EffectId, static () => new MirrorForceEffect());
        registry.Register(SakuretsuArmorEffect.EffectId, static () => new SakuretsuArmorEffect());
        registry.Register(WidespreadRuinEffect.EffectId, static () => new WidespreadRuinEffect());
        registry.Register(TorrentialTributeEffect.EffectId, static () => new TorrentialTributeEffect());
        registry.Register(TrapHoleEffect.EffectId, static () => new TrapHoleEffect());
        registry.Register(ZaborgEffect.EffectId, static () => new ZaborgEffect());
        registry.Register(MobiusEffect.EffectId, static () => new MobiusEffect());
        registry.Register(ExiledForceEffect.EffectId, static () => new ExiledForceEffect());

        // Tier 2: search and recover.
        registry.Register(SanganEffect.EffectId, static () => new SanganEffect());
        registry.Register(ReinforcementOfTheArmyEffect.EffectId, static () => new ReinforcementOfTheArmyEffect());
        registry.Register(WarriorReturningAliveEffect.EffectId, static () => new WarriorReturningAliveEffect());
        registry.Register(GravekeepersSpyEffect.EffectId, static () => new GravekeepersSpyEffect());
        registry.Register(MagicianOfFaithEffect.EffectId, static () => new MagicianOfFaithEffect());

        // Tier 2: stats and position.
        registry.Register(AxeOfDespairEffect.EffectId, static () => new AxeOfDespairEffect());
        registry.Register(AxeOfDespairRecycleEffect.EffectId, static () => new AxeOfDespairRecycleEffect());
        registry.Register(BookOfMoonEffect.EffectId, static () => new BookOfMoonEffect());
        registry.Register(BerserkGorillaEffect.EffectId, static () => new BerserkGorillaEffect());
        registry.Register(DefenseAfterAttackEffect.GoblinAttackForceId, static () => new DefenseAfterAttackEffect(DefenseAfterAttackEffect.GoblinAttackForceId));
        registry.Register(DefenseAfterAttackEffect.GiantOrcId, static () => new DefenseAfterAttackEffect(DefenseAfterAttackEffect.GiantOrcId));
        registry.Register(GravekeepersGuardEffect.EffectId, static () => new GravekeepersGuardEffect());
        return registry;
    }

    public IEnumerable<string> Ids => _factories.Keys;

    public void Register(string id, Func<IEffect> factory)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(factory);
        _factories[id] = factory;
    }

    public bool Contains(string id) => _factories.ContainsKey(id);

    public IEffect Create(string id)
    {
        if (!_factories.TryGetValue(id, out Func<IEffect>? factory))
        {
            throw new KeyNotFoundException($"No effect registered for id '{id}'.");
        }

        return factory();
    }
}
