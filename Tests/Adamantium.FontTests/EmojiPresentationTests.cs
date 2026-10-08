using Adamantium.Fonts.Text;
using NUnit.Framework;

namespace Adamantium.FontTests;

public class EmojiPresentationTests
{
    [TestCase("\U0001F600", true, TestName = "An emoji by default")]
    [TestCase("\U00002764", false, TestName = "A pictograph that is text by default")]
    [TestCase("\U00002764\U0000FE0F", true, TestName = "VS16 asks for an emoji")]
    [TestCase("\U0001F600\U0000FE0E", false, TestName = "VS15 asks for text")]
    [TestCase("1\U0000FE0F\U000020E3", true, TestName = "A keycap")]
    [TestCase("1\U000020E3", false, TestName = "A keycap without VS16")]
    [TestCase("a\U0000FE0F", false, TestName = "VS16 after a letter")]
    [TestCase("\U0001F1FA\U0001F1E6", true, TestName = "A flag")]
    [TestCase("\U0000261D\U0001F3FD", true, TestName = "A text-default pictograph with a skin tone")]
    [TestCase("\U00002764\U0000200D\U0001F525", true, TestName = "A text-default pictograph joined to another")]
    [TestCase("\U00002764\U000E0067\U000E007F", true, TestName = "A text-default pictograph with tags")]
    [TestCase("a", false, TestName = "A letter")]
    [TestCase("\U00000915\U0000094D\U0000200D", false, TestName = "A joiner after a letter")]
    public void TheGraphemeDecidesItsPresentation(string grapheme, bool emoji)
    {
        Assert.That(EmojiPresentation.IsEmoji(grapheme, 0, grapheme.Length), Is.EqualTo(emoji));
    }

    [Test]
    public void OnlyTheGraphemeIsRead()
    {
        const string text = "\U00002764\U0000FE0F";

        Assert.That(EmojiPresentation.IsEmoji(text, 0, 1), Is.False);
    }

    [Test]
    public void AnEmptyGraphemeIsNoEmoji()
    {
        Assert.That(EmojiPresentation.IsEmoji("", 0, 0), Is.False);
    }
}
