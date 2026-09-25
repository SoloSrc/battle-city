using System;
using System.Collections.Generic;
using BattleCity.Core;
using BattleCity.Ui;
using Godot;

namespace BattleCity.Diagnostics;

/// <summary>
/// Headless test for the menu screen stack (issue #168): runs New Game through
/// the real <see cref="Game"/> autoload, then opens, stacks and closes screens
/// and checks the mode, the input lock, the world pause, the open guards and
/// the focus rules, driving them with the same simulated action presses a
/// player would use. `MenuStackTest FAIL` lines fail CI.
/// </summary>
public partial class MenuStackTestScene : Node
{
    private const string FailPrefix = "MenuStackTest FAIL";
    private const ulong Seed = 24680;
    private const string TestSavePath = "user://save_menu_stack_test.json";

    /// <summary>Frames between simulated presses; the stack polls once per process frame.</summary>
    private const int StepFrames = 5;

    private enum Phase
    {
        Boot,
        Guards,
        Open,
        Navigate,
        Stack,
        Close,
        TransitionGuard,
        Done,
    }

    [Export]
    public Label? ReportLabel { get; set; }

    private readonly List<string> _report = new();
    private readonly List<string> _queued = new();
    private Phase _phase = Phase.Boot;
    private int _phaseFrames;
    private int _frame;
    private int _step;
    private int _pass;
    private int _fail;
    private GameMode _modeBefore;
    private MenuTestScreen? _first;

    private static Game G => Game.Instance!;

    public override void _Ready()
    {
        if (Game.Instance is null)
        {
            Fail("Game autoload missing");
            _phase = Phase.Done;
            return;
        }

        G.SavePath = TestSavePath;
        G.NewGame(Seed);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_phase == Phase.Done)
        {
            return;
        }

