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
        registry.Register(WarriorBoost.EffectId, static () => new WarriorBoost());
        registry.Register(EquipBoost.EffectId, static () => new EquipBoost());
        registry.Register(StealEquip.EffectId, static () => new StealEquip());
        registry.Register(TempSteal.EffectId, static () => new TempSteal());
        registry.Register(TokenMaker.EffectId, static () => new TokenMaker());
        registry.Register(CounterCharge.EffectId, static () => new CounterCharge());
        registry.Register(CounterDestroy.EffectId, static () => new CounterDestroy());
        registry.Register(LifeCostDraw.EffectId, static () => new LifeCostDraw());
        registry.Register(RandomDiscard.EffectId, static () => new RandomDiscard());
        registry.Register(Reviver.EffectId, static () => new Reviver());
        registry.Register(FusionCall.EffectId, static () => new FusionCall());
        registry.Register(PositionLock.EffectId, static () => new PositionLock());
        registry.Register(TrapLock.EffectId, static () => new TrapLock());
        registry.Register(Shield.EffectId, static () => new Shield());
        registry.Register(Piercer.EffectId, static () => new Piercer());
        registry.Register(Reaper.EffectId, static () => new Reaper());
        registry.Register(Banisher.EffectId, static () => new Banisher());
        registry.Register(FlipDown.EffectId, static () => new FlipDown());
        registry.Register(Burial.EffectId, static () => new Burial());
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
            state.Window == Window.AttackDeclared && state.AttackingMonster is { } attacker && attacker.Controller != source.Controller;

        public override void Resolve(DuelEngine engine, ChainLink link)
        {
            if (engine.State.AttackingMonster is { } attacker)
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

    /// <summary>Rush Recklessly-style Quick-Play Spell: target a face-up monster you control, it gains 700 ATK until the End Phase.</summary>
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
                engine.AddModifier(Modifier.OnCard(ModifierKind.Atk, link.Source.Id, target.Id, Amount, engine.State.TurnNumber));
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

    // ----- Issue #55 mechanisms: modifiers, equips, tokens, control and counters -----

    /// <summary>Command Knight-style Continuous effect: Warriors its controller controls gain 400 ATK; it cannot be attacked while they control another monster.</summary>
    public sealed class WarriorBoost : EffectBase
    {
        public const string EffectId = "test_warrior_boost";

        public const int Amount = 400;

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Continuous;

        public override SpellSpeed Speed => SpellSpeed.One;

        public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
        {
            PlayerState p = state.Player(source.Controller);
            foreach (CardInstance m in p.Monsters.Where(m => m.IsFaceUp && m.Def.Monster?.Type == "Warrior"))
            {
                yield return Modifier.OnCard(ModifierKind.Atk, source.Id, m.Id, Amount);
            }

            if (p.MonsterCount > 1)
            {
                yield return Modifier.OnCard(ModifierKind.CannotBeAttacked, source.Id, source.Id);
            }
        }
    }

    /// <summary>Axe of Despair-style Equip Spell: the equipped monster gains 1000 ATK.</summary>
    public sealed class EquipBoost : EffectBase
    {
        public const string EffectId = "test_equip_boost";

        public const int Amount = 1000;

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Activation;

        public override SpellSpeed Speed => SpellSpeed.One;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) => FaceUpMonsters(state).Any();

        public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source) =>
            new[] { new Choice("Choose a face-up monster to equip", FaceUpMonsters(state).Select(m => m.Id).ToList(), 1, 1) };

        public override void Resolve(DuelEngine engine, ChainLink link)
        {
            if (link.Target is { } id && engine.State.Find(id) is { } target)
            {
                engine.Equip(link.Source, target);
            }
        }

        public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
        {
            if (source.EquippedTo is { } id)
            {
                yield return Modifier.OnCard(ModifierKind.Atk, source.Id, id, Amount);
            }
        }

        internal static IEnumerable<CardInstance> FaceUpMonsters(DuelState state) => state.Players.SelectMany(p => p.Monsters).Where(m => m.IsFaceUp);
    }

    /// <summary>Snatch Steal-style Equip Spell: take control of an opponent's face-up monster for as long as this card stays on the field.</summary>
    public sealed class StealEquip : EffectBase
    {
        public const string EffectId = "test_steal_equip";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Activation;

        public override SpellSpeed Speed => SpellSpeed.One;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) =>
            Candidates(state, source).Any() && state.Player(source.Controller).MonsterCount < PlayerState.ZoneCount;

        public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source) =>
            new[] { new Choice("Take control of a face-up monster the opponent controls", Candidates(state, source).Select(m => m.Id).ToList(), 1, 1) };

        public override void Resolve(DuelEngine engine, ChainLink link)
        {
            if (link.Target is { } id && engine.State.Find(id) is { IsOnField: true, IsFaceUp: true } target && engine.ChangeControl(target, link.Player))
            {
                engine.Equip(link.Source, target);
            }
        }

        public override void OnLeftField(DuelEngine engine, CardInstance source, Location from, Guid? equippedTo)
        {
            if (equippedTo is { } id && engine.State.Find(id) is { Loc: Location.MonsterZone } monster)
            {
                engine.ChangeControl(monster, monster.Owner);
            }
        }

        private static IEnumerable<CardInstance> Candidates(DuelState state, CardInstance source) =>
            state.Opponent(source.Controller).Monsters.Where(m => m.IsFaceUp);
    }

    /// <summary>Enemy Controller-style Quick-Play Spell: take control of an opponent's face-up monster until the End Phase.</summary>
    public sealed class TempSteal : EffectBase
    {
        public const string EffectId = "test_temp_steal";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Activation;

        public override SpellSpeed Speed => SpellSpeed.Two;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) =>
            state.Opponent(source.Controller).Monsters.Any(m => m.IsFaceUp) && state.Player(source.Controller).MonsterCount < PlayerState.ZoneCount;

        public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source) =>
            new[] { new Choice("Take control of a face-up monster until the End Phase", state.Opponent(source.Controller).Monsters.Where(m => m.IsFaceUp).Select(m => m.Id).ToList(), 1, 1) };

        public override void Resolve(DuelEngine engine, ChainLink link)
        {
            if (link.Target is { } id && engine.State.Find(id) is { IsOnField: true, IsFaceUp: true } target)
            {
                engine.ChangeControl(target, link.Player, engine.State.TurnNumber);
            }
        }
    }

    /// <summary>Scapegoat-style Quick-Play Spell: Special Summon up to 4 Sheep Tokens in Defense Position; they cannot be tributed.</summary>
    public sealed class TokenMaker : EffectBase
    {
        public const string EffectId = "test_token_maker";

        public static readonly CardDefinition Sheep = CardDefinition.Token("test_sheep_token", "Sheep Token", "Beast", MonsterAttribute.Earth, 1, 0, 0);

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Activation;

        public override SpellSpeed Speed => SpellSpeed.Two;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) =>
            state.Player(source.Controller).MonsterCount < PlayerState.ZoneCount;

        public override void Resolve(DuelEngine engine, ChainLink link)
        {
            for (int i = 0; i < 4; i++)
            {
                CardInstance? token = engine.CreateToken(link.Player, Sheep, Position.FaceUpDefense);
                if (token is null)
                {
                    return;
                }

                engine.AddModifier(Modifier.OnCard(ModifierKind.CannotBeTributed, link.Source.Id, token.Id));
            }
        }
    }

    /// <summary>Breaker-style Ignition effect, once per turn: place 1 Spell Counter on this card; each counter gives 300 ATK.</summary>
    public sealed class CounterCharge : EffectBase
    {
        public const string EffectId = "test_counter_charge";

        public const string Counter = "spell";

        public const int AtkPerCounter = 300;

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Ignition;

        public override SpellSpeed Speed => SpellSpeed.One;

        public override bool OncePerTurn => true;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) => source.IsOnField;

        public override void Resolve(DuelEngine engine, ChainLink link) => engine.AddCounter(link.Source, Counter, 1);

        public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
        {
            if (source.Counter(Counter) > 0)
            {
                yield return Modifier.OnCard(ModifierKind.Atk, source.Id, source.Id, AtkPerCounter * source.Counter(Counter));
            }
        }
    }

    /// <summary>Breaker-style Ignition effect: remove 1 Spell Counter from this card as a cost to destroy 1 Spell or Trap the opponent controls.</summary>
    public sealed class CounterDestroy : EffectBase
    {
        public const string EffectId = "test_counter_destroy";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Ignition;

        public override SpellSpeed Speed => SpellSpeed.One;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) =>
            source.IsOnField && source.Counter(CounterCharge.Counter) > 0 && state.Opponent(source.Controller).SpellTrapCount > 0;

        public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source) =>
            new[] { new Choice("Destroy 1 Spell or Trap the opponent controls", state.Opponent(source.Controller).SpellTraps.Select(c => c.Id).ToList(), 1, 1) };

        public override void PayCosts(DuelEngine engine, ChainLink link) => engine.AddCounter(link.Source, CounterCharge.Counter, -1);

        public override void Resolve(DuelEngine engine, ChainLink link)
        {
            if (link.Target is { } id && engine.State.Find(id) is { IsOnField: true } target)
            {
                engine.Destroy(target, DestroyReason.Effect);
            }
        }
    }

    /// <summary>Normal Spell with a life point cost: pay 1000 Life Points, draw 1 card.</summary>
    public sealed class LifeCostDraw : EffectBase
    {
        public const string EffectId = "test_life_cost_draw";

        public const int Cost = 1000;

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Activation;

        public override SpellSpeed Speed => SpellSpeed.One;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) =>
            state.Player(source.Controller).LifePoints >= Cost;

        public override void PayCosts(DuelEngine engine, ChainLink link) => engine.PayLifePoints(link.Player, Cost, link.Source.Id);

        public override void Resolve(DuelEngine engine, ChainLink link) => engine.Draw(link.Player, 1);
    }

    /// <summary>Normal Spell: the opponent discards 1 random card.</summary>
    public sealed class RandomDiscard : EffectBase
    {
        public const string EffectId = "test_random_discard";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Activation;

        public override SpellSpeed Speed => SpellSpeed.One;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) =>
            state.Opponent(source.Controller).Hand.Count > 0;

        public override void Resolve(DuelEngine engine, ChainLink link) => engine.DiscardRandom(1 - link.Player, 1);
    }

    /// <summary>Call of the Haunted-style Quick-Play Spell: target a monster in your Graveyard, Special Summon it in Attack Position.</summary>
    public sealed class Reviver : EffectBase
    {
        public const string EffectId = "test_reviver";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Activation;

        public override SpellSpeed Speed => SpellSpeed.Two;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) =>
            Candidates(state, source).Any() && state.Player(source.Controller).MonsterCount < PlayerState.ZoneCount;

        public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source) =>
            new[] { new Choice("Special Summon a monster from your Graveyard", Candidates(state, source).Select(c => c.Id).ToList(), 1, 1) };

        public override void Resolve(DuelEngine engine, ChainLink link)
        {
            if (link.Target is { } id && engine.State.Find(id) is { Loc: Location.Graveyard } target)
            {
                engine.SpecialSummon(target, link.Player, Position.FaceUpAttack);
            }
        }

        private static IEnumerable<CardInstance> Candidates(DuelState state, CardInstance source) =>
            state.Player(source.Controller).Graveyard.Where(c => c.IsMonster);
    }

    /// <summary>Test-only Normal Spell: Special Summon the first monster of your Fusion Deck in Attack Position.</summary>
    public sealed class FusionCall : EffectBase
    {
        public const string EffectId = "test_fusion_call";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Activation;

        public override SpellSpeed Speed => SpellSpeed.One;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) =>
            state.Player(source.Controller).FusionDeck.Count > 0 && state.Player(source.Controller).MonsterCount < PlayerState.ZoneCount;

        public override void Resolve(DuelEngine engine, ChainLink link)
        {
            PlayerState p = engine.State.Player(link.Player);
            if (p.FusionDeck.Count > 0)
            {
                engine.SpecialSummon(p.FusionDeck[0], link.Player, Position.FaceUpAttack);
            }
        }
    }

    /// <summary>Normal Spell: target a monster the opponent controls; it cannot change its battle position until the end of your next turn.</summary>
    public sealed class PositionLock : EffectBase
    {
        public const string EffectId = "test_position_lock";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Activation;

        public override SpellSpeed Speed => SpellSpeed.One;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) =>
            state.Opponent(source.Controller).MonsterCount > 0;

        public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source) =>
            new[] { new Choice("Lock the position of a monster the opponent controls", state.Opponent(source.Controller).Monsters.Select(m => m.Id).ToList(), 1, 1) };

        public override void Resolve(DuelEngine engine, ChainLink link)
        {
            if (link.Target is { } id && engine.State.Find(id) is { IsOnField: true } target)
            {
                engine.AddModifier(Modifier.OnCard(ModifierKind.CannotChangePosition, link.Source.Id, target.Id, 0, engine.State.NextTurnOf(link.Player)));
            }
        }
    }

    /// <summary>Jinzo-style Continuous effect: Trap Cards cannot be activated and their effects are negated.</summary>
    public sealed class TrapLock : EffectBase
    {
        public const string EffectId = "test_trap_lock";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Continuous;

        public override SpellSpeed Speed => SpellSpeed.One;

        public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
        {
            yield return Modifier.Global(ModifierKind.TrapsNegated, source.Id);
        }
    }

    /// <summary>Waboku-style Normal Trap: this turn you take no battle damage and your monsters cannot be destroyed by battle.</summary>
    public sealed class Shield : EffectBase
    {
        public const string EffectId = "test_shield";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Activation;

        public override SpellSpeed Speed => SpellSpeed.Two;

        public override void Resolve(DuelEngine engine, ChainLink link)
        {
            int turn = engine.State.TurnNumber;
            engine.AddModifier(Modifier.OnPlayer(ModifierKind.NoBattleDamage, link.Source.Id, link.Player, 0, turn));
            engine.AddModifier(Modifier.OnPlayer(ModifierKind.CannotBeDestroyedByBattle, link.Source.Id, link.Player, 0, turn));
        }
    }

    /// <summary>Continuous effect: this card inflicts piercing battle damage.</summary>
    public sealed class Piercer : EffectBase
    {
        public const string EffectId = "test_piercer";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Continuous;

        public override SpellSpeed Speed => SpellSpeed.One;

        public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
        {
            yield return Modifier.OnCard(ModifierKind.Piercing, source.Id, source.Id);
        }
    }

    /// <summary>Reaper on the Nightmare-style Continuous effect: cannot be destroyed by battle, can attack directly.</summary>
    public sealed class Reaper : EffectBase
    {
        public const string EffectId = "test_reaper";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Continuous;

        public override SpellSpeed Speed => SpellSpeed.One;

        public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
        {
            yield return Modifier.OnCard(ModifierKind.CannotBeDestroyedByBattle, source.Id, source.Id);
            yield return Modifier.OnCard(ModifierKind.CanAttackDirectly, source.Id, source.Id);
        }
    }

    /// <summary>Normal Spell: banish 1 face-up monster the opponent controls.</summary>
    public sealed class Banisher : EffectBase
    {
        public const string EffectId = "test_banisher";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Activation;

        public override SpellSpeed Speed => SpellSpeed.One;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) =>
            state.Opponent(source.Controller).Monsters.Any(m => m.IsFaceUp);

        public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source) =>
            new[] { new Choice("Banish a face-up monster the opponent controls", state.Opponent(source.Controller).Monsters.Where(m => m.IsFaceUp).Select(m => m.Id).ToList(), 1, 1) };

        public override void Resolve(DuelEngine engine, ChainLink link)
        {
            if (link.Target is { } id && engine.State.Find(id) is { IsOnField: true } target)
            {
                engine.Banish(target);
            }
        }
    }

    /// <summary>Book of Moon-style Quick-Play Spell: flip 1 face-up monster on the field into face-down Defense Position.</summary>
    public sealed class FlipDown : EffectBase
    {
        public const string EffectId = "test_flip_down";

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Activation;

        public override SpellSpeed Speed => SpellSpeed.Two;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) => EquipBoost.FaceUpMonsters(state).Any();

        public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source) =>
            new[] { new Choice("Flip a face-up monster face-down", EquipBoost.FaceUpMonsters(state).Select(m => m.Id).ToList(), 1, 1) };

        public override void Resolve(DuelEngine engine, ChainLink link)
        {
            if (link.Target is { } id && engine.State.Find(id) is { IsOnField: true } target)
            {
                engine.FlipFaceDown(target);
            }
        }
    }

    /// <summary>Premature Burial-style Equip Spell: pay 800 Life Points, Special Summon a monster from your Graveyard and equip it; when this card leaves the field, destroy the monster.</summary>
    public sealed class Burial : EffectBase
    {
        public const string EffectId = "test_burial";

        public const int Cost = 800;

        public override string Id => EffectId;

        public override EffectKind Kind => EffectKind.Activation;

        public override SpellSpeed Speed => SpellSpeed.One;

        public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context) =>
            state.Player(source.Controller).LifePoints >= Cost && Candidates(state, source).Any() && state.Player(source.Controller).MonsterCount < PlayerState.ZoneCount;

        public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source) =>
            new[] { new Choice("Special Summon a monster from your Graveyard", Candidates(state, source).Select(c => c.Id).ToList(), 1, 1) };

        public override void PayCosts(DuelEngine engine, ChainLink link) => engine.PayLifePoints(link.Player, Cost, link.Source.Id);

        public override void Resolve(DuelEngine engine, ChainLink link)
        {
            if (link.Target is { } id && engine.State.Find(id) is { Loc: Location.Graveyard } target && engine.SpecialSummon(target, link.Player, Position.FaceUpAttack))
            {
                engine.Equip(link.Source, target);
            }
        }

        public override void OnLeftField(DuelEngine engine, CardInstance source, Location from, Guid? equippedTo)
        {
            if (equippedTo is { } id && engine.State.Find(id) is { IsOnField: true } monster)
            {
                engine.Destroy(monster, DestroyReason.Effect);
            }
        }

        private static IEnumerable<CardInstance> Candidates(DuelState state, CardInstance source) =>
            state.Player(source.Controller).Graveyard.Where(c => c.IsMonster && c.Def.Kind == CardKind.Monster);
    }
}
