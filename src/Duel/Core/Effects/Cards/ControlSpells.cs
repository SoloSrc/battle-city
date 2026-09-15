using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Events;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects.Cards;

/// <summary>Delinquent Duo: pay 1000 Life Points; the opponent discards 1 random card, then 1 card of their choice.</summary>
public sealed class DelinquentDuoEffect : EffectBase
{
    public const string EffectId = "delinquent_duo";

    public const int Cost = 1000;

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return state.Player(source.Controller).LifePoints >= Cost && state.Opponent(source.Controller).Hand.Count > 0;
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
        int opponent = 1 - link.Player;
        if (link.Stage == 0)
        {
            engine.DiscardRandom(opponent, 1);
            link.Stage = 1;
        }

        List<CardInstance> hand = engine.State.Player(opponent).Hand;
        int count = Math.Min(1, hand.Count);
        if (engine.Ask(link, new Choice("Discard 1 card", Field.Ids(hand), count, count), opponent) is { Count: 1 } answer && engine.State.Find(answer[0]) is { Loc: Location.Hand } card)
        {
            engine.Discard(card);
        }
    }
}

/// <summary>Premature Burial: pay 800 Life Points; target 1 monster in your Graveyard, Special Summon it in Attack Position and equip it with this card; when this card leaves the field, destroy the monster.</summary>
public sealed class PrematureBurialEffect : EffectBase
{
    public const string EffectId = "premature_burial";

    public const int Cost = 800;

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        PlayerState p = state.Player(source.Controller);
        return p.LifePoints >= Cost && Field.Revivable(p).Any() && p.MonsterCount < PlayerState.ZoneCount && !p.Has(PlayerRestriction.CannotSummon);
    }

    public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return new[] { new Choice("Special Summon 1 monster from your Graveyard", Field.Ids(Field.Revivable(state.Player(source.Controller))), 1, 1) };
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
        if (link.Target is { } id && engine.State.Find(id) is { Loc: Location.Graveyard } target && engine.SpecialSummon(target, link.Player, Position.FaceUpAttack))
        {
            engine.Equip(link.Source, target);
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

/// <summary>Snatch Steal: take control of 1 face-up monster the opponent controls for as long as this card stays on the field.</summary>
public sealed class SnatchStealEffect : EffectBase
{
    public const string EffectId = "snatch_steal";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return Field.FaceUpMonsters(state.Opponent(source.Controller)).Any() && state.Player(source.Controller).MonsterCount < PlayerState.ZoneCount;
    }

    public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return new[] { new Choice("Take control of 1 face-up monster the opponent controls", Field.Ids(Field.FaceUpMonsters(state.Opponent(source.Controller))), 1, 1) };
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (link.Target is { } id && Field.OnField(engine.State, id) is { IsFaceUp: true } target && target.Controller != link.Player && engine.ChangeControl(target, link.Player))
        {
            engine.Equip(link.Source, target);
        }
    }

    public override void OnLeftField(DuelEngine engine, CardInstance source, Location from, Guid? equippedTo)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (equippedTo is { } id && engine.State.Find(id) is { Loc: Location.MonsterZone } monster)
        {
            engine.ChangeControl(monster, monster.Owner);
        }
    }
}

/// <summary>Snatch Steal's upkeep: the opponent gains 1000 Life Points during each of their Standby Phases while it is on the field.</summary>
public sealed class SnatchStealUpkeepEffect : EffectBase
{
    public const string EffectId = "snatch_steal_upkeep";

    public const int Amount = 1000;

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Trigger;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool IsMandatory => true;

    public override TriggerWindow? Trigger => TriggerWindow.Standby;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return source.IsOnField && source.IsFaceUp && source.EquippedTo is not null && state.TurnPlayer != source.Controller;
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        engine.GainLifePoints(1 - link.Player, Amount, link.Source.Id);
    }
}

