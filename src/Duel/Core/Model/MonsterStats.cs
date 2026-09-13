namespace BattleCity.Duel.Core.Model;

/// <summary>Printed monster values (systems.md §5.3 <c>monster</c>).</summary>
public sealed record MonsterStats(
    string Type,
    MonsterAttribute Attribute,
    int Level,
    int Atk,
    int Def,
    MonsterCategory Category)
{
    /// <summary>Tributes needed for a Normal Summon: none up to level 4, one for 5–6, two for 7 and above (GDD §3.2).</summary>
    public int TributesRequired => Level >= 7 ? 2 : Level >= 5 ? 1 : 0;
}
