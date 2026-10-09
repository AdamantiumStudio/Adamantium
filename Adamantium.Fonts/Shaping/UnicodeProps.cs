using System.Globalization;

namespace Adamantium.Fonts.Shaping;

internal static class UnicodeProps
{
    public static void Set(ref GlyphInfo info, GlyphBuffer buffer)
    {
        var codepoint = info.Codepoint;
        info.Category = UnicodeData.GetCategory(codepoint);
        info.Flags = UnicodeFlags.None;
        info.CombiningClass = 0;
        if (codepoint < 0x80)
        {
            return;
        }

        buffer.HasNonAscii = true;
        if (UnicodeData.IsDefaultIgnorable(codepoint))
        {
            buffer.HasDefaultIgnorables = true;
            info.Flags |= UnicodeFlags.Ignorable;
            if (codepoint == 0x200C)
            {
                info.Flags |= UnicodeFlags.Zwnj;
            }
            else if (codepoint == 0x200D)
            {
                info.Flags |= UnicodeFlags.Zwj;
            }
            else if (codepoint is >= 0x180B and <= 0x180D or 0x180F or >= 0xE0020 and <= 0xE007F or 0x034F)
            {
                info.Flags |= UnicodeFlags.Hidden;
            }
        }

        if (UnicodeData.IsMark(info.Category))
        {
            info.Flags |= UnicodeFlags.Continuation;
            info.CombiningClass = UnicodeData.GetModifiedCombiningClass(codepoint);
        }
    }

    public static void SetAll(GlyphBuffer buffer)
    {
        var info = buffer.Info;
        for (var i = 0; i < buffer.Length; i++)
        {
            Set(ref info[i], buffer);
            var category = info[i].Category;
            if (category is UnicodeCategory.LowercaseLetter or UnicodeCategory.UppercaseLetter
                or UnicodeCategory.TitlecaseLetter or UnicodeCategory.OtherLetter or UnicodeCategory.SpaceSeparator)
            {
                continue;
            }

            var codepoint = info[i].Codepoint;
            if (category == UnicodeCategory.ModifierSymbol && codepoint is >= 0x1F3FB and <= 0x1F3FF)
            {
                info[i].Flags |= UnicodeFlags.Continuation;
            }
            else if (i > 0 && IsRegionalIndicator(codepoint))
            {
                if (IsRegionalIndicator(info[i - 1].Codepoint) && !info[i - 1].IsContinuation)
                {
                    info[i].Flags |= UnicodeFlags.Continuation;
                }
            }
            else if (info[i].IsZwj)
            {
                info[i].Flags |= UnicodeFlags.Continuation;
                if (i + 1 < buffer.Length && UnicodeData.IsExtendedPictographic(info[i + 1].Codepoint))
                {
                    i++;
                    Set(ref info[i], buffer);
                    info[i].Flags |= UnicodeFlags.Continuation;
                }
            }
            else if (codepoint is >= 0xFF9E and <= 0xFF9F or >= 0xE0020 and <= 0xE007F)
            {
                info[i].Flags |= UnicodeFlags.Continuation;
            }
        }
    }

    public static SpaceKind SpaceFallback(int codepoint)
    {
        switch (codepoint)
        {
            case 0x0020:
            case 0x00A0:
                return SpaceKind.Space;
            case 0x2000:
            case 0x2002:
                return SpaceKind.Em2;
            case 0x2001:
            case 0x2003:
            case 0x3000:
                return SpaceKind.Em;
            case 0x2004:
                return SpaceKind.Em3;
            case 0x2005:
                return SpaceKind.Em4;
            case 0x2006:
                return SpaceKind.Em6;
            case 0x2007:
                return SpaceKind.Figure;
            case 0x2008:
                return SpaceKind.Punctuation;
            case 0x2009:
                return SpaceKind.Em5;
            case 0x200A:
                return SpaceKind.Em16;
            case 0x202F:
                return SpaceKind.Narrow;
            case 0x205F:
                return SpaceKind.FourEm18;
            default:
                return SpaceKind.NotSpace;
        }
    }

    public static bool IsVariationSelector(int codepoint)
    {
        return codepoint is >= 0xFE00 and <= 0xFE0F or >= 0xE0100 and <= 0xE01EF or >= 0x180B and <= 0x180D or 0x180F;
    }

    private static bool IsRegionalIndicator(int codepoint) => codepoint is >= 0x1F1E6 and <= 0x1F1FF;
}
