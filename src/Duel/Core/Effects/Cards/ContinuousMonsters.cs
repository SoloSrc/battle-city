using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects.Cards;

/// <summary>Enraged Battle Ox: Beast, Beast-Warrior and Winged Beast monsters its controller controls inflict piercing battle damage.</summary>
public sealed class EnragedBattleOxEffect : EffectBase
{
    public const string EffectId = "enraged_battle_ox";

    private static readonly string[] _types = { "Beast", "Beast-Warrior", "Winged Beast" };

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Continuous;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        foreach (CardInstance monster in Field.FaceUpMonsters(state.Player(source.Controller)).Where(m => Field.HasType(m, _types)))
        {
            yield return Modifier.OnCard(ModifierKind.Piercing, source.Id, monster.Id);
        }
    }
}

/// <summary>Jinzo: Trap Cards cannot be activated and their effects are negated.</summary>
public sealed class JinzoEffect : EffectBase
{
    public const string EffectId = "jinzo";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Continuous;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(source);
        yield return Modifier.Global(ModifierKind.TrapsNegated, source.Id);
    }
}

/// <summary>Blade Knight: gains 400 ATK while its controller has 1 or fewer cards in hand; while it is their only monster, the Flip Effects of monsters it destroys by battle do not fire.</summary>
public sealed class BladeKnightEffect : EffectBase
{
    public const string EffectId = "blade_knight";

    public const int Amount = 400;

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Continuous;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        PlayerState p = state.Player(source.Controller);
        if (p.Hand.Count <= 1)
        {
            yield return Modifier.OnCard(ModifierKind.Atk, source.Id, source.Id, Amount);
        }

        if (p.MonsterCount == 1)
        {
            yield return Modifier.OnCard(ModifierKind.NegatesFlipEffectsOfDestroyed, source.Id, source.Id);
        }
    }
}

/// <summary>Command Knight: Warrior monsters its controller controls gain 400 ATK; it cannot be attacked while they control another monster.</summary>
public sealed class CommandKnightEffect : EffectBase
{
    public const string EffectId = "command_knight";

    public const int Amount = 400;

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Continuous;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        PlayerState p = state.Player(source.Controller);
        foreach (CardInstance monster in Field.FaceUpMonsters(p).Where(Field.IsWarrior))
        {
            yield return Modifier.OnCard(ModifierKind.Atk, source.Id, monster.Id, Amount);
        }

        if (p.MonsterCount > 1)
        {
            yield return Modifier.OnCard(ModifierKind.CannotBeAttacked, source.Id, source.Id);
        }
    }
}

/// <summary>Marauding Captain: when Normal Summoned, you may Special Summon 1 Level 4 or lower monster from your hand; the opponent cannot attack other Warriors you control.</summary>
public sealed class MaraudingCaptainEffect : EffectBase
{
    public const string EffectId = "marauding_captain";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Trigger;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override TriggerWindow? Trigger => TriggerWindow.OnSummon;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        PlayerState p = state.Player(source.Controller);
        return context.Summon == SummonKind.Normal && source.IsOnField && Candidates(p).Any() && p.MonsterCount < PlayerState.ZoneCount && !p.Has(PlayerRestriction.CannotSummon);
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (Field.PickOne(engine, link, Candidates(engine.State.Player(link.Player)).ToList(), "Special Summon 1 Level 4 or lower monster from your hand") is { } card)
        {
            engine.SpecialSummon(card, link.Player, Position.FaceUpAttack);
        }
    }

    public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        foreach (CardInstance monster in Field.FaceUpMonsters(state.Player(source.Controller)).Where(m => m != source && Field.IsWarrior(m)))
        {
            yield return Modifier.OnCard(ModifierKind.CannotBeAttacked, source.Id, monster.Id);
        }
    }

    private static IEnumerable<CardInstance> Candidates(PlayerState p) =>
        p.Hand.Where(c => c.Def.Kind == CardKind.Monster && c.Def.Monster!.Level <= 4 && c.Def.Monster.Category != MonsterCategory.Spirit);
}

/// <summary>Asura Priest: a Spirit that can attack every monster the opponent controls once each.</summary>
public sealed class AsuraPriestEffect : EffectBase
{
    public const string EffectId = "asura_priest";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Continuous;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(source);
        yield return Modifier.OnCard(ModifierKind.AttacksEveryMonster, source.Id, source.Id);
    }
}

/// <summary>Mystic Swordsman LV2: cannot be Set; destroys a face-down Defense Position monster it attacks at the start of the Damage Step without flipping it.</summary>
public sealed class MysticSwordsmanLv2Effect : EffectBase
{
    public const string EffectId = "mystic_swordsman_lv2";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Continuous;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override SummonLimit Limits => SummonLimit.CannotBeSet;

    public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(source);
        yield return Modifier.OnCard(ModifierKind.DestroysFaceDownTargets, source.Id, source.Id);
    }
}

/// <summary>Reaper on the Nightmare: attacks directly, cannot be destroyed by battle, is destroyed when targeted; when it inflicts battle damage by a direct attack, the opponent discards 1 random card.</summary>
public sealed class ReaperOnTheNightmareEffect : EffectBase
{
    public const string EffectId = "reaper_on_the_nightmare";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Trigger;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool IsMandatory => true;

    public override TriggerWindow? Trigger => TriggerWindow.OnBattleDamage;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        return context.Battled is null && source.IsOnField && state.Opponent(source.Controller).Hand.Count > 0;
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        engine.DiscardRandom(1 - link.Player, 1);
    }

    public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(source);
        yield return Modifier.OnCard(ModifierKind.CanAttackDirectly, source.Id, source.Id);
        yield return Modifier.OnCard(ModifierKind.CannotBeDestroyedByBattle, source.Id, source.Id);
        yield return Modifier.OnCard(ModifierKind.DestroyedWhenTargeted, source.Id, source.Id);
    }
}

/// <summary>Dark Balter the Terrible: pay 1000 Life Points to negate the activation of a Normal Spell Card; the effects of monsters it destroys by battle are negated.</summary>
public sealed class DarkBalterEffect : EffectBase
{
    public const string EffectId = "dark_balter_the_terrible";

    public const int Cost = 1000;

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Quick;

    public override SpellSpeed Speed => SpellSpeed.Two;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return source.IsOnField && state.Player(source.Controller).LifePoints >= Cost && state.Chain.Count > 0 && IsNormalSpell(state.Chain[^1]);
    }

    public override void PayCosts(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        engine.PayLifePoints(link.Player, Cost, link.Source.Id);
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        // The chain resolves last in, first out: the link below this one is the Spell it answered.
        if (engine.State.Chain.Count > 0 && IsNormalSpell(engine.State.Chain[^1]))
        {
            engine.Negate(engine.State.Chain[^1]);
        }
    }

    public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(source);
        yield return Modifier.OnCard(ModifierKind.NegatesEffectsOfDestroyed, source.Id, source.Id);
    }

    private static bool IsNormalSpell(ChainLink link) =>
        !link.Negated && link.Effect.Kind == EffectKind.Activation && link.Source.Def.Spell?.Subtype == SpellSubtype.Normal;
}
