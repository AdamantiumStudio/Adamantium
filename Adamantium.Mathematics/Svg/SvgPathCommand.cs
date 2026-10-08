using System.Collections.Generic;

namespace Adamantium.Mathematics.Svg;

/// <summary>One command of SVG path data as written: its letter (lowercase for relative) and every argument it was given,
/// a repeated command's sets one after another.</summary>
public readonly struct SvgPathCommand
{
    public SvgPathCommand(char letter, IReadOnlyList<double> arguments)
    {
        Letter = letter;
        Arguments = arguments;
    }

    /// <summary>The command's letter: M, L, H, V, C, S, Q, T, A or Z, lowercase when relative.</summary>
    public char Letter { get; }

    /// <summary>The command's arguments; an arc's flags as 0 or 1.</summary>
    public IReadOnlyList<double> Arguments { get; }
}
