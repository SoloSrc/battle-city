using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects.Cards;

/// <summary>Black Luster Soldier's first choice, once per turn (shared with the double attack): target 1 monster on the field and banish it; the Soldier cannot attack this turn.</summary>
public sealed class BlackLusterSoldierBanishEffect : EffectBase
{
    public const string EffectId = "black_luster_soldier_envoy_of_the_beginning_banish";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Ignition;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool OncePerTurn => true;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return source.IsOnField && source.Activations(BlackLusterSoldierDoubleAttackEffect.EffectId) == 0 && Field.Monsters(state).Any(m => m != source);
    }

    public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new[] { new Choice("Banish 1 monster on the field", Field.Ids(Field.Monsters(state).Where(m => m != source)), 1, 1) };
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        engine.AddModifier(Modifier.OnCard(ModifierKind.CannotAttack, link.Source.Id, link.Source.Id, 0, engine.State.TurnNumber));
        if (link.Target is { } id && Field.OnField(engine.State, id) is { } target)
        {
            engine.Banish(target);
        }
    }
}

/// <summary>Black Luster Soldier's second choice, once per turn (shared with the banish): after it destroys a monster by battle, it may attack once again.</summary>
public sealed class BlackLusterSoldierDoubleAttackEffect : EffectBase
{
    public const string EffectId = "black_luster_soldier_envoy_of_the_beginning_double_attack";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Trigger;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool OncePerTurn => true;

    public override TriggerWindow? Trigger => TriggerWindow.OnBattle;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        return source.IsOnField && source.IsInAttackPosition && source.Activations(BlackLusterSoldierBanishEffect.EffectId) == 0
            && context.Battled is { } id && state.Find(id) is { Loc: Location.Graveyard or Location.Banished } fallen && fallen.Owner != source.Controller;
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (link.Source.IsOnField)
        {
            link.Source.AttackedThisTurn = false;
        }
    }
}

/// <summary>Thousand-Eyes Restrict: other monsters cannot attack or change their battle position; it has the ATK and DEF of the monster it absorbed.</summary>
public sealed class ThousandEyesRestrictEffect : EffectBase
{
    public const string EffectId = "thousand_eyes_restrict";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Continuous;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        foreach (CardInstance monster in Field.Monsters(state).Where(m => m != source))
        {
            yield return Modifier.OnCard(ModifierKind.CannotAttack, source.Id, monster.Id);
            yield return Modifier.OnCard(ModifierKind.CannotChangePosition, source.Id, monster.Id);
        }

        CardInstance? absorbed = state.Players.SelectMany(p => p.SpellTraps).FirstOrDefault(e => e.IsMonster && e.EquippedTo == source.Id);
        if (absorbed?.Def.Monster is { } stats)
        {
            yield return Modifier.OnCard(ModifierKind.Atk, source.Id, source.Id, stats.Atk);
            yield return Modifier.OnCard(ModifierKind.Def, source.Id, source.Id, stats.Def);
        }
    }
}

/// <summary>Thousand-Eyes Restrict's Ignition effect, once per turn: target 1 monster on the field and equip it to this card; it is destroyed in this card's place when battle would destroy it.</summary>
public sealed class ThousandEyesAbsorbEffect : EffectBase
{
    public const string EffectId = "thousand_eyes_restrict_absorb";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Ignition;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool OncePerTurn => true;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return source.IsOnField && state.Player(source.Controller).SpellTrapCount < PlayerState.ZoneCount && Candidates(state, source).Any();
    }

    public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new[] { new Choice("Equip 1 monster on the field to Thousand-Eyes Restrict", Field.Ids(Candidates(state, source)), 1, 1) };
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (link.Target is { } id && Field.OnField(engine.State, id) is { } target && link.Source.IsOnField)
        {
            // A monster it already holds goes to the Graveyard first: one absorbed monster at a time.
            if (engine.AbsorbedBy(link.Source) is { } previous)
            {
                engine.SendToGraveyard(previous);
            }

            engine.AbsorbMonster(target, link.Source);
        }
    }

    private static IEnumerable<CardInstance> Candidates(DuelState state, CardInstance source) => Field.Monsters(state).Where(m => m != source && !m.IsToken);
}

/// <summary>Cyber Jar: FLIP: destroy all monsters on the field, then each player reveals the top 5 cards of their Deck, Special Summons the Level 4 or lower monsters among them in Attack Position and adds the rest to the hand.</summary>
public sealed class CyberJarEffect : EffectBase
{
    public const string EffectId = "cyber_jar";

    public const int RevealCount = 5;

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Flip;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool IsMandatory => true;

    public override TriggerWindow? Trigger => TriggerWindow.OnFlip;

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        DuelState s = engine.State;
        foreach (CardInstance monster in Field.Monsters(s).ToList())
        {
            engine.Destroy(monster, DestroyReason.Effect);
        }

        foreach (int player in new[] { link.Player, 1 - link.Player })
        {
            PlayerState p = s.Player(player);
            var revealed = p.Deck.TakeLast(RevealCount).Reverse().ToList();
            engine.Emit(new CardsRevealed(player, Field.Ids(revealed)));
            foreach (CardInstance card in revealed)
            {
                bool summonable = card.Def.Kind == CardKind.Monster && card.Def.Monster!.Level <= 4;
                if (!summonable || !engine.SpecialSummon(card, player, Position.FaceUpAttack))
                {
                    engine.ReturnToHand(card);
                }
            }
        }
    }
}
