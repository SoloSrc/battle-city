using System;
using System.Collections.Generic;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Effects;

/// <summary>Pot of Greed: draw 2 cards. The only tier 1 effect (systems.md §5.6).</summary>
public sealed class PotOfGreedEffect : IEffect
{
    public const string EffectId = "pot_of_greed";

    public string Id => EffectId;

    public EffectKind Kind => EffectKind.Activation;

    public SpellSpeed Speed => SpellSpeed.One;

    public bool IsMandatory => false;

    public TriggerWindow? Trigger => null;

    public bool CanActivate(DuelState state, CardInstance source)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(source);
        return state.Player(source.Controller).Deck.Count >= 2;
    }

    public IReadOnlyList<Choice> Costs(DuelState state, CardInstance source) => Array.Empty<Choice>();

    public IReadOnlyList<Choice> Targets(DuelState state, CardInstance source) => Array.Empty<Choice>();

    public void Resolve(DuelEngine engine, ChainLink link)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(link);
        engine.Draw(link.Player, 2);
    }
}
