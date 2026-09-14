using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects.Cards;

/// <summary>Graceful Charity: draw 3 cards, then discard 2 cards. The discard is chosen while the link resolves.</summary>
public sealed class GracefulCharityEffect : EffectBase
{
    public const string EffectId = "graceful_charity";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return state.Player(source.Controller).Deck.Count >= 3;
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        if (link.Stage == 0)
        {
            engine.Draw(link.Player, 3);
            link.Stage = 1;
            if (engine.State.IsOver)
            {
                return;
            }
        }

        List<CardInstance> hand = engine.State.Player(link.Player).Hand;
        int count = Math.Min(2, hand.Count);
        if (engine.Ask(link, new Choice("Discard 2 cards", Field.Ids(hand), count, count)) is not { } discarded)
        {
            return;
        }

        foreach (Guid id in discarded)
        {
            if (engine.State.Find(id) is { Loc: Location.Hand } card)
            {
                engine.Discard(card);
            }
        }
    }
}

/// <summary>Morphing Jar: FLIP: both players discard their entire hands, then draw 5 cards.</summary>
public sealed class MorphingJarEffect : EffectBase
{
    public const string EffectId = "morphing_jar";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Flip;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool IsMandatory => true;

    public override TriggerWindow? Trigger => TriggerWindow.OnFlip;

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        foreach (PlayerState p in engine.State.Players)
        {
            foreach (CardInstance card in p.Hand.ToList())
            {
                engine.Discard(card);
            }
        }

        foreach (int player in new[] { link.Player, 1 - link.Player })
        {
            engine.Draw(player, 5);
        }
    }
}
