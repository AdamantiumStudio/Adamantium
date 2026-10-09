namespace Adamantium.Graphics.Fonts;

/// <summary>How text wrapped by <see cref="TextWrapping.WrapByWords"/> is broken into lines.</summary>
public enum LineBreaking
{
    /// <summary>A line at a time: each takes as many words as fit, as a browser or a word processor breaks text.</summary>
    Greedy,

    /// <summary>A paragraph at a time, as TeX and InDesign's Paragraph Composer break it
    /// (<see cref="Adamantium.Fonts.Text.ParagraphComposer"/>): the lines are spaced evenly, a ragged edge stays even,
    /// and words are hyphenated only where that helps. Typing a word may change how lines above it break.</summary>
    Paragraph,
}
