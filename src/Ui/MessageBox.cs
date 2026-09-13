using BattleCity.Core;
using Godot;

namespace BattleCity.Ui;

/// <summary>
/// Placeholder text box for signs, dialogue lines and the placeholder duel
/// (systems.md §3.3). One box at a time; <c>interact</c> closes it (accepted),
/// and when shown as a choice <c>cancel</c> closes it declined. Player input is
/// locked while it is open. The dialogue system replaces the look later; the
/// contract (<see cref="Show"/> / <see cref="Closed"/>) stays.
/// </summary>
public partial class MessageBox : CanvasLayer
{
    [Signal]
    public delegate void ClosedEventHandler(bool accepted);

    private const string LockReason = "message";

    public bool IsOpen { get; private set; }

    public string Text => _text?.Text ?? string.Empty;

    private PanelContainer? _panel;
    private Label? _text;
    private Label? _hint;
    private bool _choice;
    private ulong _shownFrame;

    public override void _Ready()
    {
        Layer = 50;
        _panel = new PanelContainer { Name = "Panel", Visible = false };
        _panel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _panel.AnchorLeft = 0.5f;
        _panel.AnchorRight = 0.5f;
        _panel.AnchorTop = 1.0f;
        _panel.AnchorBottom = 1.0f;
        _panel.OffsetLeft = -480.0f;
        _panel.OffsetRight = 480.0f;
        _panel.OffsetTop = -190.0f;
        _panel.OffsetBottom = -40.0f;
        _panel.GrowHorizontal = Control.GrowDirection.Both;
        _panel.GrowVertical = Control.GrowDirection.Begin;
        AddChild(_panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        _panel.AddChild(margin);

        var column = new VBoxContainer();
        margin.AddChild(column);

        _text = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _text.AddThemeFontSizeOverride("font_size", 24);
        column.AddChild(_text);

        _hint = new Label { HorizontalAlignment = HorizontalAlignment.Right };
        _hint.AddThemeFontSizeOverride("font_size", 15);
        _hint.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.7f);
        column.AddChild(_hint);
    }

    /// <summary>Shows <paramref name="text"/>; with <paramref name="choice"/> the box offers accept (interact) and decline (cancel).</summary>
    public void Show(string text, bool choice = false, string? hint = null)
    {
        if (_panel is null || _text is null || _hint is null)
        {
            return;
        }

        if (IsOpen)
        {
            EmitSignal(SignalName.Closed, false);
        }

        _choice = choice;
        _text.Text = text;
        _hint.Text = hint ?? (choice ? "Interact: yes   Cancel: no" : "Interact: continue");
        _panel.Visible = true;
        IsOpen = true;
        _shownFrame = Engine.GetProcessFrames();
        Game.Instance?.LockInput(LockReason);
    }

    public void Close(bool accepted = true)
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        if (_panel is not null)
        {
            _panel.Visible = false;
        }

        Game.Instance?.UnlockInput(LockReason);
        EmitSignal(SignalName.Closed, accepted);
    }

    public override void _Process(double delta)
    {
        if (!IsOpen || Engine.GetProcessFrames() == _shownFrame)
        {
            return;
        }

        if (Input.IsActionJustPressed(InputActions.Interact))
        {
            Close(true);
        }
        else if (_choice && Input.IsActionJustPressed(InputActions.Cancel))
        {
            Close(false);
        }
    }
}
