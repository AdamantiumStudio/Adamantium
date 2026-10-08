using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Parsers;
using Adamantium.Fonts.Svg;
using Adamantium.Fonts.Tables;
using Adamantium.Fonts.Tables.CFF;
using Adamantium.Fonts.Tables.CMAP;
using Adamantium.Mathematics;

namespace Adamantium.Fonts
{
    public class Font : IFont
    {
        private List<Glyph> glyphs;
        private List<UInt32> unicodes;
        private Dictionary<string, Glyph> nameToGlyph;
        private Dictionary<UInt32, Glyph> unicodeToGlyph;
        private Dictionary<string, List<Feature>> featuresMap;
        private ushort[] advanceWidths;
        private short[] leftSideBearings;
        private Dictionary<string, Font> instances = new();
        private Font baseFont;
        private float[] coordinates;
        private int[] variedAdvances;
        private readonly ConcurrentDictionary<ulong, ColorLayer[]> colorLayerCache = new();
        private ConcurrentDictionary<ulong, ColorPaintOperation[]> colorPaintCache = new();
        private readonly ConcurrentDictionary<ulong, ColorBitmap> colorBitmapCache = new();
        private readonly Dictionary<uint, Glyph> outlineGlyphs = new();
        private readonly Dictionary<string, Glyph> outlineGlyphsByKey = new();
        private readonly Dictionary<ushort, string> names = new();
        private readonly Lazy<GlyphSubstitutionMap> substitutionMap;
        public Typeface Typeface { get; private set; }
        internal VariationStore VariationData { get; set; }
        internal List<InstanceRecord> InstanceData { get; set; }

        public Font(Typeface typeface)
        {
            Typeface = typeface;
            glyphs = new List<Glyph>();
            unicodes = new List<uint>();

            nameToGlyph = new Dictionary<string, Glyph>();
            unicodeToGlyph = new Dictionary<uint, Glyph>();

            featuresMap = new Dictionary<string, List<Feature>>();
            substitutionMap = new Lazy<GlyphSubstitutionMap>(() => new GlyphSubstitutionMap(this));

            Copyright = String.Empty;
            FontFamily = String.Empty;
            FontSubfamily = String.Empty;
            UniqueId = String.Empty;
            FullName = String.Empty;
            Version = String.Empty;
            Trademark = String.Empty;
            Manufacturer = String.Empty;
            Designer = String.Empty;
            Description = String.Empty;
            VendorUrl = String.Empty;
            DesignerUrl = String.Empty;
            LicenseDescription = String.Empty;
            LicenseInfoUrl = String.Empty;
            TypographicFamilyName = String.Empty;
            TypographicSubfamilyName = String.Empty;
            WwsFamilyName = String.Empty;
            WwsSubfamilyName = String.Empty;
            LightBackgroundPalette = String.Empty;
            DarkBackgroundPalette = String.Empty;
        }

        public bool IsGlyphNamesProvided { get; internal set; }

        // Name info section ---
        public string Copyright { get; internal set; }
        public string FontFamily { get; internal set; }
        public string FontSubfamily { get; internal set; }
        public string UniqueId { get; internal set; }
        public string FullName { get; internal set; }
        public string Version { get; internal set; }
        public string Trademark { get; internal set; }
        public string Manufacturer { get; internal set; }
        public string Designer { get; internal set; }
        public string Description { get; internal set; }
        public string VendorUrl { get; internal set; }
        public string DesignerUrl { get; internal set; }
        public string LicenseDescription { get; internal set; }
        public string LicenseInfoUrl { get; internal set; }
        public string TypographicFamilyName { get; internal set; }
        public string TypographicSubfamilyName { get; internal set; }
        public string WwsFamilyName { get; internal set; }
        public string WwsSubfamilyName { get; internal set; }
        public string LightBackgroundPalette { get; internal set; }
        public string DarkBackgroundPalette { get; internal set; }

        /// <inheritdoc />
        public string GetName(ushort nameId) => names.TryGetValue(nameId, out var text) ? text : null;

        internal void SetName(ushort nameId, string text) => names[nameId] = text;

        // ------
        public FeatureCatalog FeatureCatalog { get; } = new FeatureCatalog();

        /// <inheritdoc />
        public IReadOnlyList<GlyphAlternate> GetGlyphAlternates(uint glyphIndex) => substitutionMap.Value.Alternates(glyphIndex);

