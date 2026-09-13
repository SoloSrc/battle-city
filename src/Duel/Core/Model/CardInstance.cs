using System;
using System.Collections.Generic;

namespace BattleCity.Duel.Core.Model;

/// <summary>
/// One physical card in a duel (systems.md §5.2). Mutable; the engine is the
/// only writer once the duel has started.
/// </summary>
public sealed class CardInstance
{
    public CardInstance(Guid id, CardDefinition def, int owner)
    {
        Id = id;
        Def = def ?? throw new ArgumentNullException(nameof(def));
        Owner = owner;
        Controller = owner;
        Loc = Location.Deck;
        ZoneIndex = -1;
        Pos = Position.FaceDown;
        Counters = new Dictionary<string, int>(StringComparer.Ordinal);
    }

    private CardInstance(CardInstance other)
    {
        Id = other.Id;
        Def = other.Def;
        Owner = other.Owner;
        Controller = other.Controller;
        Loc = other.Loc;
        ZoneIndex = other.ZoneIndex;
        Pos = other.Pos;
        AtkMod = other.AtkMod;
        DefMod = other.DefMod;
        SetThisTurn = other.SetThisTurn;
        ArrivedThisTurn = other.ArrivedThisTurn;
        ChangedPositionThisTurn = other.ChangedPositionThisTurn;
        AttackedThisTurn = other.AttackedThisTurn;
        Counters = new Dictionary<string, int>(other.Counters, StringComparer.Ordinal);
    }

    public Guid Id { get; }

    public CardDefinition Def { get; }

    public int Owner { get; }

    public int Controller { get; set; }

    public Location Loc { get; set; }

    /// <summary>Slot index inside a zone array; −1 for list locations (deck, hand, graveyard, banished, fusion deck).</summary>
    public int ZoneIndex { get; set; }

    public Position Pos { get; set; }

    public int AtkMod { get; set; }

    public int DefMod { get; set; }

    /// <summary>Set (monster or Spell/Trap) this turn: blocks Flip Summons, Trap and Quick-Play activation.</summary>
    public bool SetThisTurn { get; set; }

    /// <summary>Summoned or Set on the field this turn: blocks position changes (systems.md §5.5).</summary>
    public bool ArrivedThisTurn { get; set; }

    public bool ChangedPositionThisTurn { get; set; }

    public bool AttackedThisTurn { get; set; }

    public Dictionary<string, int> Counters { get; }

    public int Atk => (Def.Monster?.Atk ?? 0) + AtkMod;

    public int DefValue => (Def.Monster?.Def ?? 0) + DefMod;

    public bool IsMonster => Def.IsMonster;

    public bool IsFaceUp => Pos is Position.FaceUpAttack or Position.FaceUpDefense or Position.FaceUp;

    public bool IsFaceDown => !IsFaceUp;

    public bool IsInAttackPosition => Pos == Position.FaceUpAttack;

    public bool IsInDefensePosition => Pos is Position.FaceUpDefense or Position.FaceDownDefense;

    public bool IsOnField => Loc is Location.MonsterZone or Location.SpellTrapZone or Location.FieldZone;

    /// <summary>Clears the per-turn flags at the start of each turn.</summary>
    public void ResetTurnFlags()
    {
        SetThisTurn = false;
        ArrivedThisTurn = false;
        ChangedPositionThisTurn = false;
        AttackedThisTurn = false;
    }

    /// <summary>Clears field-only state when the card leaves the field.</summary>
    public void ResetFieldState()
    {
        Controller = Owner;
        AtkMod = 0;
        DefMod = 0;
        ZoneIndex = -1;
        Pos = Position.FaceDown;
        Counters.Clear();
        ResetTurnFlags();
    }

    public CardInstance Clone() => new(this);

    public override string ToString() => $"{Def.Name} ({Id.ToString()[..8]})";
}
