using System;
using System.Collections.Generic;

namespace BattleCity.Duel.Core.Commands;

/// <summary>What a player wants to do (systems.md §5.1). Validated by <see cref="DuelEngine.Submit"/>.</summary>
public abstract record PlayerCommand(int Player);

/// <summary>Pass priority. On an empty chain two consecutive passes advance the phase or step.</summary>
public sealed record Pass(int Player) : PlayerCommand(Player);

/// <summary>Normal Summon a monster from the hand in face-up Attack Position, tributing <see cref="Tributes"/> if its level needs them.</summary>
public sealed record NormalSummon(int Player, Guid Card, IReadOnlyList<Guid> Tributes) : PlayerCommand(Player)
{
    public NormalSummon(int player, Guid card)
        : this(player, card, Array.Empty<Guid>())
    {
    }
}

/// <summary>Set a monster from the hand face-down in Defense Position (uses the Normal Summon).</summary>
public sealed record SetMonster(int Player, Guid Card, IReadOnlyList<Guid> Tributes) : PlayerCommand(Player)
{
    public SetMonster(int player, Guid card)
        : this(player, card, Array.Empty<Guid>())
    {
    }
}

/// <summary>Switch a face-up monster between Attack and Defense Position.</summary>
public sealed record ChangePosition(int Player, Guid Card) : PlayerCommand(Player);

/// <summary>Flip Summon a face-down monster into face-up Attack Position.</summary>
public sealed record FlipSummon(int Player, Guid Card) : PlayerCommand(Player);

/// <summary>Activate a Spell from the hand or a Set Spell on the field.</summary>
public sealed record ActivateSpell(int Player, Guid Card) : PlayerCommand(Player);

/// <summary>Set a Spell or Trap from the hand face-down.</summary>
public sealed record SetSpellTrap(int Player, Guid Card) : PlayerCommand(Player);

/// <summary>Move from Main Phase 1 into the Battle Phase.</summary>
public sealed record EnterBattlePhase(int Player) : PlayerCommand(Player);

/// <summary>Attack with <see cref="Attacker"/>; <see cref="Target"/> null is a direct attack.</summary>
public sealed record DeclareAttack(int Player, Guid Attacker, Guid? Target) : PlayerCommand(Player);

/// <summary>Discard from the hand during the End Phase to reach the hand limit.</summary>
public sealed record Discard(int Player, Guid Card) : PlayerCommand(Player);

public sealed record Surrender(int Player) : PlayerCommand(Player);
