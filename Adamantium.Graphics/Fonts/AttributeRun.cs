namespace Adamantium.Graphics.Fonts;

/// <summary>A stretch of attributed text with one set of attributes, its defaults already applied.</summary>
public readonly struct AttributeRun
{
    public AttributeRun(int start, int length, TextAttributes attributes)
    {
        Start = start;
        Length = length;
        Attributes = attributes;
    }

    public int Start { get; }

    public int Length { get; }

    public int End => Start + Length;

    public TextAttributes Attributes { get; }
}
