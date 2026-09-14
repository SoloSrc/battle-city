namespace BattleCity.Duel.Core.Model;

/// <summary>How a monster arrived face-up on the field; Trigger effects and Traps read it from the summon window and from their <c>ActivationContext</c>.</summary>
public enum SummonKind
{
    /// <summary>Normal Summon without tributes.</summary>
    Normal,

    /// <summary>Normal Summon with one or two tributes (the Monarchs fire on this).</summary>
    Tribute,

    Flip,

    Special,
}