        _frame++;
        _phaseFrames++;
        ReleaseQueued();
        Step();
    }

    private void Step()
    {
        switch (_phase)
        {
            case Phase.Boot:
                if (G.LevelPath.Length > 0 && !G.IsTransitioning && G.Player is not null)
                {
                    _modeBefore = G.Mode;
                    Check(G.Mode is GameMode.Overworld or GameMode.Interior, Inv($"New Game settled in a movement mode ({G.Mode})"));
                    Next(Phase.Guards);
                }

                Timeout(600, "New Game did not settle");
                break;

            case Phase.Guards:
                Check(MenuStack.CanOpen, "menu may open while exploring");
                G.Messages.Show("guard test");
                Check(!MenuStack.CanOpen, "menu may not open over a message");
                Check(!G.Menus.Push(new MenuTestScreen()), "Push is refused while a message is open");
                G.Messages.Close(false);
                Check(MenuStack.CanOpen, "menu may open again after the message closes");
                G.SetMode(GameMode.Duel);
                Check(!MenuStack.CanOpen, "menu may not open during a duel");
                Check(!G.Menus.Push(new MenuTestScreen()), "Push is refused during a duel");
                G.SetMode(_modeBefore);
                Next(Phase.Open);
                break;

            case Phase.Open:
                _first = new MenuTestScreen();
                Check(G.Menus.Push(_first), "first screen opens");
                Check(G.Menus.IsOpen && G.Menus.Count == 1, "stack reports one open screen");
                Check(G.Mode == GameMode.Menu, Inv($"mode is Menu while open ({G.Mode})"));
                Check(!G.InputEnabled, "player input is locked while open");
                Check(G.Player is { } player && !player.CanProcess(), "world stops processing while open");
                Check(_first.FocusIndex == 0, "cursor starts on the first entry");
                Next(Phase.Navigate);
                break;

            case Phase.Navigate:
                // One simulated press per step, checked on the following step.
                switch (Advance())
                {
                    case 0:
                        Press(InputActions.MoveDown);
                        break;
                    case 1:
                        Check(_first!.FocusIndex == 1, Inv($"move_down moves the cursor to entry 1 ({_first.FocusIndex})"));
                        Press(InputActions.Interact);
                        break;
                    case 2:
                        Check(_first!.Presses.Count == 1 && _first.Presses[0] == 1, "interact presses the focused entry");
                        Press(InputActions.MoveUp);
                        break;
                    case 3:
                        Check(_first!.FocusIndex == 0, "move_up moves the cursor back to entry 0");
                        Press(InputActions.MoveUp);
                        break;
                    case 4:
                        Check(_first!.FocusIndex == 2, Inv($"the cursor wraps from the top to the last entry ({_first.FocusIndex})"));
                        Next(Phase.Stack);
                        break;
                }

                break;

            case Phase.Stack:
                switch (Advance())
                {
                    case 0:
                        Check(G.Menus.Push(new MenuTestScreen()), "second screen stacks on top");
                        Check(G.Menus.Count == 2 && !_first!.Visible, "the screen below is hidden");
                        Check(G.Mode == GameMode.Menu, "mode stays Menu while stacked");
                        break;
                    case 1:
                        Press(InputActions.Cancel);
                        break;
                    case 2:
                        Check(G.Menus.Count == 1 && G.Menus.Top == _first, "cancel pops back to the first screen");
                        Check(_first!.Visible, "the revealed screen is visible again");
                        Check(_first.FocusIndex == 2, "the revealed screen kept its cursor");
                        Next(Phase.Close);
                        break;
                }

                break;

            case Phase.Close:
                switch (Advance())
                {
                    case 0:
                        Press(InputActions.Cancel);
                        break;
                    case 1:
                        Check(!G.Menus.IsOpen, "cancel on the last screen closes the stack");
                        Check(G.Mode == _modeBefore, Inv($"mode is restored on close ({G.Mode})"));
                        Check(G.InputEnabled, "player input is unlocked on close");
                        Check(G.Player is { } resumed && resumed.CanProcess(), "world resumes processing on close");
                        G.Transition(G.LevelPath, G.SpawnId);
                        Check(G.IsTransitioning && !MenuStack.CanOpen, "menu may not open during a transition");
                        Next(Phase.TransitionGuard);
                        break;
                }

                break;

            case Phase.TransitionGuard:
                if (!G.IsTransitioning)
                {
                    Check(MenuStack.CanOpen, "menu may open again after the transition");
                    Finish();
                }

                Timeout(600, "transition did not finish");
                break;
        }
    }

    /// <summary>The sub-step of the current phase, advancing every <see cref="StepFrames"/> frames.</summary>
    private int Advance()
    {
        _step = (_phaseFrames - 1) / StepFrames;
        return (_phaseFrames - 1) % StepFrames == 0 ? _step : -1;
    }

    private void Press(string action)
    {
        Input.ActionPress(action);
        _queued.Add(action);
    }

    private void ReleaseQueued()
    {
        foreach (string action in _queued)
        {
            Input.ActionRelease(action);
        }

        _queued.Clear();
    }

    private void Next(Phase phase)
    {
        _phase = phase;
        _phaseFrames = 0;
        _step = 0;
    }

    private void Timeout(int frames, string message)
    {
        if (_phaseFrames > frames)
        {
            Fail(Inv($"{message} (phase {_phase}, frame {_frame})"));
            Finish();
        }
    }

    private void Finish()
    {
        ManagedWrappers.Flush();
        _phase = Phase.Done;
        string summary = Inv($"MenuStackTest summary: {_pass} pass, {_fail} fail ({_frame} frames)");
        GD.Print(summary);
        if (ReportLabel is not null)
        {
            ReportLabel.Text = summary + "\n" + string.Join("\n", _report);
        }
    }

    private void Check(bool ok, string message)
    {
        if (ok)
        {
            _pass++;
            Report("PASS", message);
        }
        else
        {
            Fail(message);
        }
    }

    private void Fail(string message)
    {
        _fail++;
        Report("FAIL", message);
        GD.PrintErr($"{FailPrefix}: {message}");
    }

    private void Report(string status, string message)
    {
        string line = Inv($"{message} (frame {_frame})");
        _report.Add($"[{status}] {line}");
        GD.Print($"MenuStackTest {status}: {line}");
    }

    private static string Inv(FormattableString text) => FormattableString.Invariant(text);
}

/// <summary>Three plain entries; records which were pressed, for the test above.</summary>
internal partial class MenuTestScreen : MenuScreen
{
    /// <summary>Indices of the entries pressed, in order.</summary>
    public List<int> Presses { get; } = new();

    public override void _Ready()
    {
        base._Ready();
        var column = new VBoxContainer();
        AddChild(column);
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            var button = new Button { Text = Inv($"Entry {index}"), FocusMode = FocusModeEnum.None };
            button.Pressed += () => Presses.Add(index);
            column.AddChild(button);
            AddFocusable(button);
        }
    }

    private static string Inv(FormattableString text) => FormattableString.Invariant(text);
}
