using System.Globalization;

namespace Adamantium.Mathematics.Svg;

internal sealed class SvgNumberReader
{
    private readonly string text;
    private int position;

    public SvgNumberReader(string text)
    {
        this.text = text ?? string.Empty;
    }

    public bool AtEnd
    {
        get
        {
            SkipSeparators();
            return position >= text.Length;
        }
    }

    public char Peek()
    {
        SkipSeparators();
        return position < text.Length ? text[position] : '\0';
    }

    public char Take()
    {
        SkipSeparators();
        return position < text.Length ? text[position++] : '\0';
    }

    public bool TryReadNumber(out double value)
    {
        value = 0;
        SkipSeparators();
        var start = position;
        if (position < text.Length && text[position] is '+' or '-')
        {
            position++;
        }

        var digits = false;
        while (position < text.Length && char.IsDigit(text[position]))
        {
            position++;
            digits = true;
        }

        if (position < text.Length && text[position] == '.')
        {
            position++;
            while (position < text.Length && char.IsDigit(text[position]))
            {
                position++;
                digits = true;
            }
        }

        if (digits && position < text.Length && text[position] is 'e' or 'E')
        {
            var mark = position;
            position++;
            if (position < text.Length && text[position] is '+' or '-')
            {
                position++;
            }

            var exponent = false;
            while (position < text.Length && char.IsDigit(text[position]))
            {
                position++;
                exponent = true;
            }

            if (!exponent)
            {
                position = mark;
            }
        }

        if (!digits)
        {
            position = start;
            return false;
        }

        return double.TryParse(text.Substring(start, position - start), NumberStyles.Float, CultureInfo.InvariantCulture,
            out value);
    }

    public bool TryReadFlag(out double flag)
    {
        flag = 0;
        SkipSeparators();
        if (position < text.Length && text[position] is '0' or '1')
        {
            flag = text[position++] - '0';
            return true;
        }

        return false;
    }

    private void SkipSeparators()
    {
        while (position < text.Length && (char.IsWhiteSpace(text[position]) || text[position] == ','))
        {
            position++;
        }
    }
}
