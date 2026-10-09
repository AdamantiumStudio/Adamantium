namespace Adamantium.Fonts;

/// <summary>The two key instances a font on the way between two sets of axis values is drawn between, and how far it
/// is from the first to the second (<see cref="IFont.GetInstance(System.Collections.Generic.IReadOnlyList{FontVariation},System.Collections.Generic.IReadOnlyList{FontVariation},float)"/>).</summary>
public sealed class FontBlend
{
    /// <summary>The blend of two key instances.</summary>
    public FontBlend(IFont from, IFont to, float amount)
    {
        From = from;
        To = to;
        Amount = amount;
    }

    /// <summary>The key instance before the point.</summary>
    public IFont From { get; }

    /// <summary>The key instance after the point.</summary>
    public IFont To { get; }

    /// <summary>How far the point is from <see cref="From"/> to <see cref="To"/>: 0 at the first, 1 at the second.</summary>
    public float Amount { get; }
}
