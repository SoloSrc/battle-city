using System.Collections.Generic;
using Godot;

namespace BattleCity.Ui;

/// <summary>
/// One full-screen menu inside the <see cref="MenuStack"/> (issue #168,
/// systems.md §2.2). A screen builds its controls in <c>_Ready</c> and
/// registers the ones the cursor can land on with <see cref="AddFocusable"/>;
/// presses arrive through the controls' own signals. The stack owns
/// navigation and activation; a screen only says what is focusable, how a
/// focused control looks (<see cref="SetFocused"/>) and whether it consumes
/// <c>cancel</c> (<see cref="HandleCancel"/>).
/// </summary>
public partial class MenuScreen : Control
{
    private readonly List<Control> _focusables = new();
    private int _focus;

    /// <summary>Controls the cursor can land on, in registration order.</summary>
    public IReadOnlyList<Control> Focusables => _focusables;

    /// <summary>Index of the focused control; kept while a screen above hides this one.</summary>
    public int FocusIndex => _focus;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
    }

    /// <summary>Moves the cursor by <paramref name="step"/> entries, wrapping around.</summary>
    public void MoveFocus(int step)
    {
        if (_focusables.Count > 0)
        {
            SetFocus(((_focus + step) % _focusables.Count + _focusables.Count) % _focusables.Count);
        }
    }

    /// <summary>Puts the cursor on the control at <paramref name="index"/>.</summary>
    public void SetFocus(int index)
    {
        if (index < 0 || index >= _focusables.Count)
        {
            return;
        }

        if (_focus < _focusables.Count)
        {
            SetFocused(_focusables[_focus], false);
        }

        _focus = index;
        SetFocused(_focusables[_focus], true);
    }

    /// <summary>Presses the focused control the way a mouse click would.</summary>
    public void Activate()
    {
        if (_focus < _focusables.Count && _focusables[_focus] is BaseButton { Disabled: false } button)
        {
            button.EmitSignal(BaseButton.SignalName.Pressed);
        }
    }

    /// <summary>Called on <c>cancel</c>; return true to consume it, false to let the stack pop this screen.</summary>
    public virtual bool HandleCancel() => false;

    /// <summary>Registers a focusable control; hovering it moves the cursor there (mouse rule of the stack).</summary>
    protected void AddFocusable(Control control)
    {
        int index = _focusables.Count;
        _focusables.Add(control);
        control.MouseEntered += () => SetFocus(index);
        SetFocused(control, index == _focus);
    }

    /// <summary>How a focused control looks; the default dims everything the cursor is not on.</summary>
    protected virtual void SetFocused(Control control, bool focused)
    {
        control.Modulate = focused ? Colors.White : new Color(0.6f, 0.6f, 0.6f);
    }
}
