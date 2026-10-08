using System.Globalization;
using System.Linq;
using Adamantium.Mathematics.Svg;
using NUnit.Framework;

namespace Adamantium.MathTests;

public class SvgPathDataTests
{
    private static string Read(string data) =>
        string.Join(" ", SvgPathData.Parse(data).Select(c =>
            c.Letter + string.Join(",", c.Arguments.Select(a => a.ToString(CultureInfo.InvariantCulture)))));

    [TestCase("M1,2L3 4", "M1,2 L3,4")]
    [TestCase("m1-2.5.5 1", "m1,-2.5,0.5,1")]
    [TestCase("M0 0 1 1 2 2", "M0,0,1,1,2,2")]
    [TestCase("M1e2-1E-1", "M100,-0.1")]
    [TestCase("h5v-5z", "h5 v-5 z")]
    [TestCase("C1 2 3 4 5 6 7 8 9 10 11 12", "C1,2,3,4,5,6,7,8,9,10,11,12")]
    public void ReadsCommandsAndNumbersAsSvgWritesThem(string data, string expected)
    {
        Assert.That(Read(data), Is.EqualTo(expected));
    }

    [Test]
    public void AnArcsFlags_AreOneDigitEach()
    {
        Assert.That(Read("a1 1 0 11 5 5"), Is.EqualTo("a1,1,0,1,1,5,5"));
        Assert.That(Read("A2,2,30,0,1,-3-4"), Is.EqualTo("A2,2,30,0,1,-3,-4"));
    }

    [Test]
    public void ReadingStops_AtTheFirstError()
    {
        Assert.That(Read("M1 2 L3 4 X 5 6 L7 8"), Is.EqualTo("M1,2 L3,4"));
        Assert.That(Read("M1 2 L3"), Is.EqualTo("M1,2"));
    }

    [Test]
    public void Numbers_ReadAsATransformWritesThem()
    {
        Assert.That(SvgPathData.ReadNumbers("0.125 0 .045,.999-1e1"), Is.EqualTo(new[] { 0.125, 0, 0.045, 0.999, -10 }));
    }
}
