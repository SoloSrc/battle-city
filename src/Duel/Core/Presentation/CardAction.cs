using System;
using System.Collections.Generic;
using System.Linq;
using BattleCity.Duel.Core.Commands;
using BattleCity.Duel.Core.Effects;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Presentation;

/// <summary>What a menu entry does (GDD §3.4): the label is generic, never card-specific.</summary>
public enum ActionKind
{
    Summon,
    Set,
    Activate,
    SpecialSummon,
    Attack,
    ChangePosition,
    FlipSummon,
    Discard,
}

/// <summary>
/// One entry of the action menu of a card: every legal command that the entry
/// stands for. A Summon needing tributes carries one command per tribute set
/// (<see cref="TributesRequired"/> and <see cref="TributeOptions"/> feed the
/// picker); an Attack carries one command per target, null being a direct
/// attack.
/// </summary>
public sealed record CardAction(ActionKind Kind, string Label, IReadOnlyList<PlayerCommand> Commands)
{
    /// <summary>The single command of a plain entry; null when the entry needs a second choice (tributes, an attack target).</summary>
    public PlayerCommand? Single => Kind != ActionKind.Attack && !NeedsTributes && Commands.Count == 1 ? Commands[0] : null;

    public bool NeedsTributes => TributesRequired > 0;

    /// <summary>Tributes needed by the Summon or Set this entry stands for (0 for other kinds).</summary>
    public int TributesRequired => Commands.Count > 0 ? Commands[0] switch
    {
        NormalSummon s => s.Tributes.Count,
        SetMonster s => s.Tributes.Count,
        _ => 0,
    } : 0;

    /// <summary>Every monster that appears in some legal tribute set, in first-seen order.</summary>
    public IReadOnlyList<Guid> TributeOptions
    {
        get
        {
            var seen = new List<Guid>();
            foreach (PlayerCommand command in Commands)
            {
                IReadOnlyList<Guid> tributes = command switch
                {
                    NormalSummon s => s.Tributes,
                    SetMonster s => s.Tributes,
                    _ => Array.Empty<Guid>(),
                };
                foreach (Guid id in tributes)
                {
                    if (!seen.Contains(id))
                    {
                        seen.Add(id);
                    }
                }
            }

            return seen;
        }
    }

    /// <summary>The attack targets of an Attack entry; a null entry is the direct attack.</summary>
    public IReadOnlyList<Guid?> AttackTargets => Commands.OfType<DeclareAttack>().Select(a => a.Target).ToList();

    /// <summary>The Summon or Set command whose tribute set is exactly <paramref name="tributes"/>, or null when no such command is legal.</summary>
    public PlayerCommand? WithTributes(IReadOnlyCollection<Guid> tributes)
    {
        ArgumentNullException.ThrowIfNull(tributes);
        foreach (PlayerCommand command in Commands)
        {
            IReadOnlyList<Guid> set = command switch
            {
                NormalSummon s => s.Tributes,
                SetMonster s => s.Tributes,
                _ => Array.Empty<Guid>(),
            };
            if (set.Count == tributes.Count && set.All(tributes.Contains))
            {
                return command;
            }
        }

        return null;
    }

    /// <summary>The attack command aimed at <paramref name="target"/> (null for a direct attack), or null when it is not legal.</summary>
    public PlayerCommand? Targeting(Guid? target) => Commands.OfType<DeclareAttack>().FirstOrDefault(a => a.Target == target);
}

/// <summary>
/// Turns <see cref="DuelEngine.LegalActions"/> into what the HUD shows
/// (systems.md §6.3): the entries of a card's action menu, the responses of
/// a prompt, the phase advance. The UI knows no card: every label comes from
/// the command type, the card kind or the effect kind.
/// </summary>
public static class ActionCatalog
{
    /// <summary>The menu entries of <paramref name="card"/>: the legal commands that name it, grouped by kind in menu order.</summary>
    public static IReadOnlyList<CardAction> ForCard(DuelEngine engine, IReadOnlyList<PlayerCommand> legal, Guid card)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(legal);
        var groups = new Dictionary<(ActionKind Kind, string Label), List<PlayerCommand>>();
        var order = new List<(ActionKind Kind, string Label)>();
        foreach (PlayerCommand command in legal)
        {
            if (Classify(engine, command, card) is not { } entry)
            {
                continue;
            }

            if (!groups.TryGetValue(entry, out List<PlayerCommand>? list))
            {
                list = new List<PlayerCommand>();
                groups[entry] = list;
                order.Add(entry);
            }

            list.Add(command);
        }

        return order
            .OrderBy(e => e.Kind)
            .Select(e => new CardAction(e.Kind, e.Label, groups[e]))
            .ToList();
    }

    /// <summary>The card a command acts on, or null for commands without one (Pass, EnterBattlePhase, AnswerChoice).</summary>
    public static Guid? CardOf(PlayerCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command switch
        {
            NormalSummon c => c.Card,
            SetMonster c => c.Card,
            ChangePosition c => c.Card,
            FlipSummon c => c.Card,
            ActivateSpell c => c.Card,
            ActivateTrap c => c.Card,
            ActivateEffect c => c.Card,
            SetSpellTrap c => c.Card,
            DeclareAttack c => c.Attacker,
            Discard c => c.Card,
            _ => null,
        };
    }

    /// <summary>The player is answering a window rather than acting freely: a chain is being built, a window is open, or it is not their turn.</summary>
    public static bool IsResponding(DuelState state, int player)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.PendingChoice is null && (state.Chain.Count > 0 || state.Window != Window.Open || state.TurnPlayer != player);
    }

    /// <summary>The activations offered while responding (everything legal but Pass).</summary>
    public static IReadOnlyList<PlayerCommand> Responses(IReadOnlyList<PlayerCommand> legal)
    {
        ArgumentNullException.ThrowIfNull(legal);
        return legal.Where(c => c is not Pass).ToList();
    }

    /// <summary>The command behind the phase bar's advance: enter the Battle Phase when that is legal, else pass priority; null when neither is.</summary>
    public static PlayerCommand? Advance(IReadOnlyList<PlayerCommand> legal)
    {
        ArgumentNullException.ThrowIfNull(legal);
        return legal.FirstOrDefault(c => c is EnterBattlePhase) ?? legal.FirstOrDefault(c => c is Pass);
    }

    /// <summary>What the advance does from the current phase.</summary>
    public static string AdvanceLabel(DuelState state, IReadOnlyList<PlayerCommand> legal)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(legal);
        if (legal.Any(c => c is EnterBattlePhase))
        {
            return "Battle Phase";
        }

        return state.Phase switch
        {
            Phase.Main1 => "End turn",
            Phase.Battle => state.BattleStep == BattleStep.Battle ? "Main Phase 2" : "Continue",
            Phase.Main2 => "End turn",
            Phase.End => "End turn",
            _ => "Continue",
        };
    }

    /// <summary>The label of an effect activation by its kind: a summon procedure reads "Special Summon", everything else "Activate effect".</summary>
    public static string EffectLabel(IEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        return effect.Kind == EffectKind.SummonProcedure ? "Special Summon" : "Activate effect";
    }

    /// <summary>Two commands mean the same thing (record equality ignores the content of tribute lists).</summary>
    public static bool Same(PlayerCommand a, PlayerCommand b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return (a, b) switch
        {
            (NormalSummon x, NormalSummon y) => x.Player == y.Player && x.Card == y.Card && SameSet(x.Tributes, y.Tributes),
            (SetMonster x, SetMonster y) => x.Player == y.Player && x.Card == y.Card && SameSet(x.Tributes, y.Tributes),
            (AnswerChoice x, AnswerChoice y) => x.Player == y.Player && SameSet(x.Selected, y.Selected),
            _ => a.Equals(b),
        };
    }

    private static bool SameSet(IReadOnlyList<Guid> a, IReadOnlyList<Guid> b) => a.Count == b.Count && a.All(b.Contains);

    private static (ActionKind Kind, string Label)? Classify(DuelEngine engine, PlayerCommand command, Guid card)
    {
        if (CardOf(command) != card)
        {
            return null;
        }

        switch (command)
        {
            case NormalSummon:
                return (ActionKind.Summon, "Summon");
            case SetMonster:
                return (ActionKind.Set, "Set");
            case SetSpellTrap:
                return (ActionKind.Set, "Set");
            case ActivateSpell:
            case ActivateTrap:
                return (ActionKind.Activate, "Activate");
            case ActivateEffect effect:
                {
                    CardInstance? instance = engine.State.Find(effect.Card);
                    IReadOnlyList<IEffect> effects = instance is null ? Array.Empty<IEffect>() : engine.EffectsOf(instance);
                    IEffect? e = effect.EffectIndex >= 0 && effect.EffectIndex < effects.Count ? effects[effect.EffectIndex] : null;
                    bool procedure = e?.Kind == EffectKind.SummonProcedure;
                    string label = e is null ? "Activate effect" : EffectLabel(e);
                    if (effects.Count(x => x.Kind == e?.Kind) > 1)
                    {
                        label += $" {effect.EffectIndex + 1}";
                    }

                    return (procedure ? ActionKind.SpecialSummon : ActionKind.Activate, label);
                }

            case DeclareAttack:
                return (ActionKind.Attack, "Attack");
            case ChangePosition:
                {
                    CardInstance? instance = engine.State.Find(card);
                    return (ActionKind.ChangePosition, instance?.Pos == Position.FaceUpAttack ? "To Defense Position" : "To Attack Position");
                }

            case FlipSummon:
                return (ActionKind.FlipSummon, "Flip Summon");
            case Discard:
                return (ActionKind.Discard, "Discard");
            default:
                return null;
        }
    }
}
