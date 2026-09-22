using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace BattleCity.Duel.Core.Presentation;

/// <summary>A card name inside a log line: the text shown and, when the name may be inspected, the card it stands for and its owner (for the side colour).</summary>
public readonly record struct CardRef(string Name, Guid? Card, int Owner)
{
    public override string ToString() => Name;
}

/// <summary>A run of a log line: plain text, or a card name that links to <see cref="Card"/> (issue #205).</summary>
public sealed record LogSegment(string Text, Guid? Card = null, int Owner = 0)
{
    public bool IsLink => Card is not null;
}

/// <summary>
/// One line of the duel log as the HUD renders it: text segments with the
/// card references kept per name, so a name knows which card it is and
/// hidden information ("a face-down card") never carries a reference.
/// Built from an interpolated string through <see cref="Handler"/>.
/// </summary>
public sealed class LogLine
{
    private LogLine(IReadOnlyList<LogSegment> segments)
    {
        Segments = segments;
        Text = string.Concat(segments.Select(s => s.Text));
    }

    public IReadOnlyList<LogSegment> Segments { get; }

    /// <summary>The flat text, for a log that shows no links and for the tests.</summary>
    public string Text { get; }

    public static implicit operator LogLine(string text) => Plain(text);

    public static LogLine Plain(string text) => new(new[] { new LogSegment(text) });

    /// <summary>Builds a line; <see cref="CardRef"/> holes become links, everything else is formatted invariantly.</summary>
    public static LogLine Of(Handler handler) => new(handler.Segments);

    /// <summary>The names joined with ", ", each its own link.</summary>
    public static LogLine Names(IEnumerable<CardRef> refs)
    {
        ArgumentNullException.ThrowIfNull(refs);
        var segments = new List<LogSegment>();
        foreach (CardRef r in refs)
        {
            if (segments.Count > 0)
            {
                segments.Add(new LogSegment(", "));
            }

            segments.Add(new LogSegment(r.Name, r.Card, r.Owner));
        }

        return new LogLine(segments);
    }

    public override string ToString() => Text;

    /// <summary>The interpolated string handler behind <see cref="Of"/>: literals and formatted values are text, <see cref="CardRef"/> values are links.</summary>
    [InterpolatedStringHandler]
    public ref struct Handler
    {
        private readonly List<LogSegment> _segments;
        private readonly StringBuilder _text;

        public Handler(int literalLength, int formattedCount)
        {
            _segments = new List<LogSegment>(formattedCount * 2 + 1);
            _text = new StringBuilder(literalLength);
        }

        internal IReadOnlyList<LogSegment> Segments
        {
            get
            {
                Flush();
                return _segments;
            }
        }

        public void AppendLiteral(string s) => _text.Append(s);

        public void AppendFormatted(CardRef card)
        {
            Flush();
            _segments.Add(new LogSegment(card.Name, card.Card, card.Owner));
        }

        public void AppendFormatted(LogLine line)
        {
            Flush();
            _segments.AddRange(line.Segments);
        }

        public void AppendFormatted(string? s) => _text.Append(s);

        public void AppendFormatted<T>(T value) => _text.Append(Convert.ToString(value, CultureInfo.InvariantCulture));

        private void Flush()
        {
            if (_text.Length > 0)
            {
                _segments.Add(new LogSegment(_text.ToString()));
                _text.Clear();
            }
        }
    }
}
