using System;
using System.Linq;
using BattleCity.Duel.Core.Model;
using BattleCity.Duel.Core.Presentation;
using Xunit;

namespace BattleCity.Duel.Core.Tests;

public class TutorialHintsTests
{
    [Fact]
    public void EveryTopicHasAShortLine()
    {
        foreach (TutorialTopic topic in Enum.GetValues<TutorialTopic>())
        {
            string text = TutorialHints.Text(topic);
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.InRange(text.Length, 20, 160);
        }
    }

    [Fact]
    public void EveryPhaseMapsToItsOwnTopic()
    {
        TutorialTopic[] topics = Enum.GetValues<Phase>().Select(TutorialHints.Of).ToArray();
        Assert.Equal(topics.Length, topics.Distinct().Count());
        Assert.Equal(TutorialTopic.BattlePhase, TutorialHints.Of(Phase.Battle));
    }
}
