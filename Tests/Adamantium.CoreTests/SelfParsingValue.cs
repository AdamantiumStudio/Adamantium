namespace Adamantium.CoreTests;

public readonly struct SelfParsingValue
{
    public SelfParsingValue(int value)
    {
        Value = value;
    }

    public int Value { get; }

    public static SelfParsingValue Parse(string text) => new(int.Parse(text) * 2);
}
