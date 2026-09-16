using System;
using BattleCity.Duel.Core.Model;

namespace BattleCity.Duel.Core.Presentation;

/// <summary>What the tutorial duel explains (GDD §6 step 2): each phase and each kind of prompt, once.</summary>
public enum TutorialTopic
{
    DrawPhase,
    StandbyPhase,
    MainPhase1,
    BattlePhase,
    MainPhase2,
    EndPhase,
    Menu,
    Targets,
    Picker,
    Response,
    Discard,
}

/// <summary>The one-line explanations the HUD shows the first time a topic appears in the player's first duel.</summary>
public static class TutorialHints
{
    public static TutorialTopic Of(Phase phase) =>
        phase switch
        {
            Phase.Draw => TutorialTopic.DrawPhase,
            Phase.Standby => TutorialTopic.StandbyPhase,
            Phase.Main1 => TutorialTopic.MainPhase1,
            Phase.Battle => TutorialTopic.BattlePhase,
            Phase.Main2 => TutorialTopic.MainPhase2,
            Phase.End => TutorialTopic.EndPhase,
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown phase."),
        };

    public static string Text(TutorialTopic topic) =>
        topic switch
        {
            TutorialTopic.DrawPhase => "Draw Phase: the turn player draws one card. The player who goes first draws too.",
            TutorialTopic.StandbyPhase => "Standby Phase: effects that wait for the start of a turn happen here. Usually nothing does.",
            TutorialTopic.MainPhase1 => "Main Phase 1: Summon or Set one monster, play Spells, Set Traps. Pick a card and choose an action.",
            TutorialTopic.BattlePhase => "Battle Phase: attack with your monsters. Attack Position monsters fight with ATK, Defense Position ones defend with DEF.",
            TutorialTopic.MainPhase2 => "Main Phase 2: one more chance to Summon, Set and play cards after battle.",
            TutorialTopic.EndPhase => "End Phase: the turn ends. With more than six cards in hand, discard down to six.",
            TutorialTopic.Menu => "This menu lists what the selected card can do right now. Only legal actions are offered.",
            TutorialTopic.Targets => "Choose what to attack. A direct attack hits Life Points when the opponent controls no monsters.",
            TutorialTopic.Picker => "A card effect needs a choice. Mark the cards it asks for, then confirm.",
            TutorialTopic.Response => "The opponent acted. You may answer with a Trap or Quick-Play Spell, or pass.",
            TutorialTopic.Discard => "Your hand is over the limit. Choose a card to discard.",
            _ => throw new ArgumentOutOfRangeException(nameof(topic), topic, "Unknown topic."),
        };
}
