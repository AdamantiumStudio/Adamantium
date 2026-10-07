using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Parsers;
using Adamantium.Fonts.Tables;
using Adamantium.Fonts.Tables.CFF;

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
        private readonly ConcurrentDictionary<uint, ColorLayer[]> colorLayerCache = new();
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

        // ------
        public FeatureCatalog FeatureCatalog { get; } = new FeatureCatalog();
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

        internal ColorPaletteTable ColorPalettes { get; set; }

        /// <inheritdoc />
        public IReadOnlyList<ColorLayer> GetColorLayers(uint glyphIndex)
        {
            if (ColorLayers == null)
            {
                return [];
            }

            return colorLayerCache.GetOrAdd(glyphIndex, g => ColorLayers.GetLayers(g, ColorPalettes, 0));
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
                return this;
            }

            var typeface = new Typeface { Parser = Typeface.Parser };
            var instance = (Font)MemberwiseClone();
            instance.Typeface = typeface;
            instance.baseFont = this;
            instance.coordinates = normalized;
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
            if (!Typeface.GetGlyphByIndex(index, out var glyph))
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