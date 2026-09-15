using System;
using System.Collections.Generic;
using BattleCity.Duel.Core.Effects.Cards;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects;

/// <summary>
/// Maps effect ids from card data to <see cref="IEffect"/> factories
/// (systems.md §5.3). The card loader refuses ids that are not registered,
/// so an unimplemented tier 2+ card fails at load time rather than mid-duel.
/// </summary>
public sealed class EffectRegistry
{
    private readonly Dictionary<string, Func<IEffect>> _factories = new(StringComparer.Ordinal);

    /// <summary>Every effect implemented so far: tier 1 (Pot of Greed), tier 2 (issue #56, 27 cards), tier 3 (issue #57, 35 cards) and tier 4 (issue #58, 4 cards): the whole pool of systems.md §5.6.</summary>
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

        // Tier 3: continuous monsters.
        registry.Register(EnragedBattleOxEffect.EffectId, static () => new EnragedBattleOxEffect());
        registry.Register(JinzoEffect.EffectId, static () => new JinzoEffect());
        registry.Register(BladeKnightEffect.EffectId, static () => new BladeKnightEffect());
        registry.Register(CommandKnightEffect.EffectId, static () => new CommandKnightEffect());
        registry.Register(MaraudingCaptainEffect.EffectId, static () => new MaraudingCaptainEffect());
        registry.Register(AsuraPriestEffect.EffectId, static () => new AsuraPriestEffect());
        registry.Register(MysticSwordsmanLv2Effect.EffectId, static () => new MysticSwordsmanLv2Effect());
        registry.Register(ReaperEffect.ReaperOnTheNightmareId, static () => new ReaperEffect(ReaperEffect.ReaperOnTheNightmareId, attacksDirectly: true));
        registry.Register(DarkBalterEffect.EffectId, static () => new DarkBalterEffect());

        // Tier 3: battle triggers.
        registry.Register(DonZaloogEffect.EffectId, static () => new DonZaloogEffect());
        registry.Register(DDWarriorLadyEffect.EffectId, static () => new DDWarriorLadyEffect());
        registry.Register(DDAssailantEffect.EffectId, static () => new DDAssailantEffect());
        registry.Register(AirknightParshathEffect.EffectId, static () => new AirknightParshathEffect());
        registry.Register(KycooEffect.EffectId, static () => new KycooEffect());
        registry.Register(RecruiterEffect.MysticTomatoId, static () => new RecruiterEffect(RecruiterEffect.MysticTomatoId, MonsterAttribute.Dark));
        registry.Register(RecruiterEffect.ShiningAngelId, static () => new RecruiterEffect(RecruiterEffect.ShiningAngelId, MonsterAttribute.Light));

        // Tier 3: counters, summons and the Graveyard.
        registry.Register(BreakerEffect.EffectId, static () => new BreakerEffect());
        registry.Register(BreakerDestroyEffect.EffectId, static () => new BreakerDestroyEffect());
        registry.Register(ChaosSummonEffect.ChaosSorcererId, static () => new ChaosSummonEffect(ChaosSummonEffect.ChaosSorcererId));
        registry.Register(ChaosSorcererBanishEffect.EffectId, static () => new ChaosSorcererBanishEffect());
        registry.Register(SkilledDarkMagicianCounterEffect.EffectId, static () => new SkilledDarkMagicianCounterEffect());
        registry.Register(SkilledDarkMagicianSummonEffect.EffectId, static () => new SkilledDarkMagicianSummonEffect());
        registry.Register(TribeInfectingVirusEffect.EffectId, static () => new TribeInfectingVirusEffect());
        registry.Register(SinisterSerpentEffect.EffectId, static () => new SinisterSerpentEffect());
        registry.Register(TsukuyomiEffect.EffectId, static () => new TsukuyomiEffect());

        // Tier 3: spells.
        registry.Register(DelinquentDuoEffect.EffectId, static () => new DelinquentDuoEffect());
        registry.Register(PrematureBurialEffect.EffectId, static () => new PrematureBurialEffect());
        registry.Register(SnatchStealEffect.EffectId, static () => new SnatchStealEffect());
        registry.Register(SnatchStealUpkeepEffect.EffectId, static () => new SnatchStealUpkeepEffect());
        registry.Register(NoblemanOfCrossoutEffect.EffectId, static () => new NoblemanOfCrossoutEffect());
        registry.Register(EnemyControllerEffect.EffectId, static () => new EnemyControllerEffect());
        registry.Register(ScapegoatEffect.EffectId, static () => new ScapegoatEffect());
        registry.Register(MetamorphosisEffect.EffectId, static () => new MetamorphosisEffect());
        registry.Register(SwordsOfRevealingLightEffect.EffectId, static () => new SwordsOfRevealingLightEffect());
        registry.Register(CreatureSwapEffect.EffectId, static () => new CreatureSwapEffect());

        // Tier 3: traps.
        registry.Register(RingOfDestructionEffect.EffectId, static () => new RingOfDestructionEffect());
        registry.Register(CallOfTheHauntedEffect.EffectId, static () => new CallOfTheHauntedEffect());
        registry.Register(BottomlessTrapHoleEffect.EffectId, static () => new BottomlessTrapHoleEffect());
        registry.Register(WabokuEffect.EffectId, static () => new WabokuEffect());

        // Tier 4 (issue #58).
        registry.Register(ReaperEffect.SpiritReaperId, static () => new ReaperEffect(ReaperEffect.SpiritReaperId, attacksDirectly: false));
        registry.Register(ChaosSummonEffect.BlackLusterSoldierId, static () => new ChaosSummonEffect(ChaosSummonEffect.BlackLusterSoldierId));
        registry.Register(BlackLusterSoldierBanishEffect.EffectId, static () => new BlackLusterSoldierBanishEffect());
        registry.Register(BlackLusterSoldierDoubleAttackEffect.EffectId, static () => new BlackLusterSoldierDoubleAttackEffect());
        registry.Register(ThousandEyesRestrictEffect.EffectId, static () => new ThousandEyesRestrictEffect());
        registry.Register(ThousandEyesAbsorbEffect.EffectId, static () => new ThousandEyesAbsorbEffect());
        registry.Register(CyberJarEffect.EffectId, static () => new CyberJarEffect());
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
