namespace Adamantium.Fonts;

/// <summary>What a gradient does past its first and last stop.</summary>
public enum ColorExtend
{
    /// <summary>The end colors continue.</summary>
    Pad,

    /// <summary>The stops repeat.</summary>
    Repeat,

    /// <summary>The stops repeat, every other time backwards.</summary>
    Reflect,
}
