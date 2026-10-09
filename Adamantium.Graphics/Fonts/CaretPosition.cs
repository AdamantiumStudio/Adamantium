namespace Adamantium.Graphics.Fonts;

/// <summary>A place of the caret in laid-out text: before the UTF-16 character at <see cref="Index"/>, or after the one
/// before it. The two differ where the text changes direction.</summary>
public readonly struct CaretPosition
{
    /// <summary>The caret before the character at <paramref name="index"/>, or after the one before it when
    /// <paramref name="afterPrevious"/>.</summary>
    public CaretPosition(int index, bool afterPrevious = false)
    {
        Index = index;
        AfterPrevious = afterPrevious;
    }

    /// <summary>The UTF-16 offset in the text where typing would insert.</summary>
    public int Index { get; }

    /// <summary>Whether the caret stands on the trailing edge of the character before <see cref="Index"/>.</summary>
    public bool AfterPrevious { get; }
}