        /// <inheritdoc />
        public IReadOnlyList<string> GetGlyphText(uint glyphIndex) => substitutionMap.Value.Text(glyphIndex);

        internal IReadOnlyDictionary<uint, Glyph> UnicodeToGlyph => unicodeToGlyph;
        public uint GlyphCount => (uint)glyphs.Count;
        public ushort UnitsPerEm { get; internal set; }
        public Int16 Ascender { get; internal set; }
        public Int16 Descender { get; internal set; }
        public Int16 CapsHeight { get; internal set; }
        
        public Int16 LineAscent { get; internal set; }

        public Int16 LineDescent { get; internal set; }

        public short LineGap { get; internal set; }

        public Int16 UnderlinePosition { get; internal set; }

        public Int16 UnderlineThickness { get; internal set; }

        public Int16 StrikeoutPosition { get; internal set; }

        public Int16 StrikeoutSize { get; internal set; }

        /// <summary>
        /// smallest readable size in pixels
        /// </summary>
        public UInt16 LowestRecPPEM { get; internal set; }

        /// <summary>
        /// space between lines
        /// </summary>
        public Double LineSpacingMultiplier { get; internal set; }

        public DateTime Created { get; internal set; }

        public DateTime Modified { get; internal set; }

        public IReadOnlyCollection<Glyph> Glyphs => glyphs.AsReadOnly();
        public IReadOnlyCollection<uint> Unicodes => unicodes.AsReadOnly();
        internal KerningSubtable[] KerningData { get; set; }
        internal OpenTypeLayout Layout { get; } = new OpenTypeLayout();

        OpenTypeLayout IFont.Layout => Layout;

        bool IFont.TryGetGlyphIndex(int codepoint, out uint glyphIndex)
        {
            if (unicodeToGlyph.TryGetValue((uint)codepoint, out var glyph))
            {
                glyphIndex = glyph.Index;
                return true;
            }

            glyphIndex = 0;
            return false;
        }

        bool IFont.TryGetGlyphIndex(int codepoint, int variationSelector, out uint glyphIndex)
        {
            glyphIndex = 0;
            if (VariationSequences == null ||
                !VariationSequences.TryGetVariant((uint)codepoint, (uint)variationSelector, out glyphIndex, out var isDefault))
            {
                return false;
            }

            return !isDefault || ((IFont)this).TryGetGlyphIndex(codepoint, out glyphIndex);
        }

        internal CharacterMapFormat14 VariationSequences { get; set; }

        internal void SetGlyphs(IEnumerable<Glyph> inputGlyphs)
        {
            glyphs.Clear();
            glyphs.AddRange(inputGlyphs);
        }

        internal void SetHorizontalMetrics(ushort[] advances, short[] bearings)
        {
            advanceWidths = advances;
            leftSideBearings = bearings;
        }

        public FontWeight Weight { get; internal set; } = FontWeight.Normal;

        public FontStyle Style { get; internal set; }

        public FontStretch Stretch { get; internal set; } = FontStretch.Normal;

        /// <inheritdoc />
        public IReadOnlyList<FontAxis> Axes { get; internal set; } = [];

        /// <inheritdoc />
        public IReadOnlyList<FontVariation> Variations { get; private set; } = [];

        internal AxisVariationTable AxisVariations { get; set; }

        internal GlyphVariationTable GlyphVariations { get; set; }

        internal HorizontalMetricsVariationTable MetricsVariations { get; set; }

        internal ColorLayerTable ColorLayers { get; set; }

        internal ColorPaletteTable ColorPaletteTable { get; set; }

        /// <inheritdoc />
        public IReadOnlyList<ColorPalette> ColorPalettes => ColorPaletteTable?.Palettes ?? [];

        /// <inheritdoc />
        public IReadOnlyList<ColorLayer> GetColorLayers(uint glyphIndex) => GetColorLayers(glyphIndex, 0);

        /// <inheritdoc />
        public IReadOnlyList<ColorLayer> GetColorLayers(uint glyphIndex, int palette)
        {
            if (ColorLayers == null)
            {
                return [];
            }

            var chosen = ChoosePalette(palette);
            return colorLayerCache.GetOrAdd(PaletteKey(glyphIndex, chosen),
                _ => ColorLayers.GetLayers(glyphIndex, ColorPaletteTable, chosen));
        }

