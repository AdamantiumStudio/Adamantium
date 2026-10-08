using Adamantium.Fonts;
using Adamantium.Fonts.TextureGeneration;

namespace Adamantium.Graphics.Fonts;

internal readonly record struct ColorPaintMask(Glyph Glyph, double Bearing, GlyphTextureData Cell);
