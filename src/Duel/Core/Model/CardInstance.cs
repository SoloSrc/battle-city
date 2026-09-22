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
        ActivationsThisTurn = new Dictionary<string, int>(StringComparer.Ordinal);
        AttackTargetsThisTurn = new List<Guid>();
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
        AtkBonus = other.AtkBonus;
        DefBonus = other.DefBonus;
        Restrictions = other.Restrictions;
        EquippedTo = other.EquippedTo;
        ControlReturnsAfterTurn = other.ControlReturnsAfterTurn;
        SetThisTurn = other.SetThisTurn;
        ArrivedThisTurn = other.ArrivedThisTurn;
        FlippedThisTurn = other.FlippedThisTurn;
        ChangedPositionThisTurn = other.ChangedPositionThisTurn;
        AttackedThisTurn = other.AttackedThisTurn;
        LeavesAfterTurn = other.LeavesAfterTurn;
        FieldStay = other.FieldStay;
        Counters = new Dictionary<string, int>(other.Counters, StringComparer.Ordinal);
        ActivationsThisTurn = new Dictionary<string, int>(other.ActivationsThisTurn, StringComparer.Ordinal);
        AttackTargetsThisTurn = new List<Guid>(other.AttackTargetsThisTurn);
    }

    public Guid Id { get; }

    public CardDefinition Def { get; }

    public int Owner { get; }

    public int Controller { get; set; }

    public Location Loc { get; set; }

    /// <summary>Slot index inside a zone array; −1 for list locations (deck, hand, graveyard, banished, fusion deck).</summary>
    public int ZoneIndex { get; set; }

    public Position Pos { get; set; }

    /// <summary>ATK change from the active modifiers; written by the engine's recompute, never by effects.</summary>
    public int AtkBonus { get; internal set; }

    /// <summary>DEF change from the active modifiers; written by the engine's recompute, never by effects.</summary>
    public int DefBonus { get; internal set; }

    /// <summary>What the active modifiers forbid or grant this card; written by the engine's recompute.</summary>
    public Restriction Restrictions { get; internal set; }

    /// <summary>For an Equip Spell (or a card that attaches like one) on the field: the monster it is attached to.</summary>
    public Guid? EquippedTo { get; set; }

    /// <summary>Control goes back to the owner at the End Phase of this turn number (temporary control changes).</summary>
    public int? ControlReturnsAfterTurn { get; set; }

    /// <summary>Set (monster or Spell/Trap) this turn: blocks Flip Summons, Trap and Quick-Play activation.</summary>
    public bool SetThisTurn { get; set; }

    /// <summary>Summoned or Set on the field this turn: blocks position changes (systems.md §5.5).</summary>
    public bool ArrivedThisTurn { get; set; }

    /// <summary>Turned face-up this turn by a Flip Summon or by battle (Spirit monsters return at the End Phase).</summary>
    public bool FlippedThisTurn { get; set; }

    public bool ChangedPositionThisTurn { get; set; }

    public bool AttackedThisTurn { get; set; }

    /// <summary>The monsters this card attacked this turn, for a monster that may attack each of the opponent's monsters once (Asura Priest).</summary>
    public List<Guid> AttackTargetsThisTurn { get; }

    /// <summary>For a card that stays on the field for a fixed number of turns (Swords of Revealing Light): destroyed at the End Phase of this turn number.</summary>
    public int? LeavesAfterTurn { get; set; }

    /// <summary>
    /// Which stay on the field this is, counted up each time the card leaves it. A monster that left and came back
    /// is a new monster (2005 rulings): anything that remembered the old one (the pending attack) compares this stamp, not the id.
    /// </summary>
    public int FieldStay { get; private set; }

    public Dictionary<string, int> Counters { get; }

    /// <summary>Activations of each of this card's effects this turn, by effect id (once-per-turn tracking).</summary>
    public Dictionary<string, int> ActivationsThisTurn { get; }

    /// <summary>Current ATK: printed plus the active modifiers, never below 0.</summary>
    public int Atk => Math.Max(0, (Def.Monster?.Atk ?? 0) + AtkBonus);

    /// <summary>Current DEF: printed plus the active modifiers, never below 0.</summary>
    public int DefValue => Math.Max(0, (Def.Monster?.Def ?? 0) + DefBonus);

    public bool IsMonster => Def.IsMonster;

    public bool IsToken => Def.IsToken;

    public bool Has(Restriction restriction) => (Restrictions & restriction) != 0;

    public int Counter(string name) => Counters.TryGetValue(name, out int count) ? count : 0;

    public int Activations(string effectId) => ActivationsThisTurn.TryGetValue(effectId, out int count) ? count : 0;

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
        FlippedThisTurn = false;
        ChangedPositionThisTurn = false;
        AttackedThisTurn = false;
        ActivationsThisTurn.Clear();
        AttackTargetsThisTurn.Clear();
    }

    /// <summary>Clears field-only state when the card leaves the field.</summary>
    public void ResetFieldState()
    {
        Controller = Owner;
        AtkBonus = 0;
        DefBonus = 0;
        Restrictions = Restriction.None;
        EquippedTo = null;
        ControlReturnsAfterTurn = null;
        LeavesAfterTurn = null;
        ZoneIndex = -1;
        Pos = Position.FaceDown;
        FieldStay++;
        Counters.Clear();
        ResetTurnFlags();
    }

    public CardInstance Clone() => new(this);

    public override string ToString() => $"{Def.Name} ({Id.ToString()[..8]})";
}
