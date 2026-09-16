using System;
using System.Collections.Generic;
using BattleCity.Duel.Core;
using BattleCity.Duel.Core.Ai;
using BattleCity.Duel.Core.Commands;
using Godot;

namespace BattleCity.DuelScene;

/// <summary>
/// Drives one duel in the scene tree (systems.md §6.3): the human's commands
/// arrive through <see cref="Submit"/> from <see cref="DuelUi"/>, the
/// opponent's agent answers after <see cref="AiDelay"/> so the player sees each
/// move land. <see cref="Changed"/> fires after every accepted command,
/// <see cref="Finished"/> once when the duel is over.
/// </summary>
public partial class DuelSession : Node
{
    /// <summary>Pause before an agent command, so the opponent's moves read as moves.</summary>
    [Export(PropertyHint.Range, "0,3,0.05,suffix:s")]
    public float AiDelay { get; set; } = 0.7f;

    private IDuelAgent? _agent;
    private float _timer;
    private bool _finished;

    /// <summary>After every accepted command.</summary>
    public event Action? Changed;

    /// <summary>Once, when the duel ends: the winner (null for a draw).</summary>
    public event Action<int?>? Finished;

    public DuelEngine? Engine { get; private set; }

    public int HumanPlayer { get; private set; }

    public int AgentPlayer => 1 - HumanPlayer;

    public PlayerCommand? LastCommand { get; private set; }

    public int Commands { get; private set; }

    public int HumanCommands { get; private set; }

    public int Rejected { get; private set; }

    public bool IsRunning => Engine is { State.IsOver: false };

    /// <summary>The human holds priority or answers a prompt and has at least one legal action.</summary>
    public bool IsHumanTurn => Engine is { State.IsOver: false } e && e.ActingPlayer == HumanPlayer && e.LegalActions(HumanPlayer).Count > 0;

    /// <summary>Starts driving <paramref name="engine"/>; <paramref name="opponent"/> plays the other seat.</summary>
    public void Begin(DuelEngine engine, IDuelAgent opponent, int humanPlayer = 0)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(opponent);
        Engine = engine;
        _agent = opponent;
        HumanPlayer = humanPlayer;
        LastCommand = null;
        Commands = 0;
        HumanCommands = 0;
        Rejected = 0;
        _finished = false;
        _timer = AiDelay;
    }

    public void End()
    {
        Engine = null;
        _agent = null;
    }

    /// <summary>Submits a command for either seat; the human's UI calls this.</summary>
    public SubmitResult Submit(PlayerCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (Engine is null)
        {
            return SubmitResult.Rejected("no duel is running");
        }

        SubmitResult result = Engine.Submit(command);
        if (!result.Accepted)
        {
            Rejected++;
            GD.PushWarning($"DuelSession: {command} rejected: {result.Error}");
            return result;
        }

        LastCommand = command;
        Commands++;
        if (command.Player == HumanPlayer)
        {
            HumanCommands++;
        }

        _timer = AiDelay;
        Changed?.Invoke();
        return result;
    }

    public override void _Process(double delta)
    {
        if (Engine is null || _agent is null)
        {
            return;
        }

        if (Engine.State.IsOver)
        {
            if (!_finished)
            {
                _finished = true;
                Finished?.Invoke(Engine.State.Winner);
            }

            return;
        }

        if (Engine.ActingPlayer != AgentPlayer)
        {
            return;
        }

        _timer -= (float)delta;
        if (_timer > 0.0f)
        {
            return;
        }

        IReadOnlyList<PlayerCommand> legal = Engine.LegalActions(AgentPlayer);
        if (legal.Count == 0)
        {
            return;
        }

        Submit(_agent.Choose(Engine, AgentPlayer, legal));
    }
}
