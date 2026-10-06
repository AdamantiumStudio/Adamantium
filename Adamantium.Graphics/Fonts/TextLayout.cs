using System;
using System.Collections.Generic;
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

    private const uint MaxItemsCount = 4096;

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

    private TextRenderingParameters _previousRenderingParameters;
    private int _laidOutTabSize;

    private List<GlyphWordData> _wordData;
    private AttributedText _attributed;
    private double _lineAdvance;
    private double _baselineInLine;
    private double _firstLineTop;
    private double _verticalShift;
    private CaretStop[] _caretStops;
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

        // Grown to fit: preallocating the 4096 cap cost 256KB per TextBlock.
        fontItems = Array.Empty<FontItem>();
    }

    private void EnsureItemCapacity(int needed)
    {
        if (needed <= fontItems.Length) return;

        var size = Math.Max(16, fontItems.Length);
        while (size < needed) size *= 2;
        Array.Resize(ref fontItems, (int)Math.Min(size, MaxItemsCount));
    }

    public GlyphWordData[] GetTextData()
    {
        return _wordData.ToArray();
    }

    // Takes the list explicitly: it is called mid-shaping, on data not yet published as _wordData.
    private void CalculateRealTextDimensions(List<GlyphWordData> glyphsData)
    {
        var minX = glyphsData.Min(x => x.Rect.Left);
        var maxX = glyphsData.Max(x => x.Rect.Right);
        var minY = glyphsData.Min(x => x.Rect.Top);
        var maxY = glyphsData.Max(x => x.Rect.Bottom);
        RealTextDimensions = new Size(maxX - minX, maxY - minY);
    }

    private bool CompareInputParameters(string text,
        double fontSize,
        TextRenderingParameters renderingParameters)
    {
        return Text == text && MathHelper.IsZero(FontSize - fontSize) &&
               _previousRenderingParameters == renderingParameters && _laidOutTabSize == TabSize;
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

        var _b0 = System.GC.GetAllocatedBytesForCurrentThread();


        var _b1 = System.GC.GetAllocatedBytesForCurrentThread();

        var scale = fontSize / Font.UnitsPerEm;
        var items = Shape(text, scale);
        _laidOutTabSize = TabSize;
        var tabStop = spaceGlyph.AdvanceWidth * scale * Math.Max(TabSize, 1);

        var _b2 = System.GC.GetAllocatedBytesForCurrentThread();

        var lineHeight = Font.LineGap == 0 ? fontSize : Font.LineGap * scale;
        lineHeight += fontSize;
        var baseLine = Font.Baseline * scale;

        dotGlyphsWidth = (dotGlyph.AdvanceWidth * scale * 3);

        var textArea = renderingParameters.TextArea;
        double width = 0;
        double height = textArea.Y;
        double cursorPosition = 0;
        double wordStartPosition = 0;
        var glyphsData = new List<GlyphWordData>();
        int wordIndex = 0;
        int lineIndex = 0;
        double verticalShift = 0;
        var lineBreaks = renderingParameters.TextWrapping == TextWrapping.WrapByWords
            ? TextBoundaries.LineBreaks(text)
            : null;
        var wordStart = 0;
        while (wordStart <= items.Count)
        {
            var wordEnd = wordStart;
            while (wordEnd < items.Count && !IsBlank(items[wordEnd].Symbol)
                   && (wordEnd == wordStart || lineBreaks?[items[wordEnd].Cluster] != LineBreakKind.Allowed))
            {
                wordEnd++;
            }

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
                var advance = isTab ? (Math.Floor(cursorPosition / tabStop) + 1) * tabStop - cursorPosition : space.Advance;
                var rect = new RectangleF((float)cursorPosition,
                    (float)(height + baseLine),
                    (float)advance,
                    0f);
                glyphsData.Add(new GlyphWordData(isTab ? spaceGlyph : space.Glyph, space.Symbol, rect, space.Cluster,
                    lineIndex)
                {
                    PenX = cursorPosition,
                    Advance = advance,
                    Attributes = space.Attributes,
                });
                cursorPosition += advance;
            }

            wordIndex++;
            wordStart = wordEnd + 1;
        }

        // Not published yet: the render thread reads _wordData, and the alignment below still moves every glyph.
        // The height is a font metric (last baseline plus descent), not the ink, so same-size strings measure alike and
        // a turned label keeps its descenders.
        var lastBaseline = height + baseLine;
        height = lastBaseline + System.Math.Abs(Font.Descender) * scale;

        var _b3 = System.GC.GetAllocatedBytesForCurrentThread();


        CalculateRealTextDimensions(glyphsData);

        var maxX = glyphsData.Max(x => x.Rect.Right);
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
        _lineAdvance = lineHeight;
        _baselineInLine = baseLine;
        _firstLineTop = textArea.Y + verticalShift;
        _verticalShift = verticalShift;
        _caretStops = null;
        _graphemes = null;
        _words = null;

        var _b4 = System.GC.GetAllocatedBytesForCurrentThread();

        TranslateBytes += _b1 - _b0; FeatureBytes += _b2 - _b1; WordLoopBytes += _b3 - _b2; TailBytes += _b4 - _b3; ProcessCount++;

        // One reference write: a reader sees the previous layout or this one, never a half-aligned mixture.
        _wordData = glyphsData;

        CalculatedLayoutSize = finalRect;

        return CalculatedLayoutSize;

        bool ProcessWord(int start, int end)
        {
            double wordWidth = 0;
            for (var k = start; k < end; k++)
            {
                if (items[k].Symbol != '\n')
                {
                    wordWidth += items[k].Glyph.BoundingRectangle.Width * scale;
                }
            }

            // Wrapped at the word boundary, before any glyph is laid: a word that does not fit starts a new line, and
            // a word wider than the line only overflows as the first on its line.
            if (renderingParameters.TextWrapping == TextWrapping.WrapByWords
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
            for (var i = start; i < end; i++)
            {
                var item = items[i];
                switch (item.Symbol)
                {
                    case '\n':
                        if (EmitNewlineCarets)
                        {
                            var caretRect = new RectangleF((float)cursorPosition, (float)(height + baseLine), 0f, 0f);
                            glyphsData.Add(new GlyphWordData(item.Glyph, '\n', caretRect, item.Cluster, lineIndex)
                            {
                                PenX = cursorPosition,
                                Attributes = item.Attributes,
                            });
                        }
                        height += lineHeight;
                        cursorPosition = 0;
                        lineIndex++;
                        break;
                    default:
                    {
                        var glyphBase = height + baseLine;
                        var glyphRect = CalculateGlyphPosition(item.Glyph,
                            cursorPosition + item.OffsetX,
                            glyphBase - item.OffsetY,
                            scale);

                        glyphsData.Add(new GlyphWordData(item.Glyph, item.Symbol, glyphRect, item.Cluster, lineIndex)
                        {
                            PenX = cursorPosition,
                            Advance = item.Advance,
                            OffsetX = item.OffsetX,
                            OffsetY = item.OffsetY,
                            Attributes = item.Attributes,
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
                                        PrepareDataAndTrim(glyphsDataCopy, i, glyphBase);
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
                                            PrepareDataAndTrim(glyphsDataCopy, i, glyphBase);
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
            var minX = glyphsData.Min(x => x.Rect.Left);
            var maxX = glyphsData.Max(x => x.Rect.Right);
            var minY = glyphsData.Min(x => x.Rect.Top);
            switch (renderingParameters.HorizontalTextAlignment)
            {
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
                    for (int i = 0; i <= maxLines; ++i)
                    {
                        // The last line stays ragged unless JustifyLastLine asks otherwise.
                        if (i == maxLines && !renderingParameters.JustifyLastLine) break;

                        // Only the gaps between words widen; glyphs are shifted, never re-laid, so kerning and bearings stay.
                        var lineGlyphs = glyphsData.Where(x => x.LineIndex == i).OrderBy(x => x.Rect.X).ToArray();
                        if (lineGlyphs.Length == 0) continue;

                        int firstInk = -1, lastInk = -1;
                        for (int k = 0; k < lineGlyphs.Length; k++)
                        {
                            if (IsBlank(lineGlyphs[k].Symbol)) continue;
                            if (firstInk < 0) firstInk = k;
                            lastInk = k;
                        }
                        if (firstInk < 0) continue;

                        var spaceCount = 0;
                        for (int k = firstInk + 1; k < lastInk; k++)
                            if (lineGlyphs[k].Symbol == ' ') spaceCount++;
                        if (spaceCount == 0) continue;

                        var extra = finalRect.Width - lineGlyphs[lastInk].Rect.Right;
                        if (extra <= 0) continue;

                        var perSpace = extra / spaceCount;
                        double shift = 0;
                        for (int k = 0; k < lineGlyphs.Length; k++)
                        {
                            var rect = lineGlyphs[k].Rect;
                            rect.X += (float)shift;
                            lineGlyphs[k].Rect = rect;
                            lineGlyphs[k].PenX += shift;
                            if (k > firstInk && k < lastInk && lineGlyphs[k].Symbol == ' ')
                                shift += perSpace;
                        }
                    }
                }
                break;
            }

            switch (renderingParameters.VerticalTextAlignment)
            {
                case VerticalTextAlignment.Center:
                {
                    // Centered by ascent above the baseline, not the ink: ink differs per string and made same-size
                    // text wobble. No descent reserve, so descenders hang below.
                    var lineCount = glyphsData.Max(x => x.LineIndex) + 1;
                    var ascent = Font.Ascender * scale;
                    var blockHeight = (lineCount - 1) * lineHeight + ascent;
                    var blockTop = baseLine - ascent;
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
                if (index == 0 && glyphData.Glyph == spaceGlyph) continue;

                var glyphRect = CalculateGlyphPosition(glyphData.Glyph,
                    cursorPosition + glyphData.OffsetX,
                    glyphBase - glyphData.OffsetY,
                    scale);

                glyphData.Rect = glyphRect;
                glyphData.PenX = cursorPosition;
                glyphData.LineIndex = lineIndex;
                cursorPosition += glyphData.Advance;
            }
        }

        void PrepareDataAndTrim(GlyphWordData[] glyphsDataCopy, int position, double glyphBase)
        {
            for (int k = glyphsDataCopy.Length - 1; k >= 0; k--)
            {
                var data = glyphsData[k];
                cursorPosition -= data.Advance;
                glyphsData.RemoveAt(k);
                if (renderingParameters.TextTrimming == TextTrimming.None &&
                    cursorPosition <= textArea.Width)
                {
                    break;
                }
                else if (renderingParameters.TextTrimming == TextTrimming.CharEllipses &&
                    cursorPosition + dotGlyphsWidth <= textArea.Width)
                {
                    break;
                }
                else if (renderingParameters.TextTrimming ==
                         TextTrimming.WordEllipses &&
                         cursorPosition + dotGlyphsWidth <= textArea.Width)
                {
                    if (wordIndex > 0 && data.Glyph != spaceGlyph)
                    {
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (renderingParameters.TextTrimming != TextTrimming.None)
            {
                TrimText(position, glyphBase);
            }

            width = cursorPosition;
        }

        void TrimText(int position, double glyphBase)
        {
            for (int j = 0; j < 3; j++)
            {
                var glyphRect = CalculateGlyphPosition(dotGlyph,
                    cursorPosition,
                    glyphBase,
                    scale);
                glyphsData.Add(new GlyphWordData(dotGlyph, '.', glyphRect, -1, lineIndex)
                {
                    PenX = cursorPosition,
                    Advance = dotGlyph.AdvanceWidth * scale,
                });

                cursorPosition += dotGlyph.AdvanceWidth * scale;
            }
        }
    }

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
            FontAtlas = FontAtlasStore.GetOrCreateFrom(graphicsDevice, Typeface, Font,
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
        atlas.RequestAsync(_wordData.Select(x => x.Glyph).Append(dotGlyph));
        _atlasVersion = atlas.Version;
        ElementsCount = 0;
        // No vertex buffer here: only the direct draw path needs one (EnsureVertexBuffer), and one per block ran the BAR
        // heap out of memory.

        for (int i = 0; i < _wordData.Count; ++i)
        {
            var word = _wordData[i];
            if (word.Glyph == spaceGlyph || word.Symbol == '\n') continue;

            // The full cell (body plus margin), so outline/glow/shadow have room to draw.
            var gd = FontAtlas.GetGlyphData(word.Glyph.Index);
            FontItem item;
            if (gd != null && gd.BoundingRect.Width > 0 && gd.BoundingRect.Height > 0)
            {
                var rect = word.Rect;
                // Per-axis margin maps the body exactly onto the pixel-snapped rect; a zero-size side falls back to the
                // uniform margin, or the quad collapses.
                var uniform = gd.Margin * (double)FontSize / FontAtlas.MSDFTextureSize;
                var mx = rect.Width > 0 ? gd.Margin * (double)rect.Width / gd.BoundingRect.Width : uniform;
                var my = rect.Height > 0 ? gd.Margin * (double)rect.Height / gd.BoundingRect.Height : uniform;
                item = new FontItem
                {
                    ArrangeRect = new Vector4F((float)(rect.X - mx), (float)(rect.Y - my),
                        (float)(rect.Width + 2 * mx), (float)(rect.Height + 2 * my)),
                    Source = gd.UVRectFull,
                    Layer = gd.DepthLayer,
                    Depth = 1.0f,
                    Color = GlyphColor(word)
                };
            }
            else if (gd != null)
            {
                item = new FontItem
                {
                    ArrangeRect = word.Rect,
                    Source = FontAtlas.GetUVCoordinatesForGlyph(word.Glyph.Index),
                    Layer = gd.DepthLayer,
                    Depth = 1.0f,
                    Color = GlyphColor(word)
                };
            }
            else
            {
                // Not rasterized yet: no quad, or it draws a piece of a neighbor.
                continue;
            }
            if (ElementsCount >= MaxItemsCount) break;
            EnsureItemCapacity((int)ElementsCount + 1);
            fontItems[ElementsCount] = item;
            ElementsCount++;
        }

        _vertexBufferDirty = true;

        _textUpdated = true;
    }

    /// <summary>
    /// Creates and uploads the per-block vertex buffer on demand; only the direct draw path (rotated/sheared text) uses it.
    /// </summary>
    public void EnsureVertexBuffer(IGraphicsDevice graphicsDevice)
    {
        VertexBuffer ??= Adamantium.Graphics.Buffer.Vertex.New<FontItem>(graphicsDevice, MaxItemsCount,
            Adamantium.Graphics.BufferMemoryUsage.UploadFromCpuToGpu);
        if (_vertexBufferDirty)
        {
            VertexBuffer.SetData(fontItems, 0, ElementsCount, 0);
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
                width += data[j].Advance;
                j++;
            }

            var next = j < data.Count && data[j].PositionInString > cluster ? data[j].PositionInString : text.Length;
            DistributeCluster(text, cluster, next, x, width, line, stops, filled);
            endX = x + width;
            endLine = line;
            i = j;
        }

        if (text[text.Length - 1] == '\n')
        {
            stops[text.Length] = new CaretStop(0, endLine + 1);
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
    public int LineCount => Stops().Max(s => s.LineIndex) + 1;

    /// <summary>A visual line: the characters it shows and where it stands.</summary>
    public TextLineMetrics GetLine(int lineIndex)
    {
        var stops = Stops();
        var start = -1;
        var end = stops.Length - 1;
        var width = 0.0;
        for (var i = 0; i < stops.Length; i++)
        {
            if (stops[i].LineIndex < lineIndex)
            {
                continue;
            }

            if (stops[i].LineIndex > lineIndex)
            {
                end = i;
                break;
            }

            if (start < 0)
            {
                start = i;
            }

            width = Math.Max(width, stops[i].X + stops[i].Width);
        }

        if (start < 0)
        {
            start = end;
        }

        var top = _firstLineTop + lineIndex * _lineAdvance;
        var baseline = Math.Round(top - _verticalShift + _baselineInLine) + _verticalShift;
        return new TextLineMetrics(start, end, top, baseline, _lineAdvance, width);
    }

    /// <summary>Rectangles covering a UTF-16 range, one per piece of each visual line it spans, as high as the line:
    /// for selection, search hits, backgrounds.</summary>
    public IReadOnlyList<RectangleF> GetRangeRects(int start, int end)
    {
        return RangeSegments(start, end)
            .Select(s => new RectangleF((float)s.Left, (float)(_firstLineTop + s.Line * _lineAdvance),
                (float)(s.Right - s.Left), (float)_lineAdvance))
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

        var scale = FontSize / Font.UnitsPerEm;
        double em = Font.UnitsPerEm;
        var thickness = (Font.UnderlineThickness > 0 ? Font.UnderlineThickness : em / 20) * scale;
        var underline = (Font.UnderlinePosition != 0 ? Font.UnderlinePosition : -em / 10) * scale;
        var strikeout = (Font.StrikeoutPosition != 0 ? Font.StrikeoutPosition : em / 4) * scale;
        var strikeoutSize = Font.StrikeoutSize > 0 ? Font.StrikeoutSize * scale : thickness;

        foreach (var run in _attributed.Runs)
        {
            var attributes = run.Attributes;
            var decorations = attributes.Decorations ?? TextDecorations.None;
            if (attributes.Background == null && decorations == TextDecorations.None)
            {
                continue;
            }

            var lineColor = attributes.DecorationColor ?? attributes.Foreground;
            foreach (var segment in RangeSegments(run.Start, run.End))
            {
                var left = (float)segment.Left;
                var width = (float)(segment.Right - segment.Left);
                var baseline = GetLine(segment.Line).Baseline;
                if (attributes.Background is { } background)
                {
                    adornments.Add(new TextAdornment(TextAdornmentKind.Background,
                        new RectangleF(left, (float)(_firstLineTop + segment.Line * _lineAdvance), width,
                            (float)_lineAdvance), background));
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
        var lineIndex = (int)Math.Floor((y - _firstLineTop) / _lineAdvance);
        var insideY = lineIndex >= 0 && lineIndex < LineCount;
        lineIndex = Math.Max(0, Math.Min(lineIndex, LineCount - 1));
        var line = GetLine(lineIndex);

        var last = line.Start;
        for (var g = line.Start; g < line.End && g < text.Length; g = TextBoundaries.Next(graphemes, g))
        {
            if (text[g] is '\n' or '\r')
            {
                return new TextHit(g, false, false, g);
            }

            var next = TextBoundaries.Next(graphemes, g);
            var left = stops[g].X;
            var right = left;
            for (var c = g; c < next; c++)
            {
                right = Math.Max(right, stops[c].X + stops[c].Width);
            }

            var inside = insideY && x >= left && x < right;
            if (x < (left + right) / 2)
            {
                return new TextHit(g, false, inside, g);
            }

            if (x < right)
            {
                return new TextHit(g, true, inside, next);
            }

            last = g;
        }

        var end = TextBoundaries.Next(graphemes, last);
        return new TextHit(last, true, false, Math.Min(end, line.End));
    }

    private int Clamp(int index) => Math.Max(0, Math.Min(index, (Text ?? string.Empty).Length));

    private bool[] Graphemes() => _graphemes ??= TextBoundaries.Graphemes(Text ?? string.Empty);

    private bool[] Words() => _words ??= TextBoundaries.Words(Text ?? string.Empty);

    private CaretStop[] Stops() => _caretStops ??= GetCaretStops();

    private IEnumerable<(int Line, double Left, double Right)> RangeSegments(int start, int end)
    {
        var stops = Stops();
        start = Math.Max(0, start);
        end = Math.Min(end, stops.Length - 1);
        var line = -1;
        double left = 0;
        double right = 0;
        for (var i = start; i < end; i++)
        {
            if (stops[i].LineIndex != line)
            {
                if (line >= 0)
                {
                    yield return (line, left, right);
                }

                line = stops[i].LineIndex;
                left = stops[i].X;
                right = left;
            }

            right = Math.Max(right, stops[i].X + stops[i].Width);
        }

        if (line >= 0)
        {
            yield return (line, left, right);
        }
    }

    private static void DistributeCluster(string text, int start, int end, double x, double width, int line,
        CaretStop[] stops, bool[] filled)
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
                stops[c] = new CaretStop(stops[c - 1].X, line);
            }
            else
            {
                stops[c] = new CaretStop(x + share * ordinal, line, share);
                ordinal++;
            }

            filled[c] = true;
        }
    }

    private static bool IsBlank(char symbol)
    {
        return symbol == ' ' || symbol == '\t';
    }

    private static bool IsTrailingSurrogate(string text, int index)
    {
        return index > 0 && char.IsLowSurrogate(text[index]) && char.IsHighSurrogate(text[index - 1]);
    }

    private List<ShapedItem> Shape(string text, double scale)
    {
        var items = new List<ShapedItem>(text.Length);
        var runs = _attributed?.Runs;
        var runIndex = 0;
        foreach (var (start, end, attributes) in ShapingSegments(text))
        {
            var options = attributes == null
                ? ShapingOptions.Default
                : new ShapingOptions(null, attributes.Language, attributes.Features);
            var position = start;
            while (position < end)
            {
                var newline = text.IndexOf('\n', position, end - position);
                var stop = newline < 0 ? end : newline;
                if (stop > position)
                {
                    foreach (var glyph in TextShaper.Shape(Font, text.Substring(position, stop - position), options))
                    {
                        var cluster = position + glyph.Cluster;
                        items.Add(new ShapedItem(Font.GetGlyphByIndex(glyph.GlyphIndex), text[cluster], cluster,
                            glyph.XAdvance * scale, glyph.XOffset * scale, glyph.YOffset * scale,
                            AttributesAt(cluster)));
                    }
                }

                if (newline < 0)
                {
                    break;
                }

                items.Add(new ShapedItem(Font.GetGlyphByCharacter('\n'), '\n', newline, 0, 0, 0, AttributesAt(newline)));
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

    private IEnumerable<(int Start, int End, TextAttributes Attributes)> ShapingSegments(string text)
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
            if (runs[i].Attributes.ShapesLike(attributes))
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

    private readonly struct ShapedItem
    {
        public ShapedItem(Glyph glyph, char symbol, int cluster, double advance, double offsetX, double offsetY,
            TextAttributes attributes)
        {
            Glyph = glyph;
            Symbol = symbol;
            Cluster = cluster;
            Advance = advance;
            OffsetX = offsetX;
            OffsetY = offsetY;
            Attributes = attributes;
        }

        public Glyph Glyph { get; }

        public char Symbol { get; }

        public int Cluster { get; }

        public double Advance { get; }

        public double OffsetX { get; }

        public double OffsetY { get; }

        public TextAttributes Attributes { get; }
    }

    private RectangleF CalculateGlyphPosition(
        Glyph glyph,
        double glyphLeft,
        double glyphBase,
        double scale)
    {
        var verticalShift = -glyph.BoundingRectangle.Y * scale;
        var horizontalShift = glyph.LeftSideBearing * scale;

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
