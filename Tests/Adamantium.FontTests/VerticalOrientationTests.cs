using Adamantium.Fonts.Text;
using NUnit.Framework;

namespace Adamantium.FontTests;

/// <summary>The Vertical_Orientation of characters (UAX #50): ideographs and kana upright, Latin turned, punctuation in
/// vertical forms of its own.</summary>
public class VerticalOrientationTests
{
    [TestCase('A', VerticalOrientation.Rotated)]
    [TestCase('1', VerticalOrientation.Rotated)]
    [TestCase('…', VerticalOrientation.Rotated)]
    [TestCase('漢', VerticalOrientation.Upright)]
    [TestCase('ᄀ', VerticalOrientation.Upright)]
    [TestCase('。', VerticalOrientation.TransformedOrUpright)]
    [TestCase('ぁ', VerticalOrientation.TransformedOrUpright)]
    [TestCase('ー', VerticalOrientation.TransformedOrRotated)]
    [TestCase('〈', VerticalOrientation.TransformedOrRotated)]
    [TestCase('（', VerticalOrientation.TransformedOrRotated)]
    public void Of_IsTheUnicodeProperty(char character, VerticalOrientation expected)
    {
        Assert.That(VerticalOrientations.Of(character), Is.EqualTo(expected));
    }

    [Test]
    public void IsUpright_TakesTheTransformedUprightAsUpright()
    {
        Assert.That(VerticalOrientations.IsUpright('。'), Is.True);
        Assert.That(VerticalOrientations.IsUpright('ー'), Is.False);
        Assert.That(VerticalOrientations.IsUpright(0x20000), Is.True, "an ideograph outside the basic plane");
    }
}
