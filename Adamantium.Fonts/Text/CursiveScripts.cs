using Adamantium.Fonts.Shaping;

namespace Adamantium.Fonts.Text;

/// <summary>The scripts whose letters join one another, as Arabic's do (the scripts HarfBuzz shapes with its Arabic
/// shaper): space put between their letters would break the joins.</summary>
public static class CursiveScripts
{
    /// <summary>Whether <paramref name="codepoint"/> belongs to a script whose letters join.</summary>
    public static bool Contains(int codepoint) =>
        UnicodeData.GetScript(codepoint) is "Arab" or "Syrc" or "Mong" or "Nkoo" or "Phag" or "Mand" or "Mani"
            or "Phlp" or "Adlm" or "Rohg" or "Sogd" or "Chrs" or "Ougr";
}
