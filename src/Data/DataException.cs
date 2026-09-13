using System;

namespace BattleCity.Data;

/// <summary>A file under <c>data/</c> does not match its schema (systems.md §5.3, §7, §8; GDD §1.1).</summary>
public sealed class DataException : Exception
{
    public DataException()
    {
    }

    public DataException(string message)
        : base(message)
    {
    }

    public DataException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
