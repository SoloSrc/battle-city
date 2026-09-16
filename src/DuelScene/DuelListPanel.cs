using System;
using System.Collections.Generic;
using Godot;

namespace BattleCity.DuelScene;

/// <summary>One line of a <see cref="DuelListPanel"/>: its text, what pressing it does, and what it stands for (a card, a command) for the highlight and the tests.</summary>
public sealed class DuelListEntry
{
    public DuelListEntry(string label, Action? onPress, object? tag = null, Guid? card = null, bool enabled = true)
    {
        Label = label;
        OnPress = onPress;
        Tag = tag;
        Card = card;
        Enabled = enabled;
    }

    public string Label { get; set; }

    public Action? OnPress { get; }

    public object? Tag { get; }

    /// <summary>The 3D card this entry refers to; highlighted while the entry is.</summary>
    public Guid? Card { get; }

    public bool Enabled { get; }
}

/// <summary>
/// A titled vertical list of buttons with one highlighted entry: the action
/// menu, the attack targets, the response prompt, the <c>Choice</c> picker
/// and the pile lists of <see cref="DuelUi"/> are all one of these. Gamepad
/// and keyboard move the highlight and press it; the mouse presses the buttons.
/// </summary>
public partial class DuelListPanel : PanelContainer
{
    private readonly List<Button> _buttons = new();
    private readonly List<DuelListEntry> _entries = new();
    private VBoxContainer _box = null!;
    private Label _title = null!;
    private Label _footer = null!;
    private int _index = -1;

    /// <summary>The highlighted entry changed (index).</summary>
    public event Action<int>? Highlighted;

    public IReadOnlyList<DuelListEntry> Entries => _entries;

    public int Index => _index;

    public bool IsOpen => Visible;

    public DuelListEntry? Current => _index >= 0 && _index < _entries.Count ? _entries[_index] : null;

    public override void _Ready()
    {
        Visible = false;
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_top", 10);
        margin.AddThemeConstantOverride("margin_bottom", 10);
        AddChild(margin);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        margin.AddChild(column);
        _title = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(280.0f, 0.0f) };
        _title.AddThemeFontSizeOverride("font_size", 20);
        column.AddChild(_title);
        _box = new VBoxContainer();
        _box.AddThemeConstantOverride("separation", 2);
        column.AddChild(_box);
        _footer = new Label { Visible = false };
        _footer.AddThemeFontSizeOverride("font_size", 14);
        _footer.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.7f);
        column.AddChild(_footer);
    }

    /// <summary>Shows <paramref name="entries"/> under <paramref name="title"/> with the first enabled entry highlighted.</summary>
    public void Open(string title, IReadOnlyList<DuelListEntry> entries, string? footer = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        foreach (Button button in _buttons)
        {
            button.QueueFree();
        }

        _buttons.Clear();
        _entries.Clear();
        _entries.AddRange(entries);
        _title.Text = title;
        _footer.Text = footer ?? string.Empty;
        _footer.Visible = footer is not null;
        for (int i = 0; i < _entries.Count; i++)
        {
            int index = i;
            var button = new Button
            {
                Text = _entries[i].Label,
                Alignment = HorizontalAlignment.Left,
                Disabled = !_entries[i].Enabled,
                FocusMode = FocusModeEnum.None,
            };
            button.AddThemeFontSizeOverride("font_size", 18);
            button.Pressed += () => Press(index);
            button.MouseEntered += () => Highlight(index);
            _box.AddChild(button);
            _buttons.Add(button);
        }

        Visible = true;
        _index = -1;
        Move(1);
    }

    public void Close()
    {
        Visible = false;
        _index = -1;
    }

    /// <summary>Moves the highlight by <paramref name="delta"/> over the enabled entries, wrapping.</summary>
    public void Move(int delta)
    {
        if (_entries.Count == 0)
        {
            return;
        }

        int next = _index;
        for (int step = 0; step < _entries.Count; step++)
        {
            next = ((next + delta) % _entries.Count + _entries.Count) % _entries.Count;
            if (_entries[next].Enabled)
            {
                Highlight(next);
                return;
            }
        }
    }

    public void Highlight(int index)
    {
        if (index < 0 || index >= _entries.Count || !_entries[index].Enabled)
        {
            return;
        }

        _index = index;
        for (int i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].Modulate = i == index ? new Color(1.0f, 0.95f, 0.6f) : Colors.White;
        }

        Highlighted?.Invoke(index);
    }

    /// <summary>Presses the highlighted entry.</summary>
    public void Press() => Press(_index);

    public void Press(int index)
    {
        if (index < 0 || index >= _entries.Count || !_entries[index].Enabled)
        {
            return;
        }

        _entries[index].OnPress?.Invoke();
    }

    /// <summary>Changes the text of an entry (a picker toggling its mark).</summary>
    public void SetLabel(int index, string label)
    {
        if (index >= 0 && index < _entries.Count)
        {
            _entries[index].Label = label;
            _buttons[index].Text = label;
        }
    }
}