/// <summary>Nobleman of Crossout: target 1 face-down monster on the field and banish it; if it was a Flip monster, both players banish every copy of it from their Decks.</summary>
public sealed class NoblemanOfCrossoutEffect : EffectBase
{
    public const string EffectId = "nobleman_of_crossout";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return Field.Monsters(state).Any(m => m.IsFaceDown);
    }

    public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new[] { new Choice("Banish 1 face-down monster on the field", Field.Ids(Field.Monsters(state).Where(m => m.IsFaceDown)), 1, 1) };
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (link.Target is not { } id || Field.OnField(engine.State, id) is not { IsFaceDown: true } target)
        {
            return;
        }

        CardDefinition def = target.Def;
        engine.Banish(target);
        if (def.Monster?.Category != MonsterCategory.Flip)
        {
            return;
        }

        foreach (PlayerState p in engine.State.Players)
        {
            foreach (CardInstance copy in p.Deck.Where(c => c.Def.Id == def.Id).ToList())
            {
                engine.Banish(copy);
            }

            engine.ShuffleDeck(p.Index);
        }
    }
}

/// <summary>Enemy Controller: Quick-Play; either switch the battle position of 1 face-up monster the opponent controls, or tribute 1 monster to take control of it until the End Phase.</summary>
public sealed class EnemyControllerEffect : EffectBase
{
    public const string EffectId = "enemy_controller";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.Two;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return Field.FaceUpMonsters(state.Opponent(source.Controller)).Any();
    }

    /// <summary>The mode is chosen with the tribute: select a monster to take control, select nothing to switch the target's position.</summary>
    public override IReadOnlyList<Choice> Costs(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        PlayerState p = state.Player(source.Controller);
        return p.MonsterCount == 0
            ? Array.Empty<Choice>()
            : new[] { new Choice("Tribute 1 monster to take control of the target until the End Phase, or nothing to switch its battle position", Field.Ids(p.Monsters), 0, 1) };
    }

    public override IReadOnlyList<Choice> Targets(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return new[] { new Choice("Choose 1 face-up monster the opponent controls", Field.Ids(Field.FaceUpMonsters(state.Opponent(source.Controller))), 1, 1) };
    }

    public override void PayCosts(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (Tributed(link) && Field.OnField(engine.State, link.Costs[0][0]) is { } tribute)
        {
            engine.SendToGraveyard(tribute);
        }
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (link.Target is not { } id || Field.OnField(engine.State, id) is not { IsFaceUp: true } target)
        {
            return;
        }

        if (Tributed(link))
        {
            engine.ChangeControl(target, link.Player, engine.State.TurnNumber);
        }
        else
        {
            engine.SwitchPosition(target);
        }
    }

    private static bool Tributed(ChainLink link) => link.Costs.Count > 0 && link.Costs[0].Count > 0;
}

/// <summary>Scapegoat: Quick-Play; Special Summon 4 Sheep Tokens in Defense Position that cannot be tributed for a Tribute Summon; you cannot Summon other monsters this turn.</summary>
public sealed class ScapegoatEffect : EffectBase
{
    public const string EffectId = "scapegoat";

    public const int TokenCount = 4;

    public static readonly CardDefinition SheepToken = CardDefinition.Token("sheep_token", "Sheep Token", "Beast", MonsterAttribute.Earth, 1, 0, 0);

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.Two;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        PlayerState p = state.Player(source.Controller);
        return p.MonsterCount < PlayerState.ZoneCount && !p.Has(PlayerRestriction.CannotSummon);
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        engine.AddModifier(Modifier.OnPlayer(ModifierKind.CannotSummon, link.Source.Id, link.Player, 0, engine.State.TurnNumber));
        for (int i = 0; i < TokenCount; i++)
        {
            CardInstance? token = engine.CreateToken(link.Player, SheepToken, Position.FaceUpDefense);
            if (token is null)
            {
                return;
            }

            engine.AddModifier(Modifier.OnCard(ModifierKind.CannotBeTributed, link.Source.Id, token.Id));
        }
    }
}

