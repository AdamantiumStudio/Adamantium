using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Adamantium.Core;
using Adamantium.Fonts;
using Adamantium.Fonts.Shaping;
using Adamantium.Fonts.Text;
using Adamantium.Fonts.TextureGeneration;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;

namespace Adamantium.Graphics.Fonts;

public class TextLayout : DisposableObject
{
    public Guid Guid { get; }

    public Typeface Typeface { get; }
    public IFont Font { get; }

    public uint ElementsCount { get; private set; }

    private Glyph spaceGlyph;
    private Glyph dotGlyph;
    private double dotGlyphsWidth;
    public Size CalculatedLayoutSize { get; private set; }

    /// <summary>
    /// Emits a zero-width glyph for each newline, so a text editor can place the caret there. Skipped by rendering; off
    /// by default.
    /// </summary>
    public bool EmitNewlineCarets { get; set; }

    /// <summary>
    /// How many spaces wide a tab stop is: a tab moves the pen to the next multiple of that width from the line's
    /// start. 4 by default.
    /// </summary>
    public int TabSize { get; set; } = 4;

    /// <summary>
    /// Where tabs stop, in the layout's units from the start of the line, each with how the text after it lines up
    /// and what fills the gap; past the last of them, and when there are none, a tab stops every
    /// <see cref="TabSize"/> spaces.
    /// </summary>
    public IReadOnlyList<TabStop> TabStops { get; set; }

    /// <summary>
    /// Which way the text's paragraphs run: left to right, right to left, or <see cref="TextDirection.Auto"/> (the
    /// default), each paragraph by its first strong character. Right-to-left text inside runs right to left either way,
    /// by the Unicode Bidirectional Algorithm.
    /// </summary>
    public TextDirection Direction { get; set; } = TextDirection.Auto;

    /// <summary>
    /// Where words wrapped by <see cref="TextWrapping.WrapByWords"/> may break across lines:
    /// <see cref="Fonts.Hyphens.Manual"/> (the default) at soft hyphens only.
    /// </summary>
    public Hyphens Hyphens { get; set; } = Hyphens.Manual;

    /// <summary>
    /// How words wrapped by <see cref="TextWrapping.WrapByWords"/> are broken into lines: a line at a time
    /// (<see cref="Fonts.LineBreaking.Greedy"/>, the default) or a paragraph at a time.
    /// </summary>
    public LineBreaking LineBreaking { get; set; } = LineBreaking.Greedy;

    /// <summary>
    /// Hangs punctuation and hyphens at the edges of lines partly past the margin, as InDesign's Optical Margin
    /// Alignment does, so the text's edge looks straight: the start of lines aligned left or justified, the end of
    /// lines aligned right or justified. Lines of right-to-left paragraphs and centered lines are left as they are.
    /// False by default.
    /// </summary>
    public bool OpticalMarginAlignment { get; set; }

    /// <summary>
    /// The text's language, a BCP 47 tag such as "ru" or "en-GB", for ranges whose attributes name none: picks the
    /// hyphenation patterns, the font's localized glyph forms and the fallback fonts. Null when unknown.
    /// </summary>
    public string Language { get; set; }

    /// <summary>
    /// Where a character the text's font lacks is drawn from: the operating system's fallback fonts unless set; null
    /// draws the font's own missing-glyph box instead.
    /// </summary>
    public FontFallback Fallback
    {
        get => _fallbackSet ? _fallback : FontFallback.System;
        set
        {
            _fallback = value;
            _fallbackSet = true;
        }
    }

    /// <summary>
    /// True to never wait for a fallback font's file to be parsed: a character whose font is still loading takes the
    /// room of the text font's missing glyph and draws nothing, <see cref="HasPendingFonts"/> turns true, and the file
    /// parses on a worker that raises <see cref="TypefaceStore.Loaded"/>; the text is laid out again then. False (the
    /// default) waits, as a render with no next frame must.
    /// </summary>
    public bool LoadFontsInBackground { get; set; }

    /// <summary>True when the last layout drew some characters without their font, which was still loading
    /// (<see cref="LoadFontsInBackground"/>).</summary>
    public bool HasPendingFonts { get; private set; }

    private const char SoftHyphen = '­';

    private static readonly Dictionary<char, (double Left, double Right)> MarginHangs = new()
    {
        ['.'] = (0, 0.7), [','] = (0, 0.7), [':'] = (0, 0.5), [';'] = (0, 0.5), ['!'] = (0, 0.2), ['?'] = (0, 0.2),
        ['…'] = (0, 0.3), ['-'] = (0.7, 0.7), ['‐'] = (0.7, 0.7), ['‑'] = (0.7, 0.7),
        ['–'] = (0.5, 0.5), ['—'] = (0.3, 0.3), ['"'] = (0.5, 0.5), ['\''] = (0.5, 0.5),
        ['“'] = (0.7, 0.3), ['”'] = (0.3, 0.7), ['‘'] = (0.7, 0.3), ['’'] = (0.3, 0.7),
        ['„'] = (0.5, 0.5), ['‚'] = (0.5, 0.5), ['«'] = (0.5, 0.5), ['»'] = (0.5, 0.5),
        ['‹'] = (0.5, 0.5), ['›'] = (0.5, 0.5), ['('] = (0.1, 0), [')'] = (0, 0.1), ['['] = (0.1, 0),
        [']'] = (0, 0.1),
    };

    private TextRenderingParameters _previousRenderingParameters;
    private int _laidOutTabSize;
    private TextDirection _laidOutDirection;
    private Hyphens _laidOutHyphens;
    private LineBreaking _laidOutLineBreaking;
    private bool _laidOutOpticalMargins;
    private TabStop[] _laidOutTabStops = [];
    private string _laidOutLanguage;
    private List<(int Start, int End, BidiParagraph Paragraph)> _paragraphs;
    private byte[] _characterLevels;
    private FontFallback _fallback;
    private bool _fallbackSet;

    private List<GlyphWordData> _wordData;
    private AttributedText _attributed;
    private double[] _lineTops;
    private double[] _lineHeights;
    private double[] _lineBaselines;
    private double _verticalShift;
    private CaretStop[] _caretStops;
    private int[] _lineStarts;
    private bool[] _graphemes;
    private bool[] _words;
    private FontItem[] fontItems;

    public TextRenderingParameters RenderingParameters { get; private set; }
    public Buffer<FontItem> VertexBuffer { get; private set; }

    public FontAtlas FontAtlas { get; private set; }

    public string Text { get; private set; }

    public float FontSize { get; private set; }

    /// <summary>
    /// How far (screen px) a glyph's effect (outline/glow/shadow) reaches beyond its body: the atlas margin at the
    /// current font size. 0 before the atlas is built.
    /// </summary>
    public int EffectPadding => FontAtlas == null
        ? 0
        : (int)Math.Ceiling(FontAtlas.GlyphMargin * FontSize / FontAtlas.MSDFTextureSize);

    public Size RealTextDimensions { get; private set; }

    private bool _textUpdated;

    // Behind the atlas version: glyphs have arrived since, and the quads must be built again.
    private int _atlasVersion = -1;
    private bool _vertexBufferDirty;

    public TextLayout(Typeface typeface, IFont font)
    {
        Guid = Guid.NewGuid();
        Typeface = typeface;
        Font = font;
        spaceGlyph = font.GetGlyphByCharacter(' ');
        dotGlyph = font.GetGlyphByCharacter('.');

        // Grown to fit: a block holds what it draws, not a preallocated 256KB per TextBlock.
        fontItems = Array.Empty<FontItem>();
    }

    private void EnsureItemCapacity(int needed)
    {
        if (needed <= fontItems.Length) return;

        var size = Math.Max(16, fontItems.Length);
        while (size < needed) size *= 2;
        Array.Resize(ref fontItems, size);
    }

    public GlyphWordData[] GetTextData()
    {
        return _wordData.ToArray();
    }

    // Takes the list explicitly: it is called mid-shaping, on data not yet published as _wordData.
    private void CalculateRealTextDimensions(List<GlyphWordData> glyphsData)
    {
        var minX = glyphsData.Min(InkLeft);
        var maxX = glyphsData.Max(InkRight);
        var minY = glyphsData.Min(x => x.Rect.Top);
        var maxY = glyphsData.Max(x => x.Rect.Bottom);
        RealTextDimensions = new Size(maxX - minX, maxY - minY);
    }

    private bool CompareInputParameters(string text,
        double fontSize,
        TextRenderingParameters renderingParameters)
    {
        return Text == text && MathHelper.IsZero(FontSize - fontSize) &&
               _previousRenderingParameters == renderingParameters && _laidOutTabSize == TabSize &&
               _laidOutDirection == Direction && _laidOutHyphens == Hyphens && _laidOutLanguage == Language &&
               _laidOutLineBreaking == LineBreaking && _laidOutTabStops.SequenceEqual(TabStops ?? []) &&
               _laidOutOpticalMargins == OpticalMarginAlignment;
    }

    public Size ProcessText(string text,
        double fontSize,
        Size textArea,
        TextWrapping textWrapping,
        TextTrimming textTrimming,
        HorizontalTextAlignment horizontalTextAlignment,
        VerticalTextAlignment verticalTextAlignment,
        bool justifyLastLine = false)
    {
        var @params = Parameters(textArea, textWrapping, textTrimming, horizontalTextAlignment, verticalTextAlignment,
            justifyLastLine);

        if (_attributed == null && CompareInputParameters(text, fontSize, @params))
            return CalculatedLayoutSize;

        _attributed = null;
        Text = text;
        FontSize = (float)fontSize;
        _previousRenderingParameters = @params;

        _textUpdated = true;
        return ProcessText(text, fontSize, @params);
    }

    /// <summary>Lays out text whose ranges carry their own features, language, colors, background and lines.</summary>
    public Size ProcessText(AttributedText text,
        double fontSize,
        Size textArea,
        TextWrapping textWrapping,
        TextTrimming textTrimming,
        HorizontalTextAlignment horizontalTextAlignment,
        VerticalTextAlignment verticalTextAlignment,
        bool justifyLastLine = false)
    {
        var @params = Parameters(textArea, textWrapping, textTrimming, horizontalTextAlignment, verticalTextAlignment,
            justifyLastLine);

        _attributed = text;
        Text = text.Text;
        FontSize = (float)fontSize;
        _previousRenderingParameters = @params;

        _textUpdated = true;
        return ProcessText(text.Text, fontSize, @params);
    }

    /// <summary>Whether the paragraph holding UTF-16 <paramref name="index"/> of the text last laid out runs right to
    /// left: its lines start on the right, where <see cref="HorizontalTextAlignment.Left"/> puts them.</summary>
    public bool IsRightToLeftParagraph(int index)
    {
        if (_paragraphs == null || string.IsNullOrEmpty(Text))
        {
            return _laidOutDirection == TextDirection.RightToLeft;
        }

        return (ParagraphAt(Math.Max(0, Math.Min(index, Text.Length - 1))).Paragraph.BaseLevel & 1) == 1;
    }

    /// <summary>The attributed text last laid out; null when it was a plain string.</summary>
    public AttributedText AttributedText => _attributed;

    private static TextRenderingParameters Parameters(Size textArea, TextWrapping textWrapping,
        TextTrimming textTrimming, HorizontalTextAlignment horizontalTextAlignment,
        VerticalTextAlignment verticalTextAlignment, bool justifyLastLine)
    {
        if (Double.IsNaN(textArea.Width))
        {
            textArea.Width = Int32.MaxValue;
        }

        if (Double.IsNaN(textArea.Height))
        {
            textArea.Height = Int32.MaxValue;
        }

        return new TextRenderingParameters
        {
            HorizontalTextAlignment = horizontalTextAlignment,
            VerticalTextAlignment = verticalTextAlignment,
            JustifyLastLine = justifyLastLine,
            TextWrapping = textWrapping,
            TextTrimming = textTrimming,
            TextArea = new Rectangle(Vector2F.Zero, textArea)
        };
    }

    /// <summary>What laying out one string allocates, by stage. Cumulative.</summary>
    public static long TranslateBytes;
    public static long FeatureBytes;
    public static long WordLoopBytes;
    public static long TailBytes;
    public static int ProcessCount;