        private int ChoosePalette(int palette) =>
            ColorPaletteTable != null && palette > 0 && palette < ColorPaletteTable.PaletteCount ? palette : 0;

        private static ulong PaletteKey(uint glyphIndex, int palette) => (ulong)(uint)palette << 32 | glyphIndex;

        internal ColorBitmapTable ColorBitmaps { get; set; }

        /// <inheritdoc />
        public IReadOnlyList<int> ColorBitmapSizes => ColorBitmaps?.Sizes ?? [];

        /// <inheritdoc />
        public ColorBitmap GetColorBitmap(uint glyphIndex, int pixelsPerEm)
        {
            if (ColorBitmaps == null)
            {
                return null;
            }

            return colorBitmapCache.GetOrAdd((ulong)(uint)pixelsPerEm << 32 | glyphIndex,
                _ => ColorBitmaps.GetBitmap(glyphIndex, pixelsPerEm));
        }

        internal ColorPaintTable ColorPaints { get; set; }

        /// <inheritdoc />
        public IReadOnlyList<ColorPaintOperation> GetColorPaint(uint glyphIndex) => GetColorPaint(glyphIndex, 0);

        /// <inheritdoc />
        public IReadOnlyList<ColorPaintOperation> GetColorPaint(uint glyphIndex, int palette)
        {
            if (ColorPaints == null && SvgDocuments == null)
            {
                return [];
            }

            var chosen = ChoosePalette(palette);
            return colorPaintCache.GetOrAdd(PaletteKey(glyphIndex, chosen), _ => PaintOf(glyphIndex, chosen));
        }

        /// <inheritdoc />
        public bool TryGetColorClipBox(uint glyphIndex, out RectangleF box)
        {
            box = default;
            return ColorPaints != null && ColorPaints.TryGetClipBox(glyphIndex, coordinates, out box);
        }

        internal SvgDocumentTable SvgDocuments { get; set; }

        private ColorPaintOperation[] PaintOf(uint glyphIndex, int palette)
        {
            var operations = ColorPaints?.GetOperations(glyphIndex, ColorPaletteTable, palette, coordinates) ?? [];
            if (operations.Length > 0 || SvgDocuments == null)
            {
                return operations;
            }

            return SvgGlyphConverter.Convert(SvgDocuments.GetDocument(glyphIndex), glyphIndex, ColorPaletteTable, palette,
                AddOutlineGlyph);
        }

        private uint AddOutlineGlyph(string key, List<List<OutlinePoint>> contours)
        {
            lock (outlineGlyphs)
            {
                if (outlineGlyphsByKey.TryGetValue(key, out var known))
                {
                    return known.Index;
                }

                var glyph = new Glyph(Typeface.GlyphCount + (uint)outlineGlyphs.Count, OutlineType.CompactFontFormat);
                foreach (var contour in contours)
                {
                    var outline = new Outline();
                    outline.Points.AddRange(contour);
                    glyph.AddOutline(outline);
                }

                glyph.RecalculateBounds(true);
                outlineGlyphs[glyph.Index] = glyph;
                outlineGlyphsByKey[key] = glyph;
                return glyph.Index;
            }
        }

        private bool TryGetOutlineGlyph(uint index, out Glyph glyph)
        {
            lock (outlineGlyphs)
            {
                return outlineGlyphs.TryGetValue(index, out glyph);
            }
        }

        /// <inheritdoc />
        public IFont GetInstance(IReadOnlyList<FontVariation> variations)
        {
            if (baseFont != null)
            {
                return baseFont.GetInstance(variations);
            }

            if (Axes.Count == 0 || Typeface.OutlineSource is not IVariableGlyphOutlineSource variable)
            {
                return this;
            }

            var normalized = new int[Axes.Count];
            var values = new FontVariation[Axes.Count];
            for (var a = 0; a < Axes.Count; a++)
            {
                var axis = Axes[a];
                var value = axis.DefaultValue;
                foreach (var variation in variations)
                {
                    if (variation.Tag == axis.Tag)
                    {
                        value = variation.Value;
                    }
                }

                value = Math.Max(axis.MinValue, Math.Min(axis.MaxValue, value));
                values[a] = new FontVariation(axis.Tag, value);
                normalized[a] = axis.Normalize(value);
                if (AxisVariations != null)
                {
                    normalized[a] = AxisVariations.Map(a, normalized[a]);
                }
            }

            if (normalized.All(n => n == 0))
            {
                return this;
            }

            lock (instances)
            {
                var key = string.Join(",", normalized);
                if (!instances.TryGetValue(key, out var instance))
                {
                    instance = CreateInstance(variable, normalized.Select(n => n / 16384f).ToArray(), values);
                    instances[key] = instance;
                }

                return instance;
            }
        }

