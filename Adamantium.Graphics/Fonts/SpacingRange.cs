using System;
using System.Globalization;
using Adamantium.Core.TypeParsing;

namespace Adamantium.Graphics.Fonts;

/// <summary>How wide a space between words, or the space added between letters, may be, as InDesign's justification
/// sets them: a share of the font's space width, the least a justified line may squeeze it to, the width it takes
/// when it can, and the most a justified line may stretch it to (1 is the space's own width). In markup, percents:
/// <c>"80% 100% 133%"</c>, or one value for all three.</summary>
[TypeParser(typeof(SpacingRangeParser))]
public readonly struct SpacingRange : IEquatable<SpacingRange>
{
    public SpacingRange(double minimum, double desired, double maximum)
    {
        if (double.IsNaN(minimum) || double.IsNaN(desired) || double.IsNaN(maximum) || double.IsInfinity(minimum)
            || double.IsInfinity(desired) || double.IsInfinity(maximum) || minimum > desired || desired > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(desired), FormattableString.Invariant(
                $"A spacing range is finite and runs from its minimum through the desired width to its maximum, not {minimum}, {desired}, {maximum}."));
        }

        Minimum = minimum;
        Desired = desired;
        Maximum = maximum;
    }

    /// <summary>InDesign's word spacing: 80%, 100% and 133% of the space.</summary>
    public static SpacingRange Words => new(0.8, 1, 1.33);

    /// <summary>InDesign's letter spacing: nothing added between letters, and nothing a justified line may change.</summary>
    public static SpacingRange Letters => new(0, 0, 0);

    /// <summary>Reads <c>"80% 100% 133%"</c> (the minimum, desired and maximum, the % optional, spaces or commas
    /// between them) or a single value for all three.</summary>
    public static SpacingRange Parse(string text)
    {
        var parts = (text ?? string.Empty).Split([' ', ',', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is not (1 or 3))
        {
            throw new FormatException($"'{text}' is not a spacing range: give a minimum, a desired and a maximum percent.");
        }

        var values = new double[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!double.TryParse(parts[i].TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
            {
                throw new FormatException($"'{parts[i]}' in '{text}' is not a percent.");
            }

            values[i] /= 100;
        }

        try
        {
            return values.Length == 1
                ? new SpacingRange(values[0], values[0], values[0])
                : new SpacingRange(values[0], values[1], values[2]);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new FormatException($"'{text}' is not a spacing range: {exception.Message}", exception);
        }
    }

    public double Minimum { get; }

    public double Desired { get; }

    public double Maximum { get; }

    public bool Equals(SpacingRange other) =>
        Minimum.Equals(other.Minimum) && Desired.Equals(other.Desired) && Maximum.Equals(other.Maximum);

    public override bool Equals(object obj) => obj is SpacingRange other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Minimum, Desired, Maximum);

    public override string ToString() =>
        FormattableString.Invariant($"{Minimum * 100:G15}% {Desired * 100:G15}% {Maximum * 100:G15}%");
}
