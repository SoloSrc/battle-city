using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using BattleCity.Duel.Core.Presentation;
using Godot;

namespace BattleCity.DuelScene;

/// <summary>
/// The duel log (issue #205): every line of the duel, card names set apart
/// as links in their owner's side colour, a scrollbar, and a cursor for the
/// gamepad and keyboard. Hovering a name with the mouse, or moving the
/// cursor onto one, raises <see cref="CardHovered"/> so the HUD shows the
/// card in its inspector. New lines keep the log pinned to the bottom
/// unless the player scrolled up; then a "new lines" marker appears and
/// <see cref="JumpToLatest"/> (Interact) returns to the end.
/// </summary>
public partial class DuelLogPanel : PanelContainer
{
    private readonly List<LogLine> _lines = new();
    private readonly List<List<Guid>> _links = new();
    private Color _playerColor = new(0.55f, 0.9f, 1.0f);
    private Color _opponentColor = new(1.0f, 0.65f, 0.45f);
    private RichTextLabel _text = null!;
    private Button _marker = null!;
    private int _player;
    private int _cursorLine = -1;
    private int _cursorLink = -1;
    private bool _focused;
    private int _pending;
    private Guid? _mouseCard;

    /// <summary>The card under the mouse or the cursor changed; null when neither points at a name.</summary>
    public event Action<Guid?>? CardHovered;

    public IReadOnlyList<LogLine> Lines => _lines;

    /// <summary>Line of the cursor when the panel has focus, −1 otherwise.</summary>
    public int CursorLine => _focused ? _cursorLine : -1;

    /// <summary>The card the cursor's link stands for, when the panel has focus and the cursor is on a name.</summary>
    public Guid? CursorCard => _focused && _cursorLine >= 0 && _cursorLink >= 0 && _cursorLink < _links[_cursorLine].Count ? _links[_cursorLine][_cursorLink] : null;

    /// <summary>The card the mouse is over, when any.</summary>
    public Guid? MouseCard => _mouseCard;

    /// <summary>Lines added while the player was scrolled up, not yet seen.</summary>
    public int PendingLines => _pending;

    /// <summary>Whether the view is at the end of the log.</summary>
    public bool AtBottom
    {
        get
        {
            VScrollBar bar = _text.GetVScrollBar();
            return bar.Value + bar.Page >= bar.MaxValue - 1.0;
        }
    }

    /// <summary>Whether the view shows the first line of the duel.</summary>
    public bool AtTop => _text.GetVScrollBar().Value <= 0.0;

