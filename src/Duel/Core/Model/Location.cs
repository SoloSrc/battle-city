namespace BattleCity.Duel.Core.Model;

/// <summary>Where a <see cref="CardInstance"/> currently is.</summary>
public enum Location
{
    Deck,
    Hand,
    MonsterZone,
    SpellTrapZone,
    FieldZone,
    Graveyard,
    Banished,
    FusionDeck,
}