/// <summary>Metamorphosis: tribute 1 monster to Special Summon 1 Fusion Monster of the same Level from your Fusion Deck.</summary>
public sealed class MetamorphosisEffect : EffectBase
{
    public const string EffectId = "metamorphosis";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        PlayerState p = state.Player(source.Controller);
        return Tributes(p).Any() && !p.Has(PlayerRestriction.CannotSummon);
    }

    public override IReadOnlyList<Choice> Costs(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return new[] { new Choice("Tribute 1 monster whose Level matches a monster in your Fusion Deck", Field.Ids(Tributes(state.Player(source.Controller))), 1, 1) };
    }

    /// <summary>The tribute's Level is kept on <see cref="ChainLink.Stage"/>: a token is gone from the duel once tributed.</summary>
    public override void PayCosts(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (Field.OnField(engine.State, link.Costs[0][0]) is { } tribute)
        {
            link.Stage = tribute.Def.Monster!.Level;
            engine.SendToGraveyard(tribute);
        }
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        var candidates = engine.State.Player(link.Player).FusionDeck.Where(f => f.Def.Monster!.Level == link.Stage).ToList();
        if (Field.PickOne(engine, link, candidates, "Special Summon 1 Fusion Monster of the same Level") is { } fusion)
        {
            engine.SpecialSummon(fusion, link.Player, Position.FaceUpAttack);
        }
    }

    private static IEnumerable<CardInstance> Tributes(PlayerState p)
    {
        var levels = p.FusionDeck.Select(f => f.Def.Monster!.Level).ToHashSet();
        return p.Monsters.Where(m => levels.Contains(m.Def.Monster!.Level));
    }
}

/// <summary>Swords of Revealing Light: flips the opponent's monsters face-up; they cannot attack while it stays on the field, which lasts until the End Phase of the opponent's third turn.</summary>
public sealed class SwordsOfRevealingLightEffect : EffectBase
{
    public const string EffectId = "swords_of_revealing_light";

    /// <summary>The opponent's third turn after an activation on the controller's turn <c>T</c> is <c>T + 5</c>.</summary>
    public const int Duration = 5;

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool RemainsOnField => true;

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        link.Source.LeavesAfterTurn = engine.State.TurnNumber + Duration;
        foreach (CardInstance monster in engine.State.Opponent(link.Player).Monsters.Where(m => m.IsFaceDown).ToList())
        {
            engine.FlipFaceUp(monster);
        }
    }

    public override IEnumerable<Modifier> Modifiers(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(source);
        yield return Modifier.OnPlayer(ModifierKind.CannotAttack, source.Id, 1 - source.Controller);
    }
}

/// <summary>Creature Swap: each player chooses 1 monster they control and control of both is exchanged; they cannot change their battle position this turn.</summary>
public sealed class CreatureSwapEffect : EffectBase
{
    public const string EffectId = "creature_swap";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return state.Players.All(p => p.MonsterCount > 0);
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        DuelState s = engine.State;
        int opponent = 1 - link.Player;
        if (s.Player(link.Player).MonsterCount == 0 || s.Player(opponent).MonsterCount == 0)
        {
            return;
        }

        CardInstance? mine = Field.PickOne(engine, link, s.Player(link.Player).Monsters.ToList(), "Choose 1 monster you control to give away");
        if (mine is null)
        {
            return;
        }

        CardInstance? theirs = Field.PickOne(engine, link, s.Player(opponent).Monsters.ToList(), "Choose 1 monster you control to give away", opponent);
        if (theirs is null || !engine.SwapControl(mine, theirs))
        {
            return;
        }

        foreach (CardInstance monster in new[] { mine, theirs })
        {
            engine.AddModifier(Modifier.OnCard(ModifierKind.CannotChangePosition, link.Source.Id, monster.Id, 0, s.TurnNumber));
        }
    }
}