        private Font CreateInstance(IVariableGlyphOutlineSource variable, float[] normalized, FontVariation[] values)
        {
            var baseGlyphs = Typeface.Glyphs.ToArray();
            var glyphs = new Glyph[baseGlyphs.Length];
            var source = variable.Vary(this, normalized, glyphs);
            if (source == null)
            {
                if (MetricsVariations == null && ColorPaints?.Varies != true)
                {
                    return this;
                }

                source = (IGlyphOutlineSource)variable;
            }

            var typeface = new Typeface { Parser = Typeface.Parser };
            var instance = (Font)MemberwiseClone();
            instance.Typeface = typeface;
            instance.baseFont = this;
            instance.coordinates = normalized;
            instance.colorPaintCache = new ConcurrentDictionary<ulong, ColorPaintOperation[]>();
            instance.Variations = values;
            instance.Weight = values.FirstOrDefault(v => v.Tag == "wght") is { Tag: not null } weight
                ? new FontWeight(Math.Max(1, Math.Min(1000, (int)Math.Round(weight.Value))))
                : Weight;
            instance.Stretch = values.FirstOrDefault(v => v.Tag == "wdth") is { Tag: not null, Value: > 0 } width
                ? new FontStretch(width.Value)
                : Stretch;
            instance.variedAdvances = Enumerable.Repeat(-1, glyphs.Length).ToArray();
            instance.instances = new Dictionary<string, Font>();

            for (var i = 0; i < glyphs.Length; i++)
            {
                var original = baseGlyphs[i];
                var glyph = original.IsEmpty ? Glyph.EmptyGlyph(original.Index) : new Glyph(original.Index, original.OutlineType);
                glyph.Name = original.Name;
                glyph.SID = original.SID;
                glyph.ClassDefinition = original.ClassDefinition;
                glyph.AdvanceWidth = original.AdvanceWidth;
                glyph.LeftSideBearing = original.LeftSideBearing;
                glyph.AdvanceHeight = original.AdvanceHeight;
                glyph.TopSideBearing = original.TopSideBearing;
                glyph.SetUnicodes(original.Unicodes);
                if (!original.IsEmpty)
                {
                    glyph.SetOutlineSource(source);
                }

                glyphs[i] = glyph;
            }

            typeface.SetGlyphs(glyphs);
            typeface.AddFont(instance);
            typeface.SetDefaultFont();
            instance.glyphs = this.glyphs.Select(g => glyphs[g.Index]).ToList();
            instance.unicodes = new List<uint>(unicodes);
            instance.unicodeToGlyph = unicodeToGlyph.ToDictionary(p => p.Key, p => glyphs[p.Value.Index]);
            instance.nameToGlyph = nameToGlyph.ToDictionary(p => p.Key, p => glyphs[p.Value.Index]);
            return instance;
        }

        public ushort GetAdvanceWidth(uint glyphIndex)
        {
            if (baseFont != null)
            {
                return GetVariedAdvanceWidth(glyphIndex);
            }

            if (advanceWidths != null && glyphIndex < advanceWidths.Length)
            {
                return advanceWidths[glyphIndex];
            }

            return Typeface.GetGlyphByIndex(glyphIndex, out var glyph) ? glyph.AdvanceWidth : (ushort)0;
        }

        private ushort GetVariedAdvanceWidth(uint glyphIndex)
        {
            if (baseFont.MetricsVariations == null || glyphIndex >= variedAdvances.Length)
            {
                if (!Typeface.GetGlyphByIndex(glyphIndex, out var glyph))
                {
                    return 0;
                }

                _ = glyph.HasOutlines;
                return glyph.AdvanceWidth;
            }

            var advance = Volatile.Read(ref variedAdvances[glyphIndex]);
            if (advance < 0)
            {
                var delta = baseFont.MetricsVariations.GetAdvanceDelta(glyphIndex, coordinates);
                advance = Math.Max(0, baseFont.GetAdvanceWidth(glyphIndex) + (int)Math.Round(delta, MidpointRounding.AwayFromZero));
                Volatile.Write(ref variedAdvances[glyphIndex], advance);
            }

            return (ushort)advance;
        }

