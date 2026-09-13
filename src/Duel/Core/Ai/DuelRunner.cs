using System;
using System.Collections.Generic;
using BattleCity.Duel.Core.Commands;

namespace BattleCity.Duel.Core.Ai;

/// <summary>Drives a duel between two agents to its end.</summary>
public static class DuelRunner
{
    /// <summary>Runs until the duel ends or <paramref name="maxSteps"/> commands were submitted. Returns the number of commands.</summary>
    public static int Play(DuelEngine engine, IDuelAgent agent0, IDuelAgent agent1, int maxSteps = 10_000, Action<DuelEngine>? afterStep = null)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(agent0);
        ArgumentNullException.ThrowIfNull(agent1);
        int steps = 0;
        while (!engine.State.IsOver && steps < maxSteps)
        {
            int player = engine.State.Priority;
            IReadOnlyList<PlayerCommand> legal = engine.LegalActions(player);
            if (legal.Count == 0)
            {
                throw new InvalidOperationException($"Player {player} has priority but no legal action in {engine.State.Phase}/{engine.State.BattleStep}.");
            }

            PlayerCommand command = (player == 0 ? agent0 : agent1).Choose(engine, player, legal);
            SubmitResult result = engine.Submit(command);
            if (!result.Accepted)
            {
                throw new InvalidOperationException($"Agent for player {player} chose an illegal command {command}: {result.Error}");
            }

            steps++;
            afterStep?.Invoke(engine);
        }

        return steps;
    }
}