    public Size ProcessText(string text, double fontSize, TextRenderingParameters renderingParameters)
    {
        // Empty text clears the previous glyphs, or a recycled row keeps drawing its old text.
        if (string.IsNullOrEmpty(text))
        {
            _wordData?.Clear();
            ElementsCount = 0;
            CalculatedLayoutSize = Size.Zero;
            RealTextDimensions = Size.Zero;
            _textUpdated = true;
            _vertexBufferDirty = true;
            return Size.Zero;
        }

        RenderingParameters = renderingParameters;
        _graphemes = null;

        var _b0 = System.GC.GetAllocatedBytesForCurrentThread();


        var _b1 = System.GC.GetAllocatedBytesForCurrentThread();

        var scale = fontSize / Font.UnitsPerEm;
        _laidOutDirection = Direction;
        _laidOutHyphens = Hyphens;
        _laidOutLanguage = Language;
        _laidOutLineBreaking = LineBreaking;
        _laidOutOpticalMargins = OpticalMarginAlignment;
        _laidOutTabStops = TabStops?.ToArray() ?? [];
        var tabStops = _laidOutTabStops.OrderBy(stop => stop.Position).ToArray();
        _paragraphs = ResolveParagraphs(text);
        _characterLevels = null;
        var items = Shape(text, fontSize);
        _laidOutTabSize = TabSize;
        var tabStop = Font.GetAdvanceWidth(spaceGlyph.Index) * scale * Math.Max(TabSize, 1);

        var _b2 = System.GC.GetAllocatedBytesForCurrentThread();

        var lineHeight = LineAdvance(Font, fontSize);
        var baseLine = BaselineInLine(Font, scale);

        dotGlyphsWidth = Font.GetAdvanceWidth(dotGlyph.Index) * scale * 3;

        var textArea = renderingParameters.TextArea;
        double width = 0;
        double height = textArea.Y;
        double cursorPosition = 0;
        double wordStartPosition = 0;
        var glyphsData = new List<GlyphWordData>();
        var leaders = new Dictionary<GlyphWordData, string>();
        int wordIndex = 0;
        int lineIndex = 0;
        double verticalShift = 0;
        var lineBreaks = renderingParameters.TextWrapping == TextWrapping.WrapByWords
            ? TextBoundaries.LineBreaks(text)
            : null;
        for (var i = 1; lineBreaks != null && i < text.Length; i++)
        {
            if (text[i - 1] == SoftHyphen && lineBreaks[i] == LineBreakKind.Allowed)
            {
                lineBreaks[i] = LineBreakKind.None;
            }
        }

        HashSet<int> composedStarts = null;
        HashSet<int> composedCuts = null;
        if (lineBreaks != null && LineBreaking == LineBreaking.Paragraph && textArea.Width < Int32.MaxValue)
        {
            Compose();
        }

        var wordStart = 0;
        while (wordStart <= items.Count)
        {
            var wordEnd = WordEnd(wordStart);
            if (!ProcessWord(wordStart, wordEnd))
            {
                break;
            }

            if (wordEnd < items.Count && !IsBlank(items[wordEnd].Symbol))
            {
                wordIndex++;
                wordStart = wordEnd;
                continue;
            }

            if (wordEnd < items.Count)
            {
                // Sub-pixel like the glyphs, so the space doesn't inflate the line bounds. A tab draws nothing and
                // reaches the next tab stop.
                var space = items[wordEnd];
                var isTab = space.Symbol == '\t';
                var stop = isTab ? NextTabStop(cursorPosition) : default;
                var advance = isTab ? TabAdvance(stop, wordEnd) : space.Advance;
                var rect = new RectangleF((float)cursorPosition,
                    (float)(height + baseLine),
                    (float)advance,
                    0f);
                var blank = new GlyphWordData(isTab ? spaceGlyph : space.Glyph, space.Symbol, rect, space.Cluster, lineIndex)
                {
                    PenX = cursorPosition,
                    Advance = advance,
                    Attributes = space.Attributes,
                    Font = space.Font,
                    FontSize = space.FontSize,
                };
                glyphsData.Add(blank);
                if (isTab && stop.Leader != null)
                {
                    leaders[blank] = stop.Leader;
                }

                cursorPosition += advance;
            }

            wordIndex++;
            wordStart = wordEnd + 1;
        }

        ReorderLines(glyphsData);

        // Not published yet: the render thread reads _wordData, and the alignment below still moves every glyph.
        // The height is a font metric (the bottom of the last line, its descent included), not the ink, so same-size
        // strings measure alike and a turned label keeps its descenders. A line is as tall as its largest text.
        var lineCount = Math.Max(lineIndex, glyphsData.Count > 0 ? glyphsData.Max(x => x.LineIndex) : 0) + 1;
        var lineTops = new double[lineCount];
        var lineHeights = new double[lineCount];
        var lineBaselines = new double[lineCount];
        var lineAscents = new double[lineCount];
        PlaceLines();
        var lastBaseline = lineTops[lineCount - 1] + lineBaselines[lineCount - 1];
        height = lineTops[lineCount - 1] + lineHeights[lineCount - 1];

        var _b3 = System.GC.GetAllocatedBytesForCurrentThread();


        CalculateRealTextDimensions(glyphsData);

        var maxX = glyphsData.Max(InkRight);
        var finalRect = new Size(Math.Ceiling(maxX), Math.Ceiling(height));
        if (renderingParameters.TextArea.Width != Int32.MaxValue)
        {
            finalRect.Width = renderingParameters.TextArea.Width;
        }

        if (renderingParameters.TextArea.Height != Int32.MaxValue)
        {
            finalRect.Height = renderingParameters.TextArea.Height;
        }

        ArrangeText();
        for (var i = 0; i < lineCount; i++)
        {
            lineTops[i] += verticalShift;
        }

        if (leaders.Count > 0)
        {
            glyphsData = WithLeaders(glyphsData, leaders, textArea.Width < Int32.MaxValue ? textArea.Width : double.MaxValue);
        }

        _lineTops = lineTops;
        _lineHeights = lineHeights;
        _lineBaselines = lineBaselines;
        _verticalShift = verticalShift;
        _caretStops = null;
        _lineStarts = null;
        _words = null;

        var _b4 = System.GC.GetAllocatedBytesForCurrentThread();

        TranslateBytes += _b1 - _b0; FeatureBytes += _b2 - _b1; WordLoopBytes += _b3 - _b2; TailBytes += _b4 - _b3; ProcessCount++;

        // One reference write: a reader sees the previous layout or this one, never a half-aligned mixture.
        _wordData = glyphsData;

        CalculatedLayoutSize = finalRect;

        return CalculatedLayoutSize;

        TabStop NextTabStop(double x)
        {
            foreach (var stop in tabStops)
            {
                if (stop.Position > x + 1e-6)
                {
                    return stop;
                }
            }

            return new TabStop((Math.Floor(x / tabStop) + 1) * tabStop);
        }

        double TabAdvance(TabStop stop, int tab)
        {
            var advance = stop.Position - cursorPosition;
            if (stop.Alignment == TabAlignment.Left)
            {
                return advance;
            }

            double width = 0;
            double beforeSeparator = -1;
            for (var k = tab + 1; k < items.Count && items[k].Symbol is not ('\t' or '\n'); k++)
            {
                if (stop.Alignment == TabAlignment.Decimal && beforeSeparator < 0 && items[k].Symbol == stop.AlignOn)
                {
                    beforeSeparator = width;
                }

                width += items[k].Advance;
            }

            var shift = stop.Alignment switch
            {
                TabAlignment.Center => width / 2,
                TabAlignment.Decimal when beforeSeparator >= 0 => beforeSeparator,
                _ => width,
            };
            return Math.Max(advance - shift, 0);
        }

        List<GlyphWordData> WithLeaders(List<GlyphWordData> glyphs, Dictionary<GlyphWordData, string> tabLeaders, double limit)
        {
            var result = new List<GlyphWordData>(glyphs.Count + tabLeaders.Count * 16);
            foreach (var glyph in glyphs)
            {
                result.Add(glyph);
                if (!tabLeaders.TryGetValue(glyph, out var leader))
                {
                    continue;
                }

                var glyphScale = glyph.FontSize / glyph.Font.UnitsPerEm;
                var leaderGlyphs = leader.Select(symbol => glyph.Font.GetGlyphByCharacter(symbol)).ToArray();
                var advances = leaderGlyphs.Select(g => glyph.Font.GetAdvanceWidth(g.Index) * glyphScale).ToArray();
                var unit = advances.Sum();
                var to = Math.Min(glyph.PenX + glyph.Advance, limit);
                if (unit <= 0)
                {
                    continue;
                }

                for (var x = Math.Ceiling(glyph.PenX / unit) * unit; x + unit <= to + 1e-6; x += unit)
                {
                    var pen = x;
                    for (var g = 0; g < leaderGlyphs.Length; g++)
                    {
                        var rect = CalculateGlyphPosition(glyph.Font, leaderGlyphs[g], pen, glyph.Rect.Y - verticalShift,
                            glyphScale);
                        rect.Y += (float)verticalShift;
                        result.Add(new GlyphWordData(leaderGlyphs[g], leader[g], rect, -1, glyph.LineIndex)
                        {
                            PenX = pen,
                            Advance = advances[g],
                            Attributes = glyph.Attributes,
                            Font = glyph.Font,
                            FontSize = glyph.FontSize,
                        });
                        pen += advances[g];
                    }
                }
            }

            return result;
        }

        int WordEnd(int start)
        {
            var end = start;
            while (end < items.Count && !IsBlank(items[end].Symbol)
                   && (end == start || lineBreaks == null || lineBreaks[items[end].Cluster] == LineBreakKind.None))
            {
                end++;
            }

            return end;
        }

        void NewLine()
        {
            lineIndex++;
            height += lineHeight;
            cursorPosition = 0;
        }

        bool HasRoomBelow() => height + lineHeight < textArea.Height;

        void EndInEllipsis(TextTrimming trimming)
        {
            var floor = LineFirstGlyph();
            if (cursorPosition + dotGlyphsWidth <= textArea.Width)
            {
                TrimText(0, height + baseLine);
                return;
            }

            if (trimming == TextTrimming.WordEllipses && !HasBlank(floor))
            {
                trimming = TextTrimming.CharEllipses;
            }

            PrepareDataAndTrim(glyphsData.Count, floor, 0, height + baseLine, trimming);
        }

        bool ProcessComposedWord(int start, int end)
        {
            var newline = end > start && items[end - 1].Symbol == '\n' ? end - 1 : end;
            var trims = renderingParameters.TextTrimming != TextTrimming.None;
            if (composedStarts.Contains(start) && cursorPosition > 0)
            {
                if (HasRoomBelow())
                {
                    NewLine();
                }
                else if (trims)
                {
                    if (renderingParameters.TextTrimming == TextTrimming.CharEllipses)
                    {
                        LayItems(start, newline);
                    }

                    EndInEllipsis(renderingParameters.TextTrimming);
                    return false;
                }
            }

            if (cursorPosition > 0 && HasRoomBelow()
                && cursorPosition + WordWidth(start, newline) > textArea.Width + LineShrink())
            {
                NewLine();
            }

            for (var k = start + 1; k < newline; k++)
            {
                if (!composedCuts.Contains(k))
                {
                    continue;
                }

                if (!HasRoomBelow())
                {
                    if (trims)
                    {
                        LayItems(start, newline);
                        EndInEllipsis(TextTrimming.CharEllipses);
                        return false;
                    }

                    break;
                }

                LayItems(start, k);
                AddHyphen(items[k - 1]);
                NewLine();
                start = k;
            }

            wordStartPosition = cursorPosition;
            if (trims && cursorPosition == 0 && WordWidth(start, newline) > textArea.Width)
            {
                var lastLine = !HasRoomBelow();
                LayItems(start, newline);
                EndInEllipsis(TextTrimming.CharEllipses);
                return !lastLine && LayItems(newline, end);
            }

            return LayItems(start, end);
        }

        void Compose()
        {
            composedStarts = [];
            composedCuts = [];
            var justified = renderingParameters.HorizontalTextAlignment == HorizontalTextAlignment.Justify;
            var raggedStretch = 2 * fontSize;
            List<(ComposerItem Item, int Next, bool Cut, bool Automatic)> paragraph = [];
            var start = 0;
            while (start < items.Count)
            {
                var end = WordEnd(start);
                var newline = end > start && items[end - 1].Symbol == '\n' ? end - 1 : end;
                List<int> points = Hyphens == Hyphens.None ? [] : HyphenationPoints(start, newline);
                var automatic = Hyphens == Hyphens.Auto && !HasSoftHyphen(start, newline);
                var piece = start;
                foreach (var point in points)
                {
                    paragraph.Add((ComposerItem.Box(Advance(piece, point)), point, false, false));
                    paragraph.Add((ComposerItem.Penalty(HyphenAdvance(items[point - 1]), 50, true), point, true, automatic));
                    piece = point;
                }

                if (newline > piece)
                {
                    paragraph.Add((ComposerItem.Box(Advance(piece, newline)), newline, false, false));
                }

                if (newline < end || end >= items.Count)
                {
                    ComposeParagraph(paragraph, justified);
                    paragraph.Clear();
                    start = end;
                    continue;
                }

                var next = end;
                while (next < items.Count && IsBlank(items[next].Symbol))
                {
                    next++;
                }

                if (next == end)
                {
                    var hyphen = items[end - 1].Symbol is '-' or '‐';
                    AddBreak(paragraph, next, 0, hyphen ? 50 : 0, hyphen, justified, raggedStretch);
                }
                else
                {
                    AddBreak(paragraph, next, SpaceAdvance(end), 0, false, justified, raggedStretch);
                    for (var k = end + 1; k < next; k++)
                    {
                        var space = SpaceAdvance(k);
                        paragraph.Add((ComposerItem.Glue(space, justified ? space / 2 : 0, justified ? space / 3 : 0), next,
                            false, false));
                    }
                }

                start = next;
            }

            if (paragraph.Count > 0)
            {
                ComposeParagraph(paragraph, justified);
            }
        }

        void ComposeParagraph(List<(ComposerItem Item, int Next, bool Cut, bool Automatic)> paragraph, bool justified)
        {
            while (paragraph.Count > 0 && paragraph[paragraph.Count - 1].Item.Kind != ComposerItemKind.Box)
            {
                paragraph.RemoveAt(paragraph.Count - 1);
            }

            if (paragraph.Count == 0)
            {
                return;
            }

            if (!justified || !renderingParameters.JustifyLastLine)
            {
                paragraph.Add((ComposerItem.Penalty(0, ParagraphComposer.Never, false), -1, false, false));
                paragraph.Add((ComposerItem.Glue(0, double.PositiveInfinity, 0), -1, false, false));
            }

            paragraph.Add((ComposerItem.Penalty(0, ParagraphComposer.Forced, false), -1, false, false));
            var exact = paragraph.Where(entry => !entry.Automatic).ToList();
            var chosen = exact;
            var breaks = ParagraphComposer.Break(exact.Select(entry => entry.Item).ToList(), textArea.Width, 100);
            if (breaks == null)
            {
                chosen = paragraph;
                var all = paragraph.Select(entry => entry.Item).ToList();
                breaks = ParagraphComposer.Break(all, textArea.Width, 200)
                         ?? ParagraphComposer.BreakAnyway(all, textArea.Width, 2 * fontSize);
            }

            for (var k = 0; k < breaks.Length - 1; k++)
            {
                var entry = chosen[breaks[k]];
                composedStarts.Add(entry.Next);
                if (entry.Cut)
                {
                    composedCuts.Add(entry.Next);
                }
            }
        }

        static void AddBreak(List<(ComposerItem Item, int Next, bool Cut, bool Automatic)> paragraph, int next,
            double space, double cost, bool flagged, bool justified, double raggedStretch)
        {
            if (justified)
            {
                paragraph.Add(space > 0
                    ? (ComposerItem.Glue(space, space / 2, space / 3), next, false, false)
                    : (ComposerItem.Penalty(0, cost, flagged), next, false, false));
                return;
            }

            paragraph.Add((ComposerItem.Glue(0, raggedStretch, 0), next, false, false));
            paragraph.Add((ComposerItem.Penalty(0, cost, flagged), next, false, false));
            paragraph.Add((ComposerItem.Glue(space, -raggedStretch, 0), next, false, false));
        }

        double SpaceAdvance(int index) => items[index].Symbol == '\t' ? tabStop : items[index].Advance;

        double Advance(int start, int end)
        {
            double advance = 0;
            for (var k = start; k < end; k++)
            {
                advance += items[k].Advance;
            }

            return advance;
        }

        bool HasSoftHyphen(int start, int end)
        {
            for (var k = start; k < end; k++)
            {
                if (items[k].Symbol == SoftHyphen)
                {
                    return true;
                }
            }

            return false;
        }

        double HyphenAdvance(ShapedItem last)
        {
            var hyphen = HyphenGlyph(last.Font);
            return last.Font.GetAdvanceWidth(hyphen.Index) * (last.FontSize / last.Font.UnitsPerEm);
        }

        bool ProcessWord(int start, int end)
        {
            if (composedStarts != null)
            {
                return ProcessComposedWord(start, end);
            }

            var wrapsByWords = renderingParameters.TextWrapping == TextWrapping.WrapByWords;
            var wordWidth = wrapsByWords ? WordWidth(start, end) : 0;
            if (wrapsByWords && Hyphens != Hyphens.None && cursorPosition + wordWidth > textArea.Width)
            {
                var rest = HyphenateWord(start, end);
                if (rest != start)
                {
                    start = rest;
                    wordWidth = WordWidth(start, end);
                }
            }

            // Wrapped at the word boundary, before any glyph is laid: a word that does not fit starts a new line, and
            // a word wider than the line only overflows as the first on its line.
            if (wrapsByWords
                && wordIndex > 0
                && cursorPosition > 0
                && cursorPosition + wordWidth > textArea.Width
                && height + lineHeight < textArea.Height)
            {
                lineIndex++;
                height += lineHeight;
                cursorPosition = 0;
            }

            wordStartPosition = cursorPosition;
            if (wrapsByWords
                && renderingParameters.TextTrimming != TextTrimming.None
                && cursorPosition + wordWidth > textArea.Width)
            {
                var newline = end > start && items[end - 1].Symbol == '\n' ? end - 1 : end;
                var lastLine = !HasRoomBelow();
                var trimming = cursorPosition > 0 ? renderingParameters.TextTrimming : TextTrimming.CharEllipses;
                LayItems(start, newline);
                EndInEllipsis(trimming);
                return !lastLine && LayItems(newline, end);
            }

            return LayItems(start, end);
        }

        double LineShrink()
        {
            if (renderingParameters.HorizontalTextAlignment != HorizontalTextAlignment.Justify)
            {
                return 0;
            }

            double shrink = 0;
            for (var k = LineFirstGlyph(); k < glyphsData.Count; k++)
            {
                if (glyphsData[k].Symbol == ' ')
                {
                    shrink += glyphsData[k].Advance / 3;
                }
                else if (glyphsData[k].Symbol == '\t')
                {
                    shrink = 0;
                }
            }

            return shrink;
        }

        int LineFirstGlyph()
        {
            var first = glyphsData.Count;
            while (first > 0 && glyphsData[first - 1].LineIndex == lineIndex)
            {
                first--;
            }

            return first;
        }

        bool HasBlank(int from)
        {
            for (var k = from; k < glyphsData.Count; k++)
            {
                if (IsBlank(glyphsData[k].Symbol))
                {
                    return true;
                }
            }

            return false;
        }

        double WordWidth(int start, int end)
        {
            double pen = 0;
            double right = 0;
            for (var k = start; k < end; k++)
            {
                var item = items[k];
                if (item.Symbol == '\n')
                {
                    continue;
                }

                if (item.Glyph.BoundingRectangle.Width > 0)
                {
                    var inkRight = (item.Font.GetLeftSideBearing(item.Glyph.Index) + item.Glyph.BoundingRectangle.Width)
                                   * (item.FontSize / item.Font.UnitsPerEm);
                    right = Math.Max(right, pen + item.OffsetX + inkRight);
                }

                pen += item.Advance;
            }

            return right;
        }

        int HyphenateWord(int start, int end)
        {
            var newline = start;
            while (newline < end && items[newline].Symbol != '\n')
            {
                newline++;
            }

            List<int> points = null;
            while (cursorPosition + WordWidth(start, newline) > textArea.Width && height + lineHeight < textArea.Height)
            {
                points ??= HyphenationPoints(start, newline);
                var cut = -1;
                var pen = cursorPosition;
                var k = start;
                foreach (var point in points)
                {
                    for (; k < point; k++)
                    {
                        pen += items[k].Advance;
                    }

                    if (point > start && pen + HyphenInk(items[point - 1]) <= textArea.Width)
                    {
                        cut = point;
                    }
                }

                if (cut < 0)
                {
                    if (cursorPosition > 0 && wordIndex > 0)
                    {
                        NewLine();
                        continue;
                    }

                    break;
                }

                LayItems(start, cut);
                AddHyphen(items[cut - 1]);
                lineIndex++;
                height += lineHeight;
                cursorPosition = 0;
                start = cut;
            }

            return start;
        }

        List<int> HyphenationPoints(int start, int end)
        {
            var points = new List<int>();
            if (start >= end)
            {
                return points;
            }

            var first = items[start].Cluster;
            var last = end < items.Count ? items[end].Cluster : text.Length;
            var breaks = new HashSet<int>();
            for (var c = first; c < last - 1; c++)
            {
                if (text[c] == SoftHyphen)
                {
                    breaks.Add(c + 1);
                }
            }

            if (breaks.Count == 0 && Hyphens == Hyphens.Auto)
            {
                var c = first;
                var languageItem = start;
                while (c < last)
                {
                    if (!IsWordLetter(text, c, first, last))
                    {
                        c++;
                        continue;
                    }

                    var runStart = c;
                    while (c < last && IsWordLetter(text, c, first, last))
                    {
                        c++;
                    }

                    while (languageItem < end - 1 && items[languageItem].Cluster < runStart)
                    {
                        languageItem++;
                    }

                    var hyphenator = Hyphenator.ForLanguage(items[languageItem].Attributes?.Language ?? Language);
                    foreach (var offset in hyphenator?.Hyphenate(text.Substring(runStart, c - runStart)) ?? [])
                    {
                        breaks.Add(runStart + offset);
                    }
                }
            }

            for (var k = start + 1; k < end; k++)
            {
                if (items[k].Cluster != items[k - 1].Cluster && breaks.Contains(items[k].Cluster))
                {
                    points.Add(k);
                }
            }

            return points;
        }

        double HyphenInk(ShapedItem last)
        {
            var hyphen = HyphenGlyph(last.Font);
            return (last.Font.GetLeftSideBearing(hyphen.Index) + hyphen.BoundingRectangle.Width)
                   * (last.FontSize / last.Font.UnitsPerEm);
        }

        void AddHyphen(ShapedItem last)
        {
            var hyphen = HyphenGlyph(last.Font);
            var hyphenScale = last.FontSize / last.Font.UnitsPerEm;
            var advance = last.Font.GetAdvanceWidth(hyphen.Index) * hyphenScale;
            var rect = CalculateGlyphPosition(last.Font, hyphen, cursorPosition, height + baseLine, hyphenScale);
            glyphsData.Add(new GlyphWordData(hyphen, '-', rect, -1, lineIndex)
            {
                PenX = cursorPosition,
                Advance = advance,
                Attributes = last.Attributes,
                Font = last.Font,
                FontSize = last.FontSize,
            });
            cursorPosition += advance;
        }

        bool LayItems(int start, int end)
        {
            for (var i = start; i < end; i++)
            {
                var item = items[i];
                switch (item.Symbol)
                {
                    case '\n':
                        if (renderingParameters.TextWrapping == TextWrapping.WrapByWords
                            && renderingParameters.TextTrimming != TextTrimming.None
                            && !HasRoomBelow()
                            && i < items.Count - 1)
                        {
                            EndInEllipsis(renderingParameters.TextTrimming);
                            return false;
                        }

                        if (EmitNewlineCarets)
                        {
                            var caretRect = new RectangleF((float)cursorPosition, (float)(height + baseLine), 0f, 0f);
                            glyphsData.Add(new GlyphWordData(item.Glyph, '\n', caretRect, item.Cluster, lineIndex)
                            {
                                PenX = cursorPosition,
                                Attributes = item.Attributes,
                                Font = item.Font,
                                FontSize = item.FontSize,
                            });
                        }
                        height += lineHeight;
                        cursorPosition = 0;
                        lineIndex++;
                        break;
                    default:
                    {
                        var glyphBase = height + baseLine;
                        var glyphRect = CalculateGlyphPosition(item.Font, item.Glyph,
                            cursorPosition + item.OffsetX,
                            glyphBase - item.OffsetY,
                            item.FontSize / item.Font.UnitsPerEm);

                        glyphsData.Add(new GlyphWordData(item.Glyph, item.Symbol, glyphRect, item.Cluster, lineIndex)
                        {
                            PenX = cursorPosition,
                            Advance = item.Advance,
                            OffsetX = item.OffsetX,
                            OffsetY = item.OffsetY,
                            Attributes = item.Attributes,
                            Font = item.Font,
                            FontSize = item.FontSize,
                        });
                        cursorPosition += item.Advance;

                        switch (renderingParameters.TextWrapping)
                        {
                            case TextWrapping.NoWrap:
                                if (cursorPosition > textArea.Width)
                                {
                                    if (i < items.Count - 1)
                                    {
                                        var glyphsDataCopy = glyphsData.ToArray();
                                        PrepareDataAndTrim(glyphsDataCopy.Length, 0, i, glyphBase, renderingParameters.TextTrimming);
                                        return false;
                                    }
                                }
                                break;
                            case TextWrapping.WrapBySymbols:
                                {
                                    if (cursorPosition > textArea.Width)
                                    {
                                        var glyphsDataCopy = glyphsData.ToArray();
                                        if (height + lineHeight < textArea.Height)
                                        {
                                            lineIndex++;
                                            height += lineHeight;
                                            glyphBase = height + baseLine;
                                            RearrangeData(glyphsDataCopy, glyphBase);
                                        }
                                        else if (i < items.Count - 1)
                                        {
                                            PrepareDataAndTrim(glyphsDataCopy.Length, 0, i, glyphBase, renderingParameters.TextTrimming);
                                            return false;
                                        }
                                    }
                                }
                                break;
                        }
                        break;
                    }
                }
            }
            return true;
        }

        void ArrangeText()
        {
            var hung = new HashSet<int>();
            var minX = glyphsData.Min(x => x.Rect.Left);
            var maxX = glyphsData.Max(x => x.Rect.Right);
            var minY = glyphsData.Min(x => x.Rect.Top);
            switch (renderingParameters.HorizontalTextAlignment)
            {
                case HorizontalTextAlignment.Left or HorizontalTextAlignment.Right when _paragraphs != null:
                    AlignToEachLinesStart();
                    break;
                case HorizontalTextAlignment.Center:
                {
                    var maxLines = glyphsData.Max(x => x.LineIndex);
                    for (int i = 0; i <= maxLines; ++i)
                    {
                        var glyphsForLine = glyphsData.Where(x => x.LineIndex == i).ToArray();
                        if (glyphsForLine.Length == 0) break;

                        // By the ink, so leading/trailing spaces don't pull the line off-center.
                        var ink = glyphsForLine.Where(x => !IsBlank(x.Symbol)).ToArray();
                        if (ink.Length == 0) continue;
                        minX = ink.Min(x => x.Rect.Left);
                        maxX = ink.Max(x => x.Rect.Right);
                        var lineWidth = maxX - minX;
                        var diff = (finalRect.Width - lineWidth) / 2 - minX;
                        foreach (var glyphWordData in glyphsForLine)
                        {
                            var rect = glyphWordData.Rect;
                            rect.X += (float)diff;
                            glyphWordData.Rect = rect;
                            glyphWordData.PenX += diff;
                        }
                    }
                }
                break;
                case HorizontalTextAlignment.Right:
                {
                    var maxLines = glyphsData.Max(x => x.LineIndex);
                    for (int i = 0; i <= maxLines; ++i)
                    {
                        var glyphsForLine = glyphsData.Where(x => x.LineIndex == i).ToArray();
                        if (glyphsForLine.Length == 0) break;

                        maxX = glyphsForLine.Where(x=>!IsBlank(x.Symbol)).Max(x => x.Rect.Right);
                        var diff = (finalRect.Width - maxX);
                        foreach (var glyphWordData in glyphsForLine)
                        {
                            var rect = glyphWordData.Rect;
                            rect.X += (float)diff;
                            glyphWordData.Rect = rect;
                            glyphWordData.PenX += diff;
                        }
                    }
                }
                break;
                case HorizontalTextAlignment.Justify:
                {
                    var maxLines = glyphsData.Max(x => x.LineIndex);
                    var firstClusters = new int[maxLines + 2];
                    Array.Fill(firstClusters, text.Length);
                    foreach (var glyph in glyphsData)
                    {
                        if (glyph.PositionInString >= 0)
                        {
                            firstClusters[glyph.LineIndex] = Math.Min(firstClusters[glyph.LineIndex], glyph.PositionInString);
                        }
                    }

                    for (int i = 0; i <= maxLines; ++i)
                    {
                        // Only the gaps between words change; glyphs are shifted, never re-laid, so kerning and bearings stay.
                        var lineGlyphs = glyphsData.Where(x => x.LineIndex == i).OrderBy(x => x.Rect.X).ToArray();
                        if (lineGlyphs.Length == 0) continue;
                        var stretches = renderingParameters.JustifyLastLine
                                        || (i < maxLines && !EndsParagraph(lineGlyphs, firstClusters[i + 1]));

                        int firstInk = -1, lastInk = -1;
                        for (int k = 0; k < lineGlyphs.Length; k++)
                        {
                            if (IsBlank(lineGlyphs[k].Symbol)) continue;
                            if (firstInk < 0) firstInk = k;
                            lastInk = k;
                        }
                        if (firstInk < 0) continue;

                        var lastTab = Array.FindLastIndex(lineGlyphs, lastInk, lastInk + 1, glyph => glyph.Symbol == '\t');
                        var from = Math.Max(firstInk, lastTab);
                        var spaceCount = 0;
                        double shrinkable = 0;
                        for (var k = from + 1; k < lastInk; k++)
                        {
                            if (lineGlyphs[k].Symbol == ' ')
                            {
                                spaceCount++;
                                shrinkable += lineGlyphs[k].Advance / 3;
                            }
                        }

                        if (spaceCount == 0)
                        {
                            continue;
                        }

                        var hangs = OpticalMarginAlignment && !IsRightToLeftLine(lineGlyphs);
                        var leftHang = hangs && firstInk == 0 && lastTab < 0 ? Hang(lineGlyphs[firstInk], true) : 0;
                        var rightHang = hangs ? Hang(lineGlyphs[lastInk], false) : 0;
                        var extra = finalRect.Width + leftHang + rightHang - lineGlyphs[lastInk].Rect.Right;

                        if (extra == 0 || shrinkable == 0 || (extra > 0 && !stretches))
                        {
                            continue;
                        }

                        var shrinkRatio = extra < 0 ? Math.Max(extra, -shrinkable) / shrinkable : 0;
                        var perSpace = extra / spaceCount;
                        double shift = -leftHang;
                        hung.Add(i);
                        for (int k = 0; k < lineGlyphs.Length; k++)
                        {
                            var rect = lineGlyphs[k].Rect;
                            rect.X += (float)shift;
                            lineGlyphs[k].Rect = rect;
                            lineGlyphs[k].PenX += shift;
                            if (k > from && k < lastInk && lineGlyphs[k].Symbol == ' ')
                            {
                                shift += extra > 0 ? perSpace : shrinkRatio * lineGlyphs[k].Advance / 3;
                            }
                        }
                    }
                }
                break;
            }

            if (OpticalMarginAlignment && renderingParameters.HorizontalTextAlignment != HorizontalTextAlignment.Center)
            {
                HangMargins(hung);
            }

            switch (renderingParameters.VerticalTextAlignment)
            {
                case VerticalTextAlignment.Center:
                {
                    // Centered by ascent above the baseline, not the ink: ink differs per string and made same-size
                    // text wobble. No descent reserve, so descenders hang below.
                    var lastInk = glyphsData.Max(x => x.LineIndex);
                    var ascent = lineAscents[0];
                    var firstBaseline = lineTops[0] - textArea.Y + lineBaselines[0];
                    var blockHeight = lineTops[lastInk] - textArea.Y + lineBaselines[lastInk] - firstBaseline + ascent;
                    var blockTop = firstBaseline - ascent;
                    var diff = (finalRect.Height - blockHeight) / 2 - blockTop;
                    verticalShift = diff;
                    foreach (var glyphWordData in glyphsData)
                    {
                        var rect = glyphWordData.Rect;
                        rect.Y += (float)diff;
                        glyphWordData.Rect = rect;
                    }
                }
                break;
                case VerticalTextAlignment.Bottom:
                {
                    // The last baseline on the bottom edge, not the lowest ink, for the same reason.
                    var diff = finalRect.Height - lastBaseline;
                    verticalShift = diff;
                    foreach (var glyphWordData in glyphsData)
                    {
                        var rect = glyphWordData.Rect;
                        rect.Y += (float)diff;
                        glyphWordData.Rect = rect;
                    }
                }
                break;
            }
        }

        void HangMargins(HashSet<int> hung)
        {
            var toRight = renderingParameters.HorizontalTextAlignment == HorizontalTextAlignment.Right;
            foreach (var line in glyphsData.GroupBy(glyph => glyph.LineIndex))
            {
                if (hung.Contains(line.Key))
                {
                    continue;
                }

                var glyphs = line.OrderBy(glyph => glyph.PenX).ToArray();
                if (IsRightToLeftLine(glyphs))
                {
                    continue;
                }

                int from;
                int to;
                double shift;
                if (toRight)
                {
                    var last = Array.FindLastIndex(glyphs, glyph => !IsBlank(glyph.Symbol) && glyph.Symbol != '\n');
                    if (last < 0)
                    {
                        continue;
                    }

                    from = Array.FindLastIndex(glyphs, last, last + 1, glyph => glyph.Symbol == '\t') + 1;
                    to = glyphs.Length;
                    shift = Hang(glyphs[last], false);
                }
                else
                {
                    if (IsBlank(glyphs[0].Symbol) || glyphs[0].Symbol == '\n')
                    {
                        continue;
                    }

                    var firstTab = Array.FindIndex(glyphs, glyph => glyph.Symbol == '\t');
                    from = 0;
                    to = firstTab < 0 ? glyphs.Length : firstTab;
                    shift = -Hang(glyphs[0], true);
                }

                for (var k = from; k < to; k++)
                {
                    glyphs[k].Rect.X += (float)shift;
                    glyphs[k].PenX += shift;
                }
            }
        }

        bool IsRightToLeftLine(GlyphWordData[] line)
        {
            foreach (var glyph in line)
            {
                if (glyph.PositionInString >= 0)
                {
                    return IsRightToLeftParagraph(glyph.PositionInString);
                }
            }

            return false;
        }

        bool EndsParagraph(GlyphWordData[] line, int nextLineStart)
        {
            var last = -1;
            foreach (var glyph in line)
            {
                if (glyph.Symbol == '\n')
                {
                    return true;
                }

                last = Math.Max(last, glyph.PositionInString);
            }

            for (var c = last + 1; last >= 0 && c < nextLineStart; c++)
            {
                if (text[c] == '\n')
                {
                    return true;
                }
            }

            return false;
        }

        void AlignToEachLinesStart()
        {
            var lineStart = 0;
            while (lineStart < glyphsData.Count)
            {
                var lineEnd = lineStart;
                while (lineEnd < glyphsData.Count && glyphsData[lineEnd].LineIndex == glyphsData[lineStart].LineIndex)
                {
                    lineEnd++;
                }

                var first = -1;
                for (var k = lineStart; k < lineEnd && first < 0; k++)
                {
                    first = glyphsData[k].PositionInString;
                }

                var rightToLeft = first >= 0 && (ParagraphAt(first).Paragraph.BaseLevel & 1) == 1;
                var toRight = rightToLeft == (renderingParameters.HorizontalTextAlignment == HorizontalTextAlignment.Left);
                var ink = Enumerable.Range(lineStart, lineEnd - lineStart).Select(k => glyphsData[k])
                    .Where(g => !IsBlank(g.Symbol)).ToArray();
                if (toRight && ink.Length > 0)
                {
                    var shift = finalRect.Width - ink.Max(g => g.Rect.Right);
                    for (var k = lineStart; k < lineEnd; k++)
                    {
                        glyphsData[k].Rect.X += (float)shift;
                        glyphsData[k].PenX += shift;
                    }
                }

                lineStart = lineEnd;
            }
        }

        void PlaceLines()
        {
            var mixed = false;
            for (var i = 0; i < lineCount; i++)
            {
                lineAscents[i] = Font.Ascender * scale;
                lineHeights[i] = lineHeight;
                lineBaselines[i] = baseLine;
            }

            foreach (var glyph in glyphsData)
            {
                mixed |= glyph.FontSize != fontSize || !ReferenceEquals(glyph.Font, Font);
            }

            if (mixed)
            {
                var below = new double[lineCount];
                Array.Fill(below, lineHeight - baseLine);
                foreach (var glyph in glyphsData)
                {
                    var line = glyph.LineIndex;
                    var glyphScale = glyph.FontSize / glyph.Font.UnitsPerEm;
                    var glyphBaseline = BaselineInLine(glyph.Font, glyphScale);
                    var glyphBelow = LineAdvance(glyph.Font, glyph.FontSize) - glyphBaseline;
                    var glyphAscent = glyph.Font.Ascender * glyphScale;
                    lineBaselines[line] = Math.Max(lineBaselines[line], glyphBaseline);
                    below[line] = Math.Max(below[line], glyphBelow);
                    lineAscents[line] = Math.Max(lineAscents[line], glyphAscent);
                }

                for (var i = 0; i < lineCount; i++)
                {
                    lineHeights[i] = lineBaselines[i] + below[i];
                }
            }

            lineTops[0] = textArea.Y;
            for (var i = 1; i < lineCount; i++)
            {
                lineTops[i] = lineTops[i - 1] + lineHeights[i - 1];
            }

            if (!mixed)
            {
                return;
            }

            foreach (var glyph in glyphsData)
            {
                var lineBase = lineTops[glyph.LineIndex] + lineBaselines[glyph.LineIndex];
                if (IsBlank(glyph.Symbol) || glyph.Symbol == '\n')
                {
                    glyph.Rect.Y = (float)lineBase;
                }
                else
                {
                    glyph.Rect = CalculateGlyphPosition(glyph.Font, glyph.Glyph, glyph.PenX + glyph.OffsetX,
                        lineBase - glyph.OffsetY, glyph.FontSize / glyph.Font.UnitsPerEm);
                }
            }
        }

        void RearrangeData(GlyphWordData[] glyphsDataCopy, double glyphBase)
        {
            var rearrangeList = new List<GlyphWordData>();
            for (int k = glyphsDataCopy.Length - 1; k >= 0; k--)
            {
                var data = glyphsData[k];
                cursorPosition -= data.Advance;
                var wordsLeft = glyphsDataCopy.Take(k).Count(x => IsBlank(x.Symbol)) + 1;
                rearrangeList.Add(data);
                if (wordIndex > 0 &&
                    cursorPosition <= textArea.Width &&
                    renderingParameters.TextWrapping == TextWrapping.WrapByWords &&
                    IsBlank(data.Symbol))
                {
                    break;
                }
                else if (cursorPosition <= textArea.Width &&
                         renderingParameters.TextWrapping == TextWrapping.WrapBySymbols)
                {
                    break;
                }
                else if (wordsLeft == 1 &&
                         cursorPosition > textArea.Width &&
                         renderingParameters.TextWrapping == TextWrapping.WrapByWords)
                {
                    break;
                }
            }

            rearrangeList.Reverse();
            cursorPosition = 0;

            for (var index = 0; index < rearrangeList.Count; index++)
            {
                var glyphData = rearrangeList[index];
                if (index == 0 && IsBlank(glyphData.Symbol)) continue;

                var glyphRect = CalculateGlyphPosition(glyphData.Font, glyphData.Glyph,
                    cursorPosition + glyphData.OffsetX,
                    glyphBase - glyphData.OffsetY,
                    glyphData.FontSize / glyphData.Font.UnitsPerEm);

                glyphData.Rect = glyphRect;
                glyphData.PenX = cursorPosition;
                glyphData.LineIndex = lineIndex;
                cursorPosition += glyphData.Advance;
            }
        }

        void PrepareDataAndTrim(int count, int floor, int position, double glyphBase, TextTrimming trimming)
        {
            for (int k = count - 1; k >= floor; k--)
            {
                var data = glyphsData[k];
                cursorPosition -= data.Advance;
                glyphsData.RemoveAt(k);
                if (trimming == TextTrimming.None &&
                    cursorPosition <= textArea.Width)
                {
                    break;
                }
                else if (trimming == TextTrimming.CharEllipses &&
                    cursorPosition + dotGlyphsWidth <= textArea.Width)
                {
                    break;
                }
                else if (trimming ==
                         TextTrimming.WordEllipses &&
                         cursorPosition + dotGlyphsWidth <= textArea.Width)
                {
                    if (wordIndex > 0 && !IsBlank(data.Symbol))
                    {
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (trimming != TextTrimming.None)
            {
                TrimText(position, glyphBase);
            }

            width = cursorPosition;
        }

        void TrimText(int position, double glyphBase)
        {
            for (int j = 0; j < 3; j++)
            {
                var glyphRect = CalculateGlyphPosition(Font, dotGlyph,
                    cursorPosition,
                    glyphBase,
                    scale);
                glyphsData.Add(new GlyphWordData(dotGlyph, '.', glyphRect, -1, lineIndex)
                {
                    PenX = cursorPosition,
                    Advance = Font.GetAdvanceWidth(dotGlyph.Index) * scale,
                    Font = Font,
                    FontSize = fontSize,
                });

                cursorPosition += Font.GetAdvanceWidth(dotGlyph.Index) * scale;
            }
        }
    }

    /// <summary>The glyphs the laid-out text draws, each with its font, the trimming ellipsis included: what the atlas
    /// has to hold for it.</summary>
    public IEnumerable<(IFont Font, Glyph Glyph)> GetGlyphs()
    {
        var data = _wordData;
        if (data == null)
        {
            yield break;
        }

        foreach (var word in data)
        {
            if (IsBlank(word.Symbol) || word.Symbol == '\n')
            {
                continue;
            }

            var font = DrawnFont(word.Font ?? Font);
            var paint = font.GetColorPaint(word.Glyph.Index);
            if (paint.Count > 0)
            {
                foreach (var operation in paint)
                {
                    if (operation.Kind == ColorPaintOperationKind.PushClip)
                    {
                        yield return (font, font.GetGlyphByIndex(operation.GlyphIndex));
                    }
                }

                continue;
            }

            var layers = font.GetColorLayers(word.Glyph.Index);
            if (layers.Count == 0)
            {
                foreach (var field in FieldsOf(word.Font ?? Font, word.Glyph))
                {
                    yield return field;
                }

                continue;
            }

            foreach (var layer in layers)
            {
                yield return (font, font.GetGlyphByIndex(layer.GlyphIndex));
            }
        }

        foreach (var field in FieldsOf(Font, dotGlyph))
        {
            yield return field;
        }
    }

    private static IEnumerable<(IFont Font, Glyph Glyph)> FieldsOf(IFont font, Glyph glyph)
    {
        if (font.Blend is not { } blend)
        {
            yield return (font, glyph);
            yield break;
        }

        yield return (blend.From, blend.From.GetGlyphByIndex(glyph.Index));
        yield return (blend.To, blend.To.GetGlyphByIndex(glyph.Index));
    }

    private static IFont DrawnFont(IFont font) =>
        font.Blend is not { } blend ? font : blend.Amount < 0.5f ? blend.From : blend.To;

    /// <summary>Glyphs have landed in the atlas since this block built its quads; the render side rebuilds on this.</summary>
    public bool NeedsGlyphRefresh => FontAtlas != null && (FontAtlas.IsDisposed || FontAtlas.Version != _atlasVersion);

    /// <summary>Re-run the quad build against the atlas as it stands now (see <see cref="NeedsGlyphRefresh"/>).</summary>
    public void RefreshGlyphs(IGraphicsDevice graphicsDevice)
    {
        _textUpdated = true;
        Update(graphicsDevice);
    }

    /// <summary>This block's atlas, created on first use and again when its device is gone.</summary>
    public FontAtlas EnsureAtlas(IGraphicsDevice graphicsDevice)
    {
        if (FontAtlas == null || FontAtlas.IsDisposed)
        {
            FontAtlas = FontAtlasStore.GetOrCreateFrom(graphicsDevice,
                FontParameters.Default(sortingVariant: GlyphSortingVariant.ByIndex));
        }

        return FontAtlas;
    }

    public void Update(IGraphicsDevice graphicsDevice)
    {
        // Built before its first measure (a popup built ahead of its layout pass): retried next frame.
        if (!_textUpdated || _wordData == null) return;

        // Asked for, not waited for: glyphs land over the next frames and bump the atlas version (NeedsGlyphRefresh).
        var atlas = EnsureAtlas(graphicsDevice);
        atlas.RequestAsync(GetGlyphs());
        _atlasVersion = atlas.Version;
        ElementsCount = 0;
        // No vertex buffer here: only the direct draw path needs one (EnsureVertexBuffer), and one per block ran the BAR
        // heap out of memory.

        for (int i = 0; i < _wordData.Count; ++i)
        {
            var word = _wordData[i];
            if (IsBlank(word.Symbol) || word.Symbol == '\n') continue;

            var font = DrawnFont(word.Font);
            var palette = word.Attributes?.ColorPalette ?? 0;
            if (palette < 0 || palette >= font.ColorPalettes.Count)
            {
                palette = 0;
            }
            var paint = font.GetColorPaint(word.Glyph.Index, palette);
            if (paint.Count > 0)
            {
                AddProgramItem(word, FontAtlas.GetPaintProgram(font, word.Glyph.Index, palette, paint));
                continue;
            }

            var layers = font.GetColorLayers(word.Glyph.Index, palette);
            if (layers.Count == 0)
            {
                if (font.ColorBitmapSizes.Count > 0 &&
                    FontAtlas.TryGetImageProgram(font, word.Glyph.Index, out var image))
                {
                    AddProgramItem(word, image);
                    continue;
                }

                if (word.Font.Blend != null)
                {
                    AddBlendedGlyphItem(word, GlyphColor(word));
                    continue;
                }

                AddGlyphItem(word, word.Font, word.Glyph, word.Rect, GlyphColor(word));
                continue;
            }

            var scale = word.FontSize / font.UnitsPerEm;
            foreach (var layer in layers)
            {
                var glyph = font.GetGlyphByIndex(layer.GlyphIndex);
                var rect = CalculateGlyphPosition(font, glyph, word.PenX + word.OffsetX, Baseline(word), scale);
                var color = layer.Color is { } own ? own.ToVector4() : GlyphColor(word);
                AddGlyphItem(word, font, glyph, rect, color);
            }
        }

        _vertexBufferDirty = true;

        _textUpdated = true;
    }

    private void AddProgramItem(GlyphWordData word, int program)
    {
        if (program < 0)
        {
            return;
        }

        var scale = (float)(word.FontSize / word.Font.UnitsPerEm);
        EnsureItemCapacity((int)ElementsCount + 1);
        fontItems[ElementsCount] = new FontItem
        {
            ArrangeRect = new Vector4F((float)(word.PenX + word.OffsetX), (float)Math.Round(Baseline(word)), scale, scale),
            Depth = 1.0f,
            Color = GlyphColor(word),
            Paint = new Vector4F(program + 1, 0, 0, 0)
        };
        ElementsCount++;
    }

    internal static RectangleF CellSource(GlyphTextureData cell)
    {
        var full = cell.UVRectFull;
        var halfTexelU = (full.Right - full.Left) / (float)cell.FullGlyphSize.Width / 2;
        var halfTexelV = (full.Bottom - full.Top) / (float)cell.FullGlyphSize.Height / 2;
        return new RectangleF
        {
            Left = full.Left + halfTexelU,
            Top = full.Top + halfTexelV,
            Right = full.Right - halfTexelU,
            Bottom = full.Bottom - halfTexelV,
        };
    }

    private void AddBlendedGlyphItem(GlyphWordData word, Vector4F color)
    {
        var blend = word.Font.Blend;
        var scale = word.FontSize / word.Font.UnitsPerEm;
        var first = KeyCell(word, blend.From, scale);
        var second = KeyCell(word, blend.To, scale);
        var amount = blend.Amount;
        if (first == null && second == null)
        {
            return;
        }

        if (first == null || second == null)
        {
            first ??= second;
            second ??= first;
            amount = 0;
        }

        var left = Math.Max(first.Value.Quad.X, second.Value.Quad.X);
        var top = Math.Max(first.Value.Quad.Y, second.Value.Quad.Y);
        var right = Math.Min(first.Value.Quad.X + first.Value.Quad.Z, second.Value.Quad.X + second.Value.Quad.Z);
        var bottom = Math.Min(first.Value.Quad.Y + first.Value.Quad.W, second.Value.Quad.Y + second.Value.Quad.W);
        if (right <= left || bottom <= top)
        {
            return;
        }

        var quad = new Vector4F(left, top, right - left, bottom - top);
        EnsureItemCapacity((int)ElementsCount + 1);
        fontItems[ElementsCount] = new FontItem
        {
            ArrangeRect = quad,
            Source = SourceOver(first.Value, quad),
            Layer = first.Value.Layer,
            Depth = 1.0f,
            Color = color,
            Synthesis = GlyphSynthesis(word, quad),
            SecondSource = SourceOver(second.Value, quad),
            Second = new Vector2F(second.Value.Layer, amount)
        };
        ElementsCount++;
    }

    private (Vector4F Quad, RectangleF Source, float Layer)? KeyCell(GlyphWordData word, IFont key, double scale)
    {
        var glyph = key.GetGlyphByIndex(word.Glyph.Index);
        var gd = FontAtlas.GetGlyphData(key, glyph);
        if (gd == null || gd.BoundingRect.Width <= 0 || gd.BoundingRect.Height <= 0)
        {
            return null;
        }

        var rect = CalculateGlyphPosition(key, glyph, word.PenX + word.OffsetX, Baseline(word), scale);
        return (CellQuad(word, gd, rect), CellSource(gd), gd.DepthLayer);
    }

    private static Vector4F SourceOver((Vector4F Quad, RectangleF Source, float Layer) cell, Vector4F quad)
    {
        var width = cell.Source.Right - cell.Source.Left;
        var height = cell.Source.Bottom - cell.Source.Top;
        return new Vector4F(
            cell.Source.Left + (quad.X - cell.Quad.X) / cell.Quad.Z * width,
            cell.Source.Top + (quad.Y - cell.Quad.Y) / cell.Quad.W * height,
            quad.Z / cell.Quad.Z * width,
            quad.W / cell.Quad.W * height);
    }

    private Vector4F CellQuad(GlyphWordData word, GlyphTextureData gd, RectangleF rect)
    {
        var margin = gd.Margin - 0.5;
        // Per-axis margin maps the body exactly onto the pixel-snapped rect; a zero-size side falls back to the
        // uniform margin, or the quad collapses.
        var uniform = margin * word.FontSize / FontAtlas.MSDFTextureSize;
        var mx = rect.Width > 0 ? margin * rect.Width / gd.BoundingRect.Width : uniform;
        var my = rect.Height > 0 ? margin * rect.Height / gd.BoundingRect.Height : uniform;
        return new Vector4F((float)(rect.X - mx), (float)(rect.Y - my),
            (float)(rect.Width + 2 * mx), (float)(rect.Height + 2 * my));
    }

    private void AddGlyphItem(GlyphWordData word, IFont font, Glyph glyph, RectangleF rect, Vector4F color)
    {
        // The cell (body plus margin) less half a texel on each side: outline/glow/shadow keep the margin, and the
        // filter never reaches the neighbor cell, whose field would draw a sliver of another glyph.
        var gd = FontAtlas.GetGlyphData(font, glyph);
        FontItem item;
        if (gd != null && gd.BoundingRect.Width > 0 && gd.BoundingRect.Height > 0)
        {
            var source = CellSource(gd);
            var quad = CellQuad(word, gd, rect);
            item = new FontItem
            {
                ArrangeRect = quad,
                Source = source,
                Layer = gd.DepthLayer,
                Depth = 1.0f,
                Color = color,
                Synthesis = GlyphSynthesis(word, quad)
            };
        }
        else if (gd != null)
        {
            item = new FontItem
            {
                ArrangeRect = rect,
                Source = FontAtlas.GetUVCoordinatesForGlyph(font, glyph),
                Layer = gd.DepthLayer,
                Depth = 1.0f,
                Color = color,
                Synthesis = GlyphSynthesis(word, rect)
            };
        }
        else
        {
            // Not rasterized yet: no quad, or it draws a piece of a neighbor.
            return;
        }

        EnsureItemCapacity((int)ElementsCount + 1);
        fontItems[ElementsCount] = item;
        ElementsCount++;
    }

    /// <summary>
    /// Creates and uploads the per-block vertex buffer on demand, larger when the block has outgrown it; only the direct
    /// draw path (rotated/sheared text) uses it.
    /// </summary>
    public void EnsureVertexBuffer(IGraphicsDevice graphicsDevice)
    {
        if (VertexBuffer == null || VertexBuffer.ElementCount < ElementsCount)
        {
            if (VertexBuffer != null)
            {
                graphicsDevice.AddToDeferDisposeQueue(VertexBuffer);
            }

            VertexBuffer = Adamantium.Graphics.Buffer.Vertex.New<FontItem>(graphicsDevice,
                (uint)Math.Max(fontItems.Length, 1), Adamantium.Graphics.BufferMemoryUsage.UploadFromCpuToGpu);
            _vertexBufferDirty = true;
        }

        if (_vertexBufferDirty)
        {
            VertexBuffer.SetData((ReadOnlySpan<FontItem>)fontItems.AsSpan(0, (int)ElementsCount));
            _vertexBufferDirty = false;
        }
    }

    /// <summary>Copies this block's glyphs into a shared batch, baked to world space with its color, so many blocks draw
    /// in one call. False for a rotated/sheared world (direct path instead) or when <paramref name="dest"/> is full.</summary>
    public bool TryBakeWorldGlyphs(FontItem[] dest, ref int count, Matrix4x4F world, Vector2F textAreaOffset, Vector4F color)
    {
        if (ElementsCount == 0) return true;

        const float eps = 1e-4f;
        if (Math.Abs(world.M12) > eps || Math.Abs(world.M21) > eps) return false;
        if (count + (int)ElementsCount > dest.Length) return false;

        var sx = world.M11;
        var sy = world.M22;
        var tx = world.M41;
        var ty = world.M42;

        for (int i = 0; i < ElementsCount; i++)
        {
            var item = fontItems[i];
            var d = item.ArrangeRect;
            item.ArrangeRect = new Vector4F(
                (d.X + textAreaOffset.X) * sx + tx,
                (d.Y + textAreaOffset.Y) * sy + ty,
                d.Z * sx,
                d.W * sy);
            if (!item.HasOwnColor)
            {
                item.Color = color;
            }
            dest[count++] = item;
        }
        return true;
    }

    /// <summary>A frozen copy of the shaped glyphs (call after <see cref="Update"/>), so the render thread never reads
    /// this layout while it is reshaped.</summary>
    public FrozenGlyphRun SnapshotGlyphs()
    {
        var n = (int)ElementsCount;
        var copy = new FontItem[n];
        Array.Copy(fontItems, copy, n);
        return new FrozenGlyphRun(copy, n, FontAtlas, FontSize);
    }

    /// <summary>Where the caret stands before each UTF-16 character of <see cref="Text"/>, and after the last one.
    /// Characters drawn by one glyph, such as a ligature, share its width.</summary>
    public CaretStop[] GetCaretStops()
    {
        var text = Text ?? string.Empty;
        var stops = new CaretStop[text.Length + 1];
        var data = _wordData;
        if (data == null || data.Count == 0)
        {
            return stops;
        }

        var filled = new bool[text.Length + 1];
        var endX = 0.0;
        var endLine = 0;
        var i = 0;
        while (i < data.Count)
        {
            var cluster = data[i].PositionInString;
            if (cluster < 0 || cluster >= text.Length)
            {
                i++;
                continue;
            }

            var x = data[i].PenX;
            var line = data[i].LineIndex;
            var width = 0.0;
            var j = i;
            while (j < data.Count && data[j].PositionInString == cluster)
            {
                x = Math.Min(x, data[j].PenX);
                width += data[j].Advance;
                j++;
            }

            var after = j;
            while (after < data.Count && data[after].PositionInString < 0)
            {
                after++;
            }

            var next = after < data.Count && data[after].PositionInString > cluster ? data[after].PositionInString : text.Length;
            var rightToLeft = IsRightToLeftAt(cluster);
            DistributeCluster(text, cluster, next, x, width, line, rightToLeft, stops, filled);
            endX = rightToLeft ? x : x + width;
            endLine = line;
            i = j;
        }

        if (_paragraphs != null)
        {
            var rightToLeft = IsRightToLeftParagraph(text.Length - 1);
            for (var k = 0; k < text.Length; k++)
            {
                if (filled[k] && stops[k].LineIndex == endLine)
                {
                    endX = rightToLeft ? Math.Min(endX, stops[k].Left) : Math.Max(endX, stops[k].Right);
                }
            }
        }

        if (text[text.Length - 1] == '\n')
        {
            var rightToLeft = _laidOutDirection == TextDirection.RightToLeft
                || (_laidOutDirection == TextDirection.Auto && IsRightToLeftParagraph(text.Length - 1));
            var right = 0.0;
            for (var k = 0; k < text.Length && rightToLeft; k++)
            {
                right = filled[k] ? Math.Max(right, stops[k].Right) : right;
            }

            stops[text.Length] = new CaretStop(right, endLine + 1);
            filled[text.Length] = true;
        }

        for (var k = 0; k <= text.Length; k++)
        {
            if (!filled[k])
            {
                stops[k] = new CaretStop(endX, endLine);
            }
        }

        return stops;
    }

    /// <summary>How many visual lines the text takes.</summary>
    public int LineCount => LineStarts().Length - 1;

    /// <summary>A visual line: the characters it shows and where it stands.</summary>
    public TextLineMetrics GetLine(int lineIndex)
    {
        var stops = Stops();
        var starts = LineStarts();
        var start = 0;
        var end = 0;
        if (lineIndex >= starts.Length - 1)
        {
            start = end = stops.Length - 1;
        }
        else if (lineIndex >= 0)
        {
            start = starts[lineIndex];
            end = starts[lineIndex + 1];
        }

        var width = 0.0;
        for (var i = start; i < stops.Length && stops[i].LineIndex == lineIndex; i++)
        {
            width = Math.Max(width, stops[i].Right);
        }

        if (start < stops.Length && stops[start].LineIndex != lineIndex)
        {
            start = end;
        }

        var top = LineTop(lineIndex);
        var baseline = Math.Round(top - _verticalShift + LineMetric(_lineBaselines, lineIndex)) + _verticalShift;
        return new TextLineMetrics(start, end, top, baseline, LineMetric(_lineHeights, lineIndex), width);
    }

    /// <summary>Rectangles covering a UTF-16 range, one per piece of each visual line it spans, as high as the line:
    /// for selection, search hits, backgrounds.</summary>
    public IReadOnlyList<RectangleF> GetRangeRects(int start, int end)
    {
        return RangeSegments(start, end)
            .Select(s => new RectangleF((float)s.Left, (float)LineTop(s.Line),
                (float)(s.Right - s.Left), (float)LineMetric(_lineHeights, s.Line)))
            .ToList();
    }

    /// <summary>Backgrounds and lines of the attributed text last laid out: draw the backgrounds under the glyphs and the
    /// lines over them.</summary>
    public IReadOnlyList<TextAdornment> GetAdornments()
    {
        var adornments = new List<TextAdornment>();
        if (_attributed == null)
        {
            return adornments;
        }

        foreach (var run in _attributed.Runs)
        {
            var attributes = run.Attributes;
            var decorations = attributes.Decorations ?? TextDecorations.None;
            if (attributes.Background == null && decorations == TextDecorations.None)
            {
                continue;
            }

            var font = RunFont(attributes, FontSize);
            double em = font.UnitsPerEm;
            var scale = (attributes.FontSize ?? FontSize) / em;
            var thickness = (font.UnderlineThickness > 0 ? font.UnderlineThickness : em / 20) * scale;
            var underline = (font.UnderlinePosition != 0 ? font.UnderlinePosition : -em / 10) * scale;
            var strikeout = (font.StrikeoutPosition != 0 ? font.StrikeoutPosition : em / 4) * scale;
            var strikeoutSize = font.StrikeoutSize > 0 ? font.StrikeoutSize * scale : thickness;

            var lineColor = attributes.DecorationColor ?? attributes.Foreground;
            foreach (var segment in RangeSegments(run.Start, run.End))
            {
                var left = (float)segment.Left;
                var width = (float)(segment.Right - segment.Left);
                var baseline = GetLine(segment.Line).Baseline;
                if (attributes.Background is { } background)
                {
                    adornments.Add(new TextAdornment(TextAdornmentKind.Background,
                        new RectangleF(left, (float)LineTop(segment.Line), width,
                            (float)LineMetric(_lineHeights, segment.Line)), background));
                }

                if ((decorations & TextDecorations.Underline) != 0)
                {
                    adornments.Add(new TextAdornment(TextAdornmentKind.Underline,
                        new RectangleF(left, (float)(baseline - underline), width, (float)thickness), lineColor));
                }

                if ((decorations & TextDecorations.Strikethrough) != 0)
                {
                    adornments.Add(new TextAdornment(TextAdornmentKind.Strikethrough,
                        new RectangleF(left, (float)(baseline - strikeout), width, (float)strikeoutSize), lineColor));
                }

                if ((decorations & TextDecorations.Squiggle) != 0)
                {
                    adornments.Add(new TextAdornment(TextAdornmentKind.Squiggle,
                        new RectangleF(left, (float)(baseline - underline), width, (float)(thickness * 3)), lineColor));
                }
            }
        }

        return adornments;
    }

    /// <summary>The next place the caret may stand after <paramref name="index"/>: the end of the grapheme there, so an
    /// emoji or a letter with its marks is one step.</summary>
    public int NextCaretStop(int index) => TextBoundaries.Next(Graphemes(), Clamp(index));

    public int PreviousCaretStop(int index) => TextBoundaries.Previous(Graphemes(), Clamp(index));

    /// <summary>The word, run of spaces or punctuation mark at <paramref name="index"/>, as a double click selects it.</summary>
    public (int Start, int End) GetWordAt(int index)
    {
        var words = Words();
        var text = Text ?? string.Empty;
        index = Clamp(index);
        if (index == text.Length && index > 0)
        {
            index--;
        }

        var start = words[index] ? index : TextBoundaries.Previous(words, index);
        return (start, TextBoundaries.Next(words, start));
    }

    /// <summary>The start of the next word after <paramref name="index"/>, past any spaces: where Ctrl+Right goes.</summary>
    public int NextWordStop(int index)
    {
        var words = Words();
        var text = Text ?? string.Empty;
        var stop = TextBoundaries.Next(words, Clamp(index));
        while (stop < text.Length && char.IsWhiteSpace(text[stop]))
        {
            stop = TextBoundaries.Next(words, stop);
        }

        return stop;
    }

    /// <summary>The start of the word before <paramref name="index"/>: where Ctrl+Left goes.</summary>
    public int PreviousWordStop(int index)
    {
        var words = Words();
        var text = Text ?? string.Empty;
        var stop = TextBoundaries.Previous(words, Clamp(index));
        while (stop > 0 && char.IsWhiteSpace(text[stop]))
        {
            stop = TextBoundaries.Previous(words, stop);
        }

        return stop;
    }

    /// <summary>The grapheme nearest to a point in layout coordinates, and where a click there puts the caret.</summary>
    public TextHit HitTest(double x, double y)
    {
        var text = Text ?? string.Empty;
        if (text.Length == 0)
        {
            return new TextHit(0, false, false, 0);
        }

        var stops = Stops();
        var graphemes = Graphemes();
        var lineIndex = LineAt(y);
        var insideY = lineIndex >= 0 && lineIndex < LineCount;
        lineIndex = Math.Max(0, Math.Min(lineIndex, LineCount - 1));
        var line = GetLine(lineIndex);

        var newline = -1;
        var leftmost = -1;
        var rightmost = -1;
        var lineLeft = double.MaxValue;
        var lineRight = double.MinValue;
        for (var g = line.Start; g < line.End && g < text.Length; g = TextBoundaries.Next(graphemes, g))
        {
            if (text[g] is '\n' or '\r')
            {
                newline = newline < 0 ? g : newline;
                continue;
            }

            var next = TextBoundaries.Next(graphemes, g);
            var left = double.MaxValue;
            var right = double.MinValue;
            for (var c = g; c < next && c < stops.Length; c++)
            {
                left = Math.Min(left, stops[c].Left);
                right = Math.Max(right, stops[c].Right);
            }

            if (left < lineLeft)
            {
                lineLeft = left;
                leftmost = g;
            }

            if (right > lineRight)
            {
                lineRight = right;
                rightmost = g;
            }

            if (x < left || x >= right)
            {
                continue;
            }

            var leading = stops[g].IsRightToLeft ? x >= (left + right) / 2 : x < (left + right) / 2;
            return leading ? new TextHit(g, false, insideY, g) : new TextHit(g, true, insideY, Math.Min(next, line.End));
        }

        if (leftmost < 0)
        {
            var at = newline >= 0 ? newline : line.Start;
            return new TextHit(at, false, false, at);
        }

        var onLeft = x < lineLeft;
        var nearest = onLeft ? leftmost : rightmost;
        var logicalEnd = LogicalLineEnd(line);
        if (logicalEnd >= 0 && onLeft == IsRightToLeftParagraph(line.Start))
        {
            return new TextHit(nearest, false, false, logicalEnd);
        }

        var after = Math.Min(TextBoundaries.Next(graphemes, nearest), line.End);
        if (newline >= 0)
        {
            after = Math.Min(after, newline);
        }

        return stops[nearest].IsRightToLeft == onLeft
            ? new TextHit(nearest, true, false, after)
            : new TextHit(nearest, false, false, nearest);
    }

    /// <summary>Where the caret stands for a position, in layout coordinates, and on which visual line.</summary>
    public (double X, int LineIndex) GetCaretPoint(CaretPosition position)
    {
        var stops = Stops();
        var text = Text ?? string.Empty;
        var index = Clamp(position.Index);
        var held = position.AfterPrevious ? HeldCharacter(index) : -1;
        if (held >= 0)
        {
            return (stops[held].After, stops[held].LineIndex);
        }

        var line = stops[index].LineIndex;
        if (_paragraphs != null && index < text.Length && text[index] is '\n' or '\r')
        {
            var boxes = LineBoxes(line);
            if (boxes.Count > 0)
            {
                return (IsRightToLeftParagraph(index) ? boxes[0].Left : boxes[boxes.Count - 1].Right, line);
            }
        }

        return (stops[index].X, line);
    }

    /// <summary>The caret position one grapheme to the left or the right of <paramref name="from"/> on screen, whichever
    /// way the text there runs; past a line's edge, the nearer edge of the line the reading goes on to.</summary>
    public CaretPosition MoveVisually(CaretPosition from, bool toRight)
    {
        var (x, lineIndex) = GetCaretPoint(from);
        var boxes = LineBoxes(lineIndex);
        if (boxes.Count > 0)
        {
            var edge = EdgeOf(from, boxes, x);
            if (toRight)
            {
                while (edge + 1 < boxes.Count && boxes[edge].Right <= boxes[edge].Left)
                {
                    edge++;
                }

                if (edge < boxes.Count)
                {
                    return RightEdgeOf(boxes[edge]);
                }
            }
            else
            {
                while (edge > 1 && boxes[edge - 1].Right <= boxes[edge - 1].Left)
                {
                    edge--;
                }

                if (edge > 0)
                {
                    return LeftEdgeOf(boxes[edge - 1]);
                }
            }
        }

        var forward = toRight != IsRightToLeftParagraph(GetLine(lineIndex).Start);
        var target = lineIndex + (forward ? 1 : -1);
        if (target < 0 || target >= LineCount)
        {
            return from;
        }

        var targetBoxes = LineBoxes(target);
        if (targetBoxes.Count == 0)
        {
            return new CaretPosition(GetLine(target).Start);
        }

        return toRight ? LeftEdgeOf(targetBoxes[0]) : RightEdgeOf(targetBoxes[targetBoxes.Count - 1]);
    }

    /// <summary>The caret position on the edge a line starts from on screen: the left for a left-to-right paragraph, the
    /// right for a right-to-left one.</summary>
    public CaretPosition GetLineStart(int lineIndex)
    {
        var line = GetLine(lineIndex);
        var boxes = LineBoxes(lineIndex);
        if (boxes.Count == 0)
        {
            return new CaretPosition(line.Start);
        }

        return IsRightToLeftParagraph(line.Start) ? RightEdgeOf(boxes[boxes.Count - 1]) : LeftEdgeOf(boxes[0]);
    }

    /// <summary>The caret position where a line ends: before its newline or at the end of the text, on the edge its
    /// paragraph ends on; after the last character on screen when the line wraps.</summary>
    public CaretPosition GetLineEnd(int lineIndex)
    {
        var line = GetLine(lineIndex);
        var end = LogicalLineEnd(line);
        var boxes = LineBoxes(lineIndex);
        if (end >= 0 || boxes.Count == 0)
        {
            return new CaretPosition(end >= 0 ? end : line.Start);
        }

        return IsRightToLeftParagraph(line.Start) ? LeftEdgeOf(boxes[0]) : RightEdgeOf(boxes[boxes.Count - 1]);
    }

    /// <summary>The characters lying on screen between two caret positions: on one line, between the two; across lines,
    /// from the upper one to its line's end, every line between, and from the lower line's start. In order of the text,
    /// one range per unbroken run of characters.</summary>
    public IReadOnlyList<(int Start, int End)> GetVisualRanges(CaretPosition anchor, CaretPosition focus)
    {
        var text = Text ?? string.Empty;
        var selected = new bool[text.Length];
        var (anchorX, anchorLine) = GetCaretPoint(anchor);
        var (focusX, focusLine) = GetCaretPoint(focus);
        if (anchorLine == focusLine)
        {
            SelectBetween(anchorLine, Math.Min(anchorX, focusX), Math.Max(anchorX, focusX), selected);
        }
        else
        {
            var (upper, upperX, lower, lowerX) = anchorLine < focusLine
                ? (anchorLine, anchorX, focusLine, focusX)
                : (focusLine, focusX, anchorLine, anchorX);
            var upperLine = GetLine(upper);
            if (IsRightToLeftParagraph(upperLine.Start))
            {
                SelectBetween(upper, double.MinValue, upperX, selected);
            }
            else
            {
                SelectBetween(upper, upperX, double.MaxValue, selected);
            }

            var newline = LogicalLineEnd(upperLine);
            for (var c = Math.Max(newline, 0); newline >= 0 && c < upperLine.End && c < text.Length; c++)
            {
                selected[c] = true;
            }

            for (var lineIndex = upper + 1; lineIndex < lower; lineIndex++)
            {
                var line = GetLine(lineIndex);
                for (var c = line.Start; c < line.End && c < text.Length; c++)
                {
                    selected[c] = true;
                }
            }

            if (IsRightToLeftParagraph(GetLine(lower).Start))
            {
                SelectBetween(lower, lowerX, double.MaxValue, selected);
            }
            else
            {
                SelectBetween(lower, double.MinValue, lowerX, selected);
            }
        }

        var ranges = new List<(int Start, int End)>();
        for (var i = 0; i < selected.Length; i++)
        {
            if (!selected[i])
            {
                continue;
            }

            var start = i;
            while (i < selected.Length && selected[i])
            {
                i++;
            }

            ranges.Add((start, i));
        }

        return ranges;
    }

    private int Clamp(int index) => Math.Max(0, Math.Min(index, (Text ?? string.Empty).Length));

    private static double LineAdvance(IFont font, double fontSize)
    {
        return (font.LineAscent + font.LineDescent + font.LineGap) * (fontSize / font.UnitsPerEm);
    }

    private static double BaselineInLine(IFont font, double scale)
    {
        return (font.LineGap / 2.0 + font.LineAscent) * scale;
    }

    private double LineTop(int line)
    {
        if (_lineTops == null || _lineTops.Length == 0)
        {
            return 0;
        }

        var last = _lineTops.Length - 1;
        if (line < 0)
        {
            return _lineTops[0] + line * _lineHeights[0];
        }

        return line <= last ? _lineTops[line] : _lineTops[last] + (line - last) * _lineHeights[last];
    }

    private static double LineMetric(double[] metrics, int line)
    {
        if (metrics == null || metrics.Length == 0)
        {
            return 0;
        }

        return metrics[Math.Max(0, Math.Min(line, metrics.Length - 1))];
    }

    private int LineAt(double y)
    {
        if (_lineTops == null || _lineTops.Length == 0 || y < _lineTops[0])
        {
            return -1;
        }

        for (var i = 0; i < _lineTops.Length; i++)
        {
            if (y < _lineTops[i] + _lineHeights[i])
            {
                return i;
            }
        }

        return _lineTops.Length;
    }

    private bool[] Graphemes() => _graphemes ??= TextBoundaries.Graphemes(Text ?? string.Empty);

    private bool[] Words() => _words ??= TextBoundaries.Words(Text ?? string.Empty);

    private CaretStop[] Stops() => _caretStops ??= GetCaretStops();

    private int[] LineStarts()
    {
        if (_lineStarts != null)
        {
            return _lineStarts;
        }

        var stops = Stops();
        var count = stops.Max(s => s.LineIndex) + 1;
        var starts = new int[count + 1];
        var line = 0;
        for (var i = 0; i < stops.Length; i++)
        {
            while (line <= stops[i].LineIndex)
            {
                starts[line++] = i;
            }
        }

        starts[count] = stops.Length - 1;
        return _lineStarts = starts;
    }

    private int HeldCharacter(int index)
    {
        var text = Text ?? string.Empty;
        if (index <= 0 || index > text.Length || text[index - 1] is '\n' or '\r')
        {
            return -1;
        }

        var character = index - 1;
        return character > 0 && char.IsLowSurrogate(text[character]) && char.IsHighSurrogate(text[character - 1])
            ? character - 1
            : character;
    }

    private List<(int Start, int End, double Left, double Right)> LineBoxes(int lineIndex)
    {
        var text = Text ?? string.Empty;
        var stops = Stops();
        var graphemes = Graphemes();
        var line = GetLine(lineIndex);
        var boxes = new List<(int Start, int End, double Left, double Right)>();
        for (var g = line.Start; g < line.End && g < text.Length; g = TextBoundaries.Next(graphemes, g))
        {
            if (text[g] is '\n' or '\r')
            {
                continue;
            }

            var next = TextBoundaries.Next(graphemes, g);
            var left = double.MaxValue;
            var right = double.MinValue;
            for (var c = g; c < next && c < stops.Length; c++)
            {
                left = Math.Min(left, stops[c].Left);
                right = Math.Max(right, stops[c].Right);
            }

            boxes.Add((g, next, left, right));
        }

        return boxes.OrderBy(b => b.Left).ThenBy(b => stops[b.Start].IsRightToLeft ? -b.Start : b.Start).ToList();
    }

    private int EdgeOf(CaretPosition position, List<(int Start, int End, double Left, double Right)> boxes, double x)
    {
        var stops = Stops();
        var held = position.AfterPrevious ? HeldCharacter(Clamp(position.Index)) : -1;
        for (var k = 0; k < boxes.Count; k++)
        {
            var rightToLeft = stops[boxes[k].Start].IsRightToLeft;
            if (held >= boxes[k].Start && held < boxes[k].End)
            {
                return rightToLeft ? k : k + 1;
            }

            if (held < 0 && position.Index == boxes[k].Start)
            {
                return rightToLeft ? k + 1 : k;
            }
        }

        var edge = 0;
        var distance = double.MaxValue;
        for (var k = 0; k <= boxes.Count; k++)
        {
            var edgeX = k < boxes.Count ? boxes[k].Left : boxes[k - 1].Right;
            if (Math.Abs(edgeX - x) < distance)
            {
                distance = Math.Abs(edgeX - x);
                edge = k;
            }
        }

        return edge;
    }

    private int LogicalLineEnd(TextLineMetrics line)
    {
        var text = Text ?? string.Empty;
        for (var c = line.Start; c < line.End && c < text.Length; c++)
        {
            if (text[c] is '\n' or '\r')
            {
                return c;
            }
        }

        return line.End >= text.Length ? text.Length : -1;
    }

    private CaretPosition LeftEdgeOf((int Start, int End, double Left, double Right) box) =>
        Stops()[box.Start].IsRightToLeft ? new CaretPosition(box.End, true) : new CaretPosition(box.Start);

    private CaretPosition RightEdgeOf((int Start, int End, double Left, double Right) box) =>
        Stops()[box.Start].IsRightToLeft ? new CaretPosition(box.Start) : new CaretPosition(box.End, true);

    private void SelectBetween(int lineIndex, double left, double right, bool[] selected)
    {
        foreach (var box in LineBoxes(lineIndex))
        {
            var middle = (box.Left + box.Right) / 2;
            if (middle <= left || middle >= right)
            {
                continue;
            }

            for (var c = box.Start; c < box.End; c++)
            {
                selected[c] = true;
            }
        }
    }

    private IEnumerable<(int Line, double Left, double Right)> RangeSegments(int start, int end)
    {
        var stops = Stops();
        start = Math.Max(0, start);
        end = Math.Min(end, stops.Length - 1);
        var line = -1;
        var pieces = new List<(double Left, double Right)>();
        for (var i = start; i < end; i++)
        {
            if (stops[i].LineIndex != line)
            {
                foreach (var piece in Merged(pieces))
                {
                    yield return (line, piece.Left, piece.Right);
                }

                pieces.Clear();
                line = stops[i].LineIndex;
            }

            pieces.Add((stops[i].Left, stops[i].Right));
        }

        foreach (var piece in Merged(pieces))
        {
            yield return (line, piece.Left, piece.Right);
        }
    }

    private static List<(double Left, double Right)> Merged(List<(double Left, double Right)> pieces)
    {
        var merged = new List<(double Left, double Right)>();
        foreach (var piece in pieces.OrderBy(p => p.Left))
        {
            if (merged.Count > 0 && piece.Left <= merged[merged.Count - 1].Right + 0.01)
            {
                var last = merged[merged.Count - 1];
                merged[merged.Count - 1] = (last.Left, Math.Max(last.Right, piece.Right));
                continue;
            }

            merged.Add(piece);
        }

        return merged;
    }

    private static void DistributeCluster(string text, int start, int end, double x, double width, int line,
        bool rightToLeft, CaretStop[] stops, bool[] filled)
    {
        var count = 0;
        for (var c = start; c < end; c++)
        {
            if (!IsTrailingSurrogate(text, c))
            {
                count++;
            }
        }

        var share = width / Math.Max(count, 1);
        var ordinal = 0;
        for (var c = start; c < end; c++)
        {
            if (IsTrailingSurrogate(text, c))
            {
                stops[c] = new CaretStop(stops[c - 1].X, line, 0, rightToLeft);
            }
            else
            {
                var caret = rightToLeft ? x + width - share * ordinal : x + share * ordinal;
                stops[c] = new CaretStop(caret, line, share, rightToLeft);
                ordinal++;
            }

            filled[c] = true;
        }
    }

    private static double Hang(GlyphWordData glyph, bool left)
    {
        if ((glyph.PositionInString < 0 && glyph.Symbol != '-') || !MarginHangs.TryGetValue(glyph.Symbol, out var hang))
        {
            return 0;
        }

        return (left ? hang.Left : hang.Right) * glyph.Advance;
    }

    private static bool IsBlank(char symbol)
    {
        return symbol == ' ' || symbol == '\t';
    }

    private static bool IsWordLetter(string text, int index, int start, int end)
    {
        var symbol = text[index];
        if (symbol is '\'' or '’')
        {
            return index > start && index + 1 < end && char.IsLetter(text[index - 1]) && char.IsLetter(text[index + 1]);
        }

        return char.IsLetter(symbol) || CharUnicodeInfo.GetUnicodeCategory(symbol) is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark;
    }

    private static Glyph HyphenGlyph(IFont font)
    {
        return font.TryGetGlyphIndex(0x2010, out var hyphen)
            ? font.GetGlyphByIndex(hyphen)
            : font.GetGlyphByCharacter('-');
    }

    private static bool IsTrailingSurrogate(string text, int index)
    {
        return index > 0 && char.IsLowSurrogate(text[index]) && char.IsHighSurrogate(text[index - 1]);
    }

    private List<(int Start, int End, BidiParagraph Paragraph)> ResolveParagraphs(string text)
    {
        if (!BidiParagraph.IsNeeded(text, 0, text.Length, Direction))
        {
            return null;
        }

        var paragraphs = new List<(int Start, int End, BidiParagraph Paragraph)>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (BidiParagraph.IsParagraphSeparator(text, i))
            {
                paragraphs.Add((start, i + 1, new BidiParagraph(text, start, i + 1, Direction)));
                start = i + 1;
            }
        }

        if (start < text.Length || paragraphs.Count == 0)
        {
            paragraphs.Add((start, text.Length, new BidiParagraph(text, start, text.Length, Direction)));
        }

        return paragraphs;
    }

    private (int Start, int End, BidiParagraph Paragraph) ParagraphAt(int index)
    {
        var low = 0;
        var high = _paragraphs.Count - 1;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (index < _paragraphs[middle].End)
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }

        return _paragraphs[low];
    }

    private bool IsRightToLeftAt(int index)
    {
        if (_paragraphs == null || index < 0)
        {
            return false;
        }

        var level = _characterLevels != null && index < _characterLevels.Length
            ? _characterLevels[index]
            : ParagraphAt(index).Paragraph.LevelAt(index);
        return (level & 1) == 1;
    }

    private IEnumerable<(int Start, int End, TextDirection Direction)> LevelRuns(int start, int end)
    {
        if (_paragraphs == null)
        {
            yield return (start, end, TextDirection.Auto);
            yield break;
        }

        var runStart = start;
        var level = ParagraphAt(start).Paragraph.LevelAt(start);
        for (var i = start + 1; i < end; i++)
        {
            var next = ParagraphAt(i).Paragraph.LevelAt(i);
            if (next == level)
            {
                continue;
            }

            yield return (runStart, i, DirectionOf(level));
            runStart = i;
            level = next;
        }

        yield return (runStart, end, DirectionOf(level));
    }

    private static TextDirection DirectionOf(int level) =>
        (level & 1) == 1 ? TextDirection.RightToLeft : TextDirection.LeftToRight;

    private static ShapedGlyph[] ToLogicalOrder(ShapedGlyph[] visual)
    {
        var logical = new ShapedGlyph[visual.Length];
        var start = 0;
        while (start < visual.Length)
        {
            var end = start + 1;
            while (end < visual.Length && visual[end].Cluster <= visual[end - 1].Cluster)
            {
                end++;
            }

            for (var k = start; k < end; k++)
            {
                logical[start + end - 1 - k] = visual[k];
            }

            start = end;
        }

        return logical;
    }

    private void ReorderLines(List<GlyphWordData> glyphs)
    {
        if (_paragraphs == null)
        {
            return;
        }

        _characterLevels = new byte[_paragraphs[_paragraphs.Count - 1].End];
        var start = 0;
        while (start < glyphs.Count)
        {
            var end = start;
            while (end < glyphs.Count && glyphs[end].LineIndex == glyphs[start].LineIndex)
            {
                end++;
            }

            ReorderLine(glyphs, start, end);
            start = end;
        }
    }

    private void ReorderLine(List<GlyphWordData> glyphs, int start, int end)
    {
        var x = double.MaxValue;
        for (var k = start; k < end; k++)
        {
            x = Math.Min(x, glyphs[k].PenX);
        }

        var segment = start;
        while (segment < end)
        {
            var first = -1;
            for (var k = segment; k < end && first < 0; k++)
            {
                first = glyphs[k].PositionInString;
            }

            if (first < 0)
            {
                return;
            }

            var (_, paragraphEnd, paragraph) = ParagraphAt(first);
            var segmentEnd = segment;
            while (segmentEnd < end && glyphs[segmentEnd].PositionInString < paragraphEnd)
            {
                segmentEnd++;
            }

            var lineEnd = paragraphEnd;
            for (var k = segmentEnd; k < glyphs.Count && segmentEnd == end; k++)
            {
                if (glyphs[k].PositionInString >= first)
                {
                    lineEnd = Math.Min(lineEnd, glyphs[k].PositionInString);
                    break;
                }
            }

            x = PlaceInVisualOrder(glyphs, segment, segmentEnd, paragraph, first, lineEnd, x);
            segment = segmentEnd;
        }
    }

    private double PlaceInVisualOrder(List<GlyphWordData> glyphs, int start, int end, BidiParagraph paragraph,
        int first, int lineEnd, double x)
    {
        var lineLevels = paragraph.LineLevels(first, lineEnd);
        Array.Copy(lineLevels, 0, _characterLevels, first, lineLevels.Length);
        var levels = new byte[end - start];
        var moves = false;
        for (var k = start; k < end; k++)
        {
            var cluster = glyphs[k].PositionInString;
            levels[k - start] = cluster >= first && cluster < lineEnd
                ? lineLevels[cluster - first]
                : (byte)paragraph.BaseLevel;
            moves |= levels[k - start] != 0;
        }

        if (!moves)
        {
            return glyphs[end - 1].PenX + glyphs[end - 1].Advance;
        }

        foreach (var index in BidiParagraph.VisualOrder(levels))
        {
            var glyph = glyphs[start + index];
            var shift = x - glyph.PenX;
            glyph.PenX = x;
            glyph.Rect.X += (float)shift;
            x += glyph.Advance;
        }

        return x;
    }

    private List<ShapedItem> Shape(string text, double fontSize)
    {
        HasPendingFonts = false;
        var items = new List<ShapedItem>(text.Length);
        var runs = _attributed?.Runs;
        var runIndex = 0;
        var graphemes = Fallback == null ? null
            : ReferenceEquals(text, Text) ? Graphemes()
            : TextBoundaries.Graphemes(text);
        foreach (var (start, end, attributes) in ShapingSegments(text, fontSize))
        {
            var language = attributes?.Language ?? Language;
            var options = attributes == null && language == null
                ? ShapingOptions.Default
                : new ShapingOptions(null, language, attributes?.Features);
            var font = RunFont(attributes, fontSize);
            var position = start;
            while (position < end)
            {
                var newline = text.IndexOf('\n', position, end - position);
                var stop = newline < 0 ? end : newline;
                foreach (var (runStart, runEnd, runFont, pending) in FontRuns(text, graphemes, position, stop, font, language))
                {
                    var blank = pending ? runFont.GetGlyphByCharacter(' ') : null;
                    foreach (var (levelStart, levelEnd, direction) in LevelRuns(runStart, runEnd))
                    {
                        var shaped = direction == TextDirection.Auto
                            ? TextShaper.Shape(runFont, text, levelStart, levelEnd, options)
                            : TextShaper.Shape(runFont, text, levelStart, levelEnd,
                                new ShapingOptions(options.Script, options.Language, options.Features, direction));
                        if (direction == TextDirection.RightToLeft)
                        {
                            shaped = ToLogicalOrder(shaped);
                        }

                        foreach (var glyph in shaped)
                        {
                            var cluster = levelStart + glyph.Cluster;
                            var glyphAttributes = AttributesAt(cluster);
                            var size = glyphAttributes?.FontSize ?? fontSize;
                            var scale = size / runFont.UnitsPerEm;
                            var advance = glyph.XAdvance * scale;
                            if (advance > 0 && IsEmboldened(glyphAttributes))
                            {
                                advance += 2 * FontSynthesisRules.EmboldenPerSide(size) * size;
                            }

                            items.Add(new ShapedItem(blank ?? runFont.GetGlyphByIndex(glyph.GlyphIndex), text[cluster],
                                cluster, advance, glyph.XOffset * scale, glyph.YOffset * scale,
                                glyphAttributes, runFont, size));
                        }
                    }
                }

                if (newline < 0)
                {
                    break;
                }

                var newlineAttributes = AttributesAt(newline);
                items.Add(new ShapedItem(font.GetGlyphByCharacter('\n'), '\n', newline, 0, 0, 0, newlineAttributes,
                    font, newlineAttributes?.FontSize ?? fontSize));
                position = newline + 1;
            }
        }

        return items;

        TextAttributes AttributesAt(int index)
        {
            if (runs == null)
            {
                return null;
            }

            while (runIndex < runs.Count - 1 && index >= runs[runIndex].End)
            {
                runIndex++;
            }

            return runs[runIndex].Attributes;
        }
    }

    private IEnumerable<(int Start, int End, IFont Font, bool Pending)> FontRuns(string text, bool[] graphemes, int start,
        int end, IFont font, string language)
    {
        if (end <= start)
        {
            yield break;
        }

        if (Fallback == null)
        {
            yield return (start, end, font, false);
            yield break;
        }

        var runStart = start;
        IFont runFont = null;
        var runPending = false;
        var index = start;
        while (index < end)
        {
            var codepoint = char.IsHighSurrogate(text[index]) && index + 1 < end && char.IsLowSurrogate(text[index + 1])
                ? char.ConvertToUtf32(text[index], text[index + 1])
                : text[index];
            var nextGrapheme = TextBoundaries.Next(graphemes, index);
            var graphemeEnd = Math.Min(nextGrapheme, end);
            IFont chosen;
            var pending = false;
            if (runFont != null && (JoinsPrevious(codepoint) || IsSpaceOrControl(codepoint)))
            {
                chosen = runFont;
                pending = runPending;
            }
            else if (IsSpaceOrControl(codepoint))
            {
                chosen = font;
            }
            else
            {
                var emoji = EmojiPresentation.IsEmoji(text, index, nextGrapheme);
                if (emoji ? HasColorGlyph(font, codepoint) : font.TryGetGlyphIndex(codepoint, out _))
                {
                    chosen = font;
                }
                else if (LoadFontsInBackground)
                {
                    chosen = Fallback.FontFor(codepoint, font, language, emoji, out pending) ?? font;
                    HasPendingFonts |= pending;
                }
                else
                {
                    chosen = Fallback.FontFor(codepoint, font, language, emoji) ?? font;
                }
            }

            if (runFont == null)
            {
                runFont = chosen;
                runPending = pending;
            }
            else if (!ReferenceEquals(chosen, runFont) || pending != runPending)
            {
                yield return (runStart, index, runFont, runPending);
                runStart = index;
                runFont = chosen;
                runPending = pending;
            }

            index = graphemeEnd;
        }

        yield return (runStart, end, runFont, runPending);
    }

    private static bool HasColorGlyph(IFont font, int codepoint)
    {
        if (!font.TryGetGlyphIndex(codepoint, 0xFE0F, out var glyph) && !font.TryGetGlyphIndex(codepoint, out glyph))
        {
            return false;
        }

        return font.GetColorPaint(glyph).Count > 0 || font.GetColorLayers(glyph).Count > 0 ||
               (font.ColorBitmapSizes.Count > 0 && font.GetColorBitmap(glyph, 0) != null);
    }

    private static bool JoinsPrevious(int codepoint)
    {
        if (codepoint is 0x200C or 0x200D or >= 0xFE00 and <= 0xFE0F or >= 0x1F3FB and <= 0x1F3FF
            or >= 0xE0020 and <= 0xE007F or >= 0xE0100 and <= 0xE01EF)
        {
            return true;
        }

        return CharUnicodeInfo.GetUnicodeCategory(codepoint) is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
    }

    private static bool IsSpaceOrControl(int codepoint)
    {
        return CharUnicodeInfo.GetUnicodeCategory(codepoint) is UnicodeCategory.SpaceSeparator
            or UnicodeCategory.Control or UnicodeCategory.Format;
    }

    private IFont RunFont(TextAttributes attributes, double fontSize) =>
        (attributes?.Font ?? Font).AtOpticalSize((float)(attributes?.FontSize ?? fontSize));

    private IEnumerable<(int Start, int End, TextAttributes Attributes)> ShapingSegments(string text, double fontSize)
    {
        if (_attributed == null || _attributed.Runs.Count == 0)
        {
            yield return (0, text.Length, null);
            yield break;
        }

        var runs = _attributed.Runs;
        var start = runs[0].Start;
        var attributes = runs[0].Attributes;
        for (var i = 1; i < runs.Count; i++)
        {
            if (runs[i].Attributes.ShapesLike(attributes)
                && ReferenceEquals(RunFont(runs[i].Attributes, fontSize), RunFont(attributes, fontSize)))
            {
                continue;
            }

            yield return (start, runs[i].Start, attributes);
            start = runs[i].Start;
            attributes = runs[i].Attributes;
        }

        yield return (start, text.Length, attributes);
    }

    private static Vector4F GlyphColor(GlyphWordData word)
    {
        return word.Attributes?.Foreground is { } color ? color.ToVector4() : FontItem.InheritedColor;
    }

    private static bool IsEmboldened(TextAttributes attributes) =>
        attributes?.Synthesis is { } synthesis && (synthesis & FontSynthesis.Weight) != 0;

    private static bool IsSlanted(TextAttributes attributes) =>
        attributes?.Synthesis is { } synthesis && (synthesis & FontSynthesis.Style) != 0;

    private static double Baseline(GlyphWordData word) =>
        word.Rect.Bottom + word.Glyph.BoundingRectangle.Y * word.FontSize / word.Font.UnitsPerEm;

    private static double InkRight(GlyphWordData word) =>
        IsSlanted(word.Attributes)
            ? word.Rect.Right + Math.Max(0, Baseline(word) - word.Rect.Top) * FontSynthesisRules.Slant
            : word.Rect.Right;

    private static double InkLeft(GlyphWordData word) =>
        IsSlanted(word.Attributes)
            ? word.Rect.Left - Math.Max(0, word.Rect.Bottom - Baseline(word)) * FontSynthesisRules.Slant
            : word.Rect.Left;

    private Vector4F GlyphSynthesis(GlyphWordData word, Vector4F quad)
    {
        var embolden = IsEmboldened(word.Attributes)
            ? (float)(FontSynthesisRules.EmboldenPerSide(word.FontSize) * FontAtlas.MSDFTextureSize / FontAtlas.PixelRange)
            : 0f;
        if (!IsSlanted(word.Attributes) || quad.W <= 0)
        {
            return new Vector4F(embolden, 0, 0, 0);
        }

        return new Vector4F(embolden, (float)FontSynthesisRules.Slant, (float)((Baseline(word) - quad.Y) / quad.W), 0);
    }

    private readonly struct ShapedItem
    {
        public ShapedItem(Glyph glyph, char symbol, int cluster, double advance, double offsetX, double offsetY,
            TextAttributes attributes, IFont font, double fontSize)
        {
            Font = font;
            Glyph = glyph;
            Symbol = symbol;
            Cluster = cluster;
            Advance = advance;
            OffsetX = offsetX;
            OffsetY = offsetY;
            Attributes = attributes;
            FontSize = fontSize;
        }

        public IFont Font { get; }

        public double FontSize { get; }

        public Glyph Glyph { get; }

        public char Symbol { get; }

        public int Cluster { get; }

        public double Advance { get; }

        public double OffsetX { get; }

        public double OffsetY { get; }

        public TextAttributes Attributes { get; }
    }

    private static RectangleF CalculateGlyphPosition(
        IFont font,
        Glyph glyph,
        double glyphLeft,
        double glyphBase,
        double scale)
    {
        var verticalShift = -glyph.BoundingRectangle.Y * scale;
        var horizontalShift = font.GetLeftSideBearing(glyph.Index) * scale;

        var glyphWidth = glyph.BoundingRectangle.Width * scale;
        var glyphHeight = glyph.BoundingRectangle.Height * scale;
        var glyphTop = (Math.Round(glyphBase) - glyphHeight) + verticalShift;
        glyphLeft += horizontalShift;

        // X stays sub-pixel: snapping fractional advances made equal gaps render a pixel apart.
        return new RectangleF((float)glyphLeft, (float)glyphTop, (float)glyphWidth, (float)glyphHeight);
    }
}

class BackToFrontComparer : IComparer<FontItem>
{
    public int Compare(FontItem left, FontItem rigth)
    {
        return rigth.Depth.CompareTo(left.Depth);
    }
}

class FrontToBackComparer : IComparer<FontItem>
{
    public int Compare(FontItem left, FontItem rigth)
    {
        return left.Depth.CompareTo(rigth.Depth);
    }
}