        public short GetLeftSideBearing(uint glyphIndex)
        {
            if (TryGetOutlineGlyph(glyphIndex, out var outlineGlyph))
            {
                return (short)outlineGlyph.BoundingRectangle.X;
            }

            if (baseFont != null)
            {
                return Typeface.GetGlyphByIndex(glyphIndex, out var varied) ? (short)varied.BoundingRectangle.X : (short)0;
            }

            if (leftSideBearings != null && glyphIndex < leftSideBearings.Length)
            {
                return leftSideBearings[glyphIndex];
            }

            return Typeface.GetGlyphByIndex(glyphIndex, out var glyph) ? glyph.LeftSideBearing : (short)0;
        }

        void IFont.UpdateGlyphNamesCache()
        {
            if (!IsGlyphNamesProvided) return;

            foreach (var glyph in glyphs)
            {
                var name = glyph.Name;
                if (nameToGlyph.ContainsKey(glyph.Name))
                {
                    name = GetUniqueName(glyph.Name);
                }

                nameToGlyph[name] = glyph;
            }
        }

        private string GetUniqueName(string originalName)
        {
            int count = 1;

            string uniqueName = originalName;
            while (nameToGlyph.ContainsKey(uniqueName))
            {
                uniqueName = $"{originalName}.{count++}";
            }

            return uniqueName;
        }

        void IFont.SetGlyphUnicodes(Dictionary<uint, List<uint>> glyphMapping)
        {
            unicodes.Clear();
            unicodeToGlyph.Clear();

            foreach (var kvp in glyphMapping)
            {
                unicodes.AddRange(kvp.Value);
                foreach (var unicode in kvp.Value)
                {
                    if (Typeface.GetGlyphByIndex(kvp.Key, out var glyph))
                    {
                        unicodeToGlyph[unicode] = glyph;
                    }
                }
            }
        }
        
        public IReadOnlyList<Glyph> TranslateIntoGlyphs(string input)
        {
            var translatedGlyphs = new List<Glyph>();
            foreach (var character in input)
            {
                var glyph = GetGlyphByCharacter(character);
                if (glyph == null)
                {
                    Typeface.GetGlyphByIndex(0U, out glyph);
                }
                // Once per character: the glyph is shared for the font's lifetime, so appending on every measure grew
                // without bound. Consumers only read the first entry.
                if (!glyph.RelatedCharacters.Contains(character))
                {
                    // Locked only on the rare first sight of a character: text is laid out from the layout thread AND
                    // from parallel arrange, and an unguarded Add on a shared List is a torn list, not a wrong number.
                    lock (glyph.RelatedCharacters)
                    {
                        if (!glyph.RelatedCharacters.Contains(character)) glyph.RelatedCharacters.Add(character);
                    }
                }

                translatedGlyphs.Add(glyph);
            }

            return translatedGlyphs;
        }

        public Glyph GetGlyphByIndex(uint index)
        {
            if (!Typeface.GetGlyphByIndex(index, out var glyph) && !TryGetOutlineGlyph(index, out glyph))
            {
                Typeface.GetGlyphByIndex(0, out glyph);
            }

            return glyph;
        }

        public Glyph GetGlyphByName(string name)
        {
            if (!nameToGlyph.TryGetValue(name, out var glyph))
            {
                return null;
            }

            return glyph;
        }

        public Glyph GetGlyphByUnicode(uint unicode)
        {
            if (!unicodeToGlyph.TryGetValue(unicode, out var glyph))
            {
                return glyphs[0];
            }

            return glyph;
        }

        public Glyph GetGlyphByCharacter(char character)
        {
            return GetGlyphByUnicode(character);
        }

        public Int16 GetKerningValue(UInt16 leftGlyphIndex, UInt16 rightGlyphIndex)
        {
            if (KerningData == null)
            {
                return 0;
            }

            Int16 kerningValue = 0;

            UInt32 key = SfntParser.GenerateKerningKey(leftGlyphIndex, rightGlyphIndex);

            foreach (var data in KerningData)
            {
                if (!data.KerningValues.ContainsKey(key)) continue;

                kerningValue = data.KerningValues[key];
                break;
            }

            return kerningValue;
        }
    }
}