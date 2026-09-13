using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Effects;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Tests;

/// <summary>
/// Stub effects that exercise every timing the engine offers (issue #54):
/// one per speed, kind and trigger window. Real card effects arrive with
/// issues #55–#58; these stay test-only.
/// </summary>
internal static class TestEffects
{
    public static EffectRegistry Registry()
    {
        EffectRegistry registry = EffectRegistry.CreateDefault();
        registry.Register(AttackTrap.EffectId, static () => new AttackTrap());
        registry.Register(SummonTrap.EffectId, static () => new SummonTrap());
        registry.Register(NegateTrap.EffectId, static () => new NegateTrap());
        registry.Register(BoostSpell.EffectId, static () => new BoostSpell());
        registry.Register(OptionalSummonDraw.EffectId, static () => new OptionalSummonDraw());
        registry.Register(BattleDestroyedDraw.EffectId, static () => new BattleDestroyedDraw());
        registry.Register(FlipDraw.EffectId, static () => new FlipDraw());
        registry.Register(IgnitionDestroy.EffectId, static () => new IgnitionDestroy());
        registry.Register(FieldToGraveDraw.EffectId, static () => new FieldToGraveDraw());
        registry.Register(StandbyDraw.EffectId, static () => new StandbyDraw());
        return registry;
    }

    /// <summary>Sakuretsu-style Normal Trap: when an opponent's monster declares an attack, destroy it.</summary>
    public sealed class AttackTrap : EffectBase
    {
        public const string EffectId = "test_attack_trap";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Activation;

        public override SpellSpeed Speed => SpellSpeed.Two;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) =>
            state.Window == Window.AttackDeclared && state.Find(state.Attacker!.Value) is { } attacker && attacker.Controller != source.Controller;

        public override void Resolve(DuelEngine engine, ChainLink link)
        {
            if (engine.State.Attacker is { } id && engine.State.Find(id) is { } attacker)
            {
                engine.Destroy(attacker, DestroyReason.Effect);
            }
        }
    }

    /// <summary>Trap Hole-style Normal Trap: when the opponent Summons a monster, destroy it.</summary>
    public sealed class SummonTrap : EffectBase
    {
        public const string EffectId = "test_summon_trap";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Activation;

        public override SpellSpeed Speed => SpellSpeed.Two;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) =>
            state.Window == Window.Summon && state.Find(state.WindowCard!.Value) is { IsOnField: true } summoned && summoned.Controller != source.Controller;

        public override void Resolve(DuelEngine engine, ChainLink link)
        {
            if (engine.State.WindowCard is { } id && engine.State.Find(id) is { } summoned)
            {
                engine.Destroy(summoned, DestroyReason.Effect);
            }
        }
    }

    /// <summary>Counter Trap: negate the opponent's last chain link.</summary>
    public sealed class NegateTrap : EffectBase
    {
        public const string EffectId = "test_negate_trap";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Activation;

        public override SpellSpeed Speed => SpellSpeed.Three;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) =>
            state.Chain.Count > 0 && state.Chain[^1].Player != source.Controller;

        public override void Resolve(DuelEngine engine, ChainLink link)
        {
            // The link below this one is the last remaining link while this one resolves.
            if (engine.State.Chain.Count > 0)
            {
                engine.Negate(engine.State.Chain[^1]);
            }
        }
    }

    /// <summary>Rush Recklessly-style Quick-Play Spell: target a face-up monster you control, it gains 700 ATK (for the test, permanently).</summary>
    public sealed class BoostSpell : EffectBase
    {
        public const string EffectId = "test_boost";

        public const int Amount = 700;

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Activation;

        public override SpellSpeed Speed => SpellSpeed.Two;

        public override bool UsableInDamageStep => true;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) =>
            Candidates(state, source).Any();

        public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source) =>
            new[] { new Choice("Choose a face-up monster you control", Candidates(state, source).Select(m => m.Id).ToList(), 1, 1) };

        public override void Resolve(DuelEngine engine, ChainLink link)
        {
            if (link.Target is { } id && engine.State.Find(id) is { IsOnField: true } target)
            {
                target.AtkMod += Amount;
            }
        }

        private static IEnumerable<CardInstance> Candidates(DuelState state, CardInstance source) =>
            state.Player(source.Controller).Monsters.Where(m => m.IsFaceUp);
    }

    /// <summary>Optional Trigger: when this card is Summoned, you may draw 1 card.</summary>
    public sealed class OptionalSummonDraw : EffectBase
    {
        public const string EffectId = "test_optional_summon_draw";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Trigger;

        public override SpellSpeed Speed => SpellSpeed.One;

        public override TriggerWindow? Trigger => TriggerWindow.OnSummon;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) => source.IsOnField;

        public override void Resolve(DuelEngine engine, ChainLink link) => engine.Draw(link.Player, 1);
    }

    /// <summary>Mandatory Trigger: when this card is destroyed by battle and sent to the Graveyard, draw 1 card.</summary>
    public sealed class BattleDestroyedDraw : EffectBase
    {
        public const string EffectId = "test_battle_destroyed_draw";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Trigger;

        public override SpellSpeed Speed => SpellSpeed.One;

        public override bool IsMandatory => true;

        public override TriggerWindow? Trigger => TriggerWindow.OnDestroyedByBattle;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) => source.Loc == Location.Graveyard;

        public override void Resolve(DuelEngine engine, ChainLink link) => engine.Draw(link.Player, 1);
    }

    /// <summary>Mandatory Flip effect: draw 1 card.</summary>
    public sealed class FlipDraw : EffectBase
    {
        public const string EffectId = "test_flip_draw";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Flip;

        public override SpellSpeed Speed => SpellSpeed.One;

        public override bool IsMandatory => true;

        public override TriggerWindow? Trigger => TriggerWindow.OnFlip;

        public override void Resolve(DuelEngine engine, ChainLink link) => engine.Draw(link.Player, 1);
    }

    /// <summary>Ignition effect with a cost and a target: discard 1 card, then destroy 1 monster the opponent controls.</summary>
    public sealed class IgnitionDestroy : EffectBase
    {
        public const string EffectId = "test_ignition_destroy";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Ignition;

        public override SpellSpeed Speed => SpellSpeed.One;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) =>
            state.Player(source.Controller).Hand.Count > 0 && state.Opponent(source.Controller).MonsterCount > 0;

        public override IReadOnlyList<Choice> Costs(DuelState state, CardInstance source) =>
            new[] { new Choice("Discard 1 card", state.Player(source.Controller).Hand.Select(c => c.Id).ToList(), 1, 1) };

        public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source) =>
            new[] { new Choice("Destroy 1 monster your opponent controls", state.Opponent(source.Controller).Monsters.Select(m => m.Id).ToList(), 1, 1) };

        public override void PayCosts(DuelEngine engine, ChainLink link)
        {
            foreach (Guid id in link.Costs[0])
            {
                engine.SendToGraveyard(engine.State.Find(id)!);
            }
        }

        public override void Resolve(DuelEngine engine, ChainLink link)
        {
            if (link.Target is { } id && engine.State.Find(id) is { IsOnField: true } target)
            {
                engine.Destroy(target, DestroyReason.Effect);
            }
        }
    }

    /// <summary>Sangan-style mandatory Trigger: when this card is sent from the field to the Graveyard, draw 1 card.</summary>
    public sealed class FieldToGraveDraw : EffectBase
    {
        public const string EffectId = "test_field_to_grave_draw";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Trigger;

        public override SpellSpeed Speed => SpellSpeed.One;

        public override bool IsMandatory => true;

        public override TriggerWindow? Trigger => TriggerWindow.OnSentToGrave;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) =>
            source.Loc == Location.Graveyard && context.From == Location.MonsterZone;

        public override void Resolve(DuelEngine engine, ChainLink link) => engine.Draw(link.Player, 1);
    }

    /// <summary>Mandatory Trigger: during each Standby Phase, its controller draws 1 card.</summary>
    public sealed class StandbyDraw : EffectBase
    {
        public const string EffectId = "test_standby_draw";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Trigger;

        public override SpellSpeed Speed => SpellSpeed.One;

        public override bool IsMandatory => true;

        public override TriggerWindow? Trigger => TriggerWindow.Standby;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) => source.IsOnField && source.IsFaceUp;

        public override void Resolve(DuelEngine engine, ChainLink link) => engine.Draw(link.Player, 1);
    }
}