    /// <summary>Whether new lines keep the view at the end (the player has not scrolled up).</summary>
    public bool Pinned => _text.ScrollFollowing;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 4);
        AddChild(column);
        _text = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollActive = true,
            ScrollFollowing = true,
            FitContent = false,
            SelectionEnabled = false,
            FocusMode = FocusModeEnum.None,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Stop,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _text.AddThemeFontSizeOverride("normal_font_size", 15);
        _text.MetaHoverStarted += meta => SetMouseCard(Parse(meta));
        _text.MetaHoverEnded += _ => SetMouseCard(null);
        _text.MouseExited += () => SetMouseCard(null);
        // The player scrolling (wheel or scrollbar) unpins the log unless they land at the end again; layout changes never touch the pin.
        _text.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown })
            {
                CallDeferred(MethodName.PinFromView);
            }
        };
        _text.GetVScrollBar().Scrolling += PinFromView;
        column.AddChild(_text);
        _marker = new Button { Visible = false, FocusMode = FocusModeEnum.None };
        _marker.AddThemeFontSizeOverride("font_size", 14);
        _marker.Pressed += JumpToLatest;
        column.AddChild(_marker);
        Render();
    }

    /// <summary>Which seat reads the log (its cards take the player colour) and the side colours.</summary>
    public void Configure(int player, Color playerColor, Color opponentColor)
    {
        _player = player;
        _playerColor = playerColor;
        _opponentColor = opponentColor;
        Render();
    }

    public void Add(LogLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        bool pinned = !IsNodeReady() || _text.ScrollFollowing;
        _lines.Add(line);
        _links.Add(line.Segments.Where(s => s.IsLink).Select(s => s.Card!.Value).ToList());
        if (!IsNodeReady())
        {
            return;
        }

        Render();
        if (!pinned)
        {
            _pending++;
            _marker.Visible = true;
            _marker.Text = _pending == 1 ? "▼ 1 new line" : string.Format(CultureInfo.InvariantCulture, "▼ {0} new lines", _pending);
        }
    }

    public void Clear()
    {
        _lines.Clear();
        _links.Clear();
        _cursorLine = -1;
        _cursorLink = -1;
        ClearPending();
        SetMouseCard(null);
        if (IsNodeReady())
        {
            Render();
        }
    }

    /// <summary>Takes the gamepad cursor: it starts on the newest line.</summary>
    public void Focus()
    {
        _focused = true;
        _cursorLine = _lines.Count - 1;
        _cursorLink = _links.Count > 0 && _links[^1].Count > 0 ? 0 : -1;
        Render();
        RaiseHover();
    }

    public void Unfocus()
    {
        _focused = false;
        Render();
        RaiseHover();
    }

    /// <summary>Moves the cursor: <paramref name="dy"/> across lines, <paramref name="dx"/> across the names of the line; the view follows.</summary>
    public void Navigate(int dx, int dy)
    {
        if (!_focused || _lines.Count == 0)
        {
            return;
        }

        if (dy != 0)
        {
            _cursorLine = Math.Clamp(_cursorLine + dy, 0, _lines.Count - 1);
            _cursorLink = _links[_cursorLine].Count > 0 ? 0 : -1;
        }

        if (dx != 0 && _links[_cursorLine].Count > 0)
        {
            _cursorLink = Math.Clamp(_cursorLink + dx, 0, _links[_cursorLine].Count - 1);
        }

        Render();
        ScrollToCursor();
        _text.ScrollFollowing = _cursorLine == _lines.Count - 1;
        if (_text.ScrollFollowing)
        {
            ClearPending();
        }

        RaiseHover();
    }

    /// <summary>Back to the newest line (the marker's press, Interact with the log focused).</summary>
    public void JumpToLatest()
    {
        if (_focused)
        {
            _cursorLine = _lines.Count - 1;
            _cursorLink = _links.Count > 0 && _links[^1].Count > 0 ? 0 : -1;
            Render();
            RaiseHover();
        }

        ScrollToEnd();
        ClearPending();
    }

    public void ScrollToTop()
    {
        _text.ScrollFollowing = false;
        _text.GetVScrollBar().Value = 0.0;
    }

    private void ScrollToCursor()
    {
        if (_cursorLine >= 0 && _cursorLine < _lines.Count)
        {
            _text.ScrollToLine(_cursorLine);
        }
    }

    private void ScrollToEnd()
    {
        _text.ScrollFollowing = true;
        VScrollBar bar = _text.GetVScrollBar();
        bar.Value = bar.MaxValue;
    }

    private void PinFromView()
    {
        _text.ScrollFollowing = AtBottom;
        if (_text.ScrollFollowing)
        {
            ClearPending();
        }
    }

    private void ClearPending()
    {
        _pending = 0;
        _marker.Visible = false;
    }

    private void SetMouseCard(Guid? card)
    {
        if (_mouseCard == card)
        {
            return;
        }

        _mouseCard = card;
        RaiseHover();
    }

    private void RaiseHover() => CardHovered?.Invoke(_mouseCard ?? CursorCard);

    private static Guid? Parse(Variant meta) => Guid.TryParse(meta.AsString(), out Guid id) ? id : null;

    private void Render()
    {
        if (_text is null)
        {
            return;
        }

        if (_lines.Count == 0)
        {
            _text.Text = "(nothing yet)";
            return;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < _lines.Count; i++)
        {
            if (i > 0)
            {
                sb.Append('\n');
            }

            bool cursorLine = _focused && i == _cursorLine;
            if (cursorLine)
            {
                sb.Append("[bgcolor=#ffffff22]");
            }

            int link = 0;
            foreach (LogSegment segment in _lines[i].Segments)
            {
                if (!segment.IsLink)
                {
                    sb.Append(Escape(segment.Text));
                    continue;
                }

                Color color = segment.Owner == _player ? _playerColor : _opponentColor;
                bool current = cursorLine && link == _cursorLink;
                sb.Append("[color=#").Append(color.ToHtml(false)).Append(']');
                if (current)
                {
                    sb.Append("[bgcolor=#").Append(color.ToHtml(false)).Append("55]");
                }

                sb.Append("[url=").Append(segment.Card!.Value.ToString("N")).Append("][u]").Append(Escape(segment.Text)).Append("[/u][/url]");
                if (current)
                {
                    sb.Append("[/bgcolor]");
                }

                sb.Append("[/color]");
                link++;
            }

            if (cursorLine)
            {
                sb.Append("[/bgcolor]");
            }
        }

        _text.Text = sb.ToString();
    }

    private static string Escape(string text) => text.Replace("[", "[lb]", StringComparison.Ordinal);
}
