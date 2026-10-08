using Adamantium.Fonts.Shaping;

namespace Adamantium.Fonts.Text;

/// <summary>Whether a grapheme is drawn as an emoji, in color, or as text, by Unicode Technical Standard #51.</summary>
public static class EmojiPresentation
{
    private const char TextSelector = (char)0xFE0E;
    private const char EmojiSelector = (char)0xFE0F;

    /// <summary>Whether the grapheme of <paramref name="text"/> from <paramref name="start"/> to <paramref name="end"/>
    /// is drawn as an emoji: a variation selector after an emoji character decides (U+FE0F emoji, U+FE0E text);
    /// without one, a character that is an emoji by default (Emoji_Presentation) is, and so is a pictograph followed by
    /// a joiner, a skin tone or a tag.</summary>
    public static bool IsEmoji(string text, int start, int end)
    {
        if (start >= end)
        {
            return false;
        }

        var first = CodepointAt(text, start, end);
        var next = start + (first > 0xFFFF ? 2 : 1);
        var byDefault = UnicodeData.IsEmojiPresentation(first);
        var pictographic = UnicodeData.IsExtendedPictographic(first);
        if (next < end && (byDefault || pictographic || IsKeycapBase(first)))
        {
            switch (text[next])
            {
                case TextSelector:
                    return false;
                case EmojiSelector:
                    return true;
            }
        }

        if (byDefault)
        {
            return true;
        }

        if (!pictographic)
        {
            return false;
        }

        for (var i = next; i < end; i++)
        {
            var codepoint = CodepointAt(text, i, end);
            if (codepoint is 0x200D or >= 0x1F3FB and <= 0x1F3FF or >= 0xE0020 and <= 0xE007F)
            {
                return true;
            }

            if (codepoint > 0xFFFF)
            {
                i++;
            }
        }

        return false;
    }

    private static bool IsKeycapBase(int codepoint)
    {
        return codepoint is >= '0' and <= '9' or '#' or '*';
    }

    private static int CodepointAt(string text, int index, int end)
    {
        return char.IsHighSurrogate(text[index]) && index + 1 < end && char.IsLowSurrogate(text[index + 1])
            ? char.ConvertToUtf32(text[index], text[index + 1])
            : text[index];
    }
}
