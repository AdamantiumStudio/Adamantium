using System;
using System.Collections.Generic;

namespace Adamantium.Graphics.Fonts;

/// <summary>Text with attributes over ranges of it: features, language, colors, background and lines.</summary>
public sealed class AttributedText
{
    private readonly List<AttributeRun> _runs = [];

    public AttributedText(string text, TextAttributes defaults = null)
    {
        Text = text ?? string.Empty;
        Defaults = defaults ?? TextAttributes.Empty;
        if (Text.Length > 0)
        {
            _runs.Add(new AttributeRun(0, Text.Length, Defaults));
        }
    }

    public string Text { get; }

    public TextAttributes Defaults { get; }

    /// <summary>Stretches in text order that cover the text without overlapping, each with its final attributes.</summary>
    public IReadOnlyList<AttributeRun> Runs => _runs;

    /// <summary>Sets <paramref name="attributes"/> over a range: the fields it sets replace those there, the rest stay.</summary>
    public AttributedText Apply(int start, int length, TextAttributes attributes)
    {
        var end = Math.Min(start + length, Text.Length);
        start = Math.Max(start, 0);
        if (end <= start || attributes == null)
        {
            return this;
        }

        var result = new List<AttributeRun>(_runs.Count + 2);
        foreach (var run in _runs)
        {
            if (run.End <= start || run.Start >= end)
            {
                result.Add(run);
                continue;
            }

            if (run.Start < start)
            {
                result.Add(new AttributeRun(run.Start, start - run.Start, run.Attributes));
            }

            var from = Math.Max(run.Start, start);
            var to = Math.Min(run.End, end);
            result.Add(new AttributeRun(from, to - from, run.Attributes.With(attributes)));

            if (run.End > end)
            {
                result.Add(new AttributeRun(end, run.End - end, run.Attributes));
            }
        }

        _runs.Clear();
        _runs.AddRange(result);
        return this;
    }

    /// <summary>The attributes at a UTF-16 index.</summary>
    public TextAttributes AttributesAt(int index)
    {
        foreach (var run in _runs)
        {
            if (index >= run.Start && index < run.End)
            {
                return run.Attributes;
            }
        }

        return Defaults;
    }
}
