using System.Collections.Generic;
using BattleCity.Core;
using BattleCity.World;
using Godot;

namespace BattleCity.Ui;

/// <summary>
/// The stack of full-screen menus over the world (issue #168, systems.md §2.2).
/// While at least one <see cref="MenuScreen"/> is open the game is in
/// <see cref="GameMode.Menu"/>, the world subtree stops processing and player
/// input is locked; closing the last screen restores all three. The gamepad,
/// keyboard and mouse rules live here in one place: <c>move_*</c> moves the
/// cursor, <c>interact</c> presses, <c>cancel</c> pops, and hovering moves the
/// cursor (wired by <see cref="MenuScreen.AddFocusable"/>). The stack refuses
/// to open during duels, transitions, encounters and open messages.
/// </summary>
public partial class MenuStack : CanvasLayer
{
    public const string LockReason = "menu";

    [Signal]
    public delegate void OpenedEventHandler();

    [Signal]
    public delegate void ClosedEventHandler();

    private readonly List<MenuScreen> _screens = new();
    private GameMode _previousMode = GameMode.Overworld;
    private ulong _changedFrame;

    public bool IsOpen => _screens.Count > 0;

    public int Count => _screens.Count;

    public MenuScreen? Top => _screens.Count > 0 ? _screens[^1] : null;

    /// <summary>True when a menu may open: exploring, with no duel, transition, encounter or message running.</summary>
    public static bool CanOpen =>
        Game.Instance is { Mode: GameMode.Overworld or GameMode.Interior, IsTransitioning: false } game
        && game.Encounters.State == EncounterSystem.EncounterState.Idle
        && !game.Messages.IsOpen;

    public override void _Ready()
    {
        // Over the duel HUD (45), under messages (50) and the fade (100).
        Layer = 48;
    }

    /// <summary>Opens <paramref name="screen"/> on top; returns false, freeing it, when the stack is closed and may not open.</summary>
    public bool Push(MenuScreen screen)
    {
        if (!IsOpen)
        {
            if (!CanOpen)
            {
                // Never added to the tree, so free it directly.
                screen.Free();
                return false;
            }

            Game game = Game.Instance!;
            _previousMode = game.Mode;
            game.SetMode(GameMode.Menu);
            game.LockInput(LockReason);
            game.PauseWorld(true);
            EmitSignal(SignalName.Opened);
        }

        if (Top is { } below)
        {
            below.Visible = false;
        }

        _screens.Add(screen);
        AddChild(screen);
        _changedFrame = Engine.GetProcessFrames();
        return true;
    }

    /// <summary>Closes the top screen; the world resumes when the last one closes.</summary>
    public void Pop()
    {
        if (!IsOpen)
        {
            return;
        }

        MenuScreen top = _screens[^1];
        _screens.RemoveAt(_screens.Count - 1);
        RemoveChild(top);
        top.QueueFree();
        _changedFrame = Engine.GetProcessFrames();
        if (Top is { } revealed)
        {
            revealed.Visible = true;
            return;
        }

        if (Game.Instance is { } game)
        {
            game.PauseWorld(false);
            game.UnlockInput(LockReason);
            game.SetMode(_previousMode);
        }

        EmitSignal(SignalName.Closed);
    }

    /// <summary>Closes every screen at once.</summary>
    public void Clear()
    {
        while (IsOpen)
        {
            Pop();
        }
    }

    public override void _Process(double delta)
    {
        // The frame guard keeps one key press from acting on two screens.
        if (!IsOpen || Engine.GetProcessFrames() == _changedFrame)
        {
            return;
        }

        MenuScreen top = _screens[^1];
        if (Input.IsActionJustPressed(InputActions.Cancel))
        {
            if (!top.HandleCancel())
            {
                Pop();
            }
        }
        else if (Input.IsActionJustPressed(InputActions.Interact))
        {
            top.Activate();
        }
        else if (Input.IsActionJustPressed(InputActions.MoveDown) || Input.IsActionJustPressed(InputActions.MoveRight))
        {
            top.MoveFocus(1);
        }
        else if (Input.IsActionJustPressed(InputActions.MoveUp) || Input.IsActionJustPressed(InputActions.MoveLeft))
        {
            top.MoveFocus(-1);
        }
    }
}
