namespace Adamantium.Fonts.Shaping;

internal struct GlyphPlacement
{
    public const byte AttachMark = 1;
    public const byte AttachCursive = 2;

    public int XAdvance;
    public int YAdvance;
    public int XOffset;
    public int YOffset;
    public int AttachChain;
    public byte AttachType;
}
