using System.Collections.Generic;

namespace Adamantium.Mathematics.Svg;

/// <summary>SVG's number and path grammar: numbers separated by commas, spaces or by nothing at all ("1-2.5.5" is three,
/// an exponent's sign belongs to it), and path data read into its commands.</summary>
public static class SvgPathData
{
    /// <summary>The commands of <paramref name="data"/> (a path's <c>d</c>), each with its arguments. An arc's flags are
    /// one digit each, so "a1 1 0 11 5 5" reads them; reading stops at the first thing that is neither a command nor an
    /// argument, keeping what came before, as SVG renders a path up to its first error.</summary>
    public static List<SvgPathCommand> Parse(string data)
    {
        var commands = new List<SvgPathCommand>();
        var reader = new SvgNumberReader(data);
        while (!reader.AtEnd)
        {
            var letter = reader.Take();
            var arity = Arity(letter);
            if (arity < 0)
            {
                break;
            }

            var arguments = new List<double>();
            if (arity > 0)
            {
                var complete = true;
                do
                {
                    var set = new double[arity];
                    for (var i = 0; i < arity && complete; i++)
                    {
                        var isFlag = arity == 7 && i is 3 or 4;
                        complete = isFlag ? reader.TryReadFlag(out set[i]) : reader.TryReadNumber(out set[i]);
                    }

                    if (complete)
                    {
                        arguments.AddRange(set);
                    }
                }
                while (complete && IsNumberStart(reader.Peek()));

                if (!complete)
                {
                    if (arguments.Count > 0)
                    {
                        commands.Add(new SvgPathCommand(letter, arguments));
                    }

                    break;
                }
            }

            commands.Add(new SvgPathCommand(letter, arguments));
        }

        return commands;
    }

    /// <summary>The numbers of <paramref name="text"/>, as a transform's arguments or a polygon's points are written.</summary>
    public static List<double> ReadNumbers(string text)
    {
        var numbers = new List<double>();
        var reader = new SvgNumberReader(text);
        while (reader.TryReadNumber(out var number))
        {
            numbers.Add(number);
        }

        return numbers;
    }

    private static bool IsNumberStart(char c) => c is >= '0' and <= '9' or '-' or '+' or '.';

    private static int Arity(char letter)
    {
        switch (char.ToUpperInvariant(letter))
        {
            case 'Z':
                return 0;
            case 'H':
            case 'V':
                return 1;
            case 'M':
            case 'L':
            case 'T':
                return 2;
            case 'S':
            case 'Q':
                return 4;
            case 'C':
                return 6;
            case 'A':
                return 7;
            default:
                return -1;
        }
    }
}
