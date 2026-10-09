using System;

namespace Adamantium.Graphics.Fonts;

/// <summary>A place a tab moves the text to: how far from the start of the line, how the text after the tab lines up
/// with it, and what fills the gap the tab leaves, as dots before a page number in a table of contents.</summary>
public readonly struct TabStop : IEquatable<TabStop>
{
    public TabStop(double position, TabAlignment alignment = TabAlignment.Left, string leader = null, char alignOn = '.')
    {
        if (double.IsNaN(position) || double.IsInfinity(position) || position < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, "A tab stop stands at a finite distance, zero or more, from the start of the line.");
        }

        Position = position;
        Alignment = alignment;
        Leader = string.IsNullOrEmpty(leader) ? null : leader;
        AlignOn = alignOn;
    }

    /// <summary>How far the stop is from the start of the line, in the layout's units.</summary>
    public double Position { get; }

    public TabAlignment Alignment { get; }

    /// <summary>The characters repeated across the gap the tab leaves, lined up from line to line; null leaves it empty.</summary>
    public string Leader { get; }

    /// <summary>The character a <see cref="TabAlignment.Decimal"/> stop lines the text up on.</summary>
    public char AlignOn { get; }

    public bool Equals(TabStop other) =>
        Position.Equals(other.Position) && Alignment == other.Alignment && Leader == other.Leader && AlignOn == other.AlignOn;

    public override bool Equals(object obj) => obj is TabStop other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = Position.GetHashCode();
            hash = hash * 397 ^ (int)Alignment;
            hash = hash * 397 ^ (Leader?.GetHashCode() ?? 0);
            return hash * 397 ^ AlignOn;
        }
    }

    public override string ToString() => FormattableString.Invariant($"{Position} {Alignment}");
}
