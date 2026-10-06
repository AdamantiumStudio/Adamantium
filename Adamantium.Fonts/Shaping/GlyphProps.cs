namespace Adamantium.Fonts.Shaping;

internal static class GlyphProps
{
    public const ushort BaseGlyph = 0x02;
    public const ushort Ligature = 0x04;
    public const ushort Mark = 0x08;
    public const ushort ClassMask = BaseGlyph | Ligature | Mark;
    public const ushort Substituted = 0x10;
    public const ushort Ligated = 0x20;
    public const ushort Multiplied = 0x40;
    public const ushort Preserve = Substituted | Ligated | Multiplied;
    public const ushort MarkAttachmentTypeMask = 0xFF00;
}
