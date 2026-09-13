using System;

namespace BattleCity.Duel.Core.Data;

/// <summary>A card file under <c>data/cards/</c> does not match the schema of systems.md §5.3.</summary>
public sealed class CardDataException : Exception
{
    public CardDataException()
    {
    }

    public CardDataException(string message)
        : base(message)
    {
    }

    public CardDataException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
