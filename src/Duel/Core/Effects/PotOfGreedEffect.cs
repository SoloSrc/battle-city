using System;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects;

/// <summary>Pot of Greed: draw 2 cards. The only tier 1 effect (systems.md §5.6).</summary>
public sealed class PotOfGreedEffect : EffectBase
{
    public const string EffectId = "pot_of_greed";

    public override string Id => EffectId;

    public override EffectKind Kind => EffectKind.Activation;

    public override SpellSpeed Speed => SpellSpeed.One;

    public override bool CanActivate(DuelState state, CardInstance source, ActivationContext context)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return state.Player(source.Controller).Deck.Count >= 2;
    }

    public override void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        engine.Draw(link.Player, 2);
    }
}
