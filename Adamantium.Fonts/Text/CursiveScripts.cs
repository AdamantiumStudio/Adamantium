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

    /// <summary>Whether the letter at <paramref name="index"/> of <paramref name="text"/> joins the letter after it,
    /// marks between them aside, as Arabic's joining types (ArabicShaping.txt) say.</summary>
    public static bool JoinsNext(string text, int index) => ArabicShaper.JoinsNext(text, index);

    /// <summary>The character that stretches a word of <paramref name="codepoint"/>'s script between two joined
    /// letters - the tatweel (U+0640) for Arabic, Syriac and the scripts that share it, the lajanyalan (U+07FA) for
    /// N'Ko - or 0 when the script has none.</summary>
    public static int Extender(int codepoint) => UnicodeData.GetScript(codepoint) switch
    {
        "Arab" or "Syrc" or "Mand" or "Mani" or "Phlp" or "Adlm" or "Rohg" or "Sogd" or "Ougr" => 0x0640,
        "Nkoo" => 0x07FA,
        _ => 0,
    };
}
