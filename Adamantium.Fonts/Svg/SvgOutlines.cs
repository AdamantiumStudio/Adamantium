using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using Adamantium.Fonts.Common;
using Adamantium.Mathematics;
using Adamantium.Mathematics.Svg;

namespace Adamantium.Fonts.Svg;

internal static class SvgOutlines
{
    private const double MaxArcPiece = Math.PI / 4;

    public static List<List<OutlinePoint>> Of(XElement element)
    {
        var contours = Shape(element);
        foreach (var contour in contours)
        {
            EndOnCurve(contour);
        }

        return contours;
    }

    private static List<List<OutlinePoint>> Shape(XElement element)
    {
        switch (element.Name.LocalName)
        {
            case "path":
                return Path((string)element.Attribute("d"));
            case "rect":
                return Rect(Number(element, "x"), Number(element, "y"), Number(element, "width"),
                    Number(element, "height"), Optional(element, "rx"), Optional(element, "ry"));
            case "circle":
                var r = Number(element, "r");
                return Ellipse(Number(element, "cx"), Number(element, "cy"), r, r);
            case "ellipse":
                return Ellipse(Number(element, "cx"), Number(element, "cy"), Number(element, "rx"), Number(element, "ry"));
            case "polygon":
            case "polyline":
                return Polygon(SvgPathData.ReadNumbers((string)element.Attribute("points")));
            default:
                return [];
        }
    }

    public static List<List<OutlinePoint>> Path(string data)
    {
        var contours = new List<List<OutlinePoint>>();
        List<OutlinePoint> contour = null;
        var current = Vector2.Zero;
        var start = Vector2.Zero;
        var lastCubic = Vector2.Zero;
        var lastQuadratic = Vector2.Zero;
        var previous = ' ';
        foreach (var command in SvgPathData.Parse(data))
        {
            var letter = char.ToUpperInvariant(command.Letter);
            var relative = char.IsLower(command.Letter);
            var a = command.Arguments;
            if (letter == 'Z')
            {
                Close(contours, ref contour);
                current = start;
                previous = 'Z';
                continue;
            }

            var step = a.Count == 0 ? 0 : Step(letter);
            for (var at = 0; step > 0 && at + step <= a.Count; at += step)
            {
                var origin = relative ? current : Vector2.Zero;
                switch (letter)
                {
                    case 'M' when at == 0:
                        Close(contours, ref contour);
                        current = start = origin + new Vector2(a[at], a[at + 1]);
                        contour = [new OutlinePoint(current)];
                        break;
                    case 'M':
                    case 'L':
                        current = LineTo(ref contour, ref start, current, origin + new Vector2(a[at], a[at + 1]));
                        break;
                    case 'H':
                        current = LineTo(ref contour, ref start, current, new Vector2((relative ? current.X : 0) + a[at], current.Y));
                        break;
                    case 'V':
                        current = LineTo(ref contour, ref start, current, new Vector2(current.X, (relative ? current.Y : 0) + a[at]));
                        break;
                    case 'C':
                        lastCubic = origin + new Vector2(a[at + 2], a[at + 3]);
                        current = CubicTo(ref contour, ref start, current, origin + new Vector2(a[at], a[at + 1]), lastCubic,
                            origin + new Vector2(a[at + 4], a[at + 5]));
                        break;
                    case 'S':
                    {
                        var first = previous is 'C' or 'S' ? current * 2 - lastCubic : current;
                        lastCubic = origin + new Vector2(a[at], a[at + 1]);
                        current = CubicTo(ref contour, ref start, current, first, lastCubic, origin + new Vector2(a[at + 2], a[at + 3]));
                        break;
                    }
                    case 'Q':
                        lastQuadratic = origin + new Vector2(a[at], a[at + 1]);
                        current = QuadraticTo(ref contour, ref start, current, lastQuadratic, origin + new Vector2(a[at + 2], a[at + 3]));
                        break;
                    case 'T':
                        lastQuadratic = previous is 'Q' or 'T' ? current * 2 - lastQuadratic : current;
                        current = QuadraticTo(ref contour, ref start, current, lastQuadratic, origin + new Vector2(a[at], a[at + 1]));
                        break;
                    case 'A':
                        current = ArcTo(ref contour, ref start, current, a[at], a[at + 1], a[at + 2], a[at + 3] != 0,
                            a[at + 4] != 0, origin + new Vector2(a[at + 5], a[at + 6]));
                        break;
                }

                previous = letter == 'M' ? 'L' : letter;
            }

            if (letter == 'M')
            {
                previous = 'M';
            }
        }

        Close(contours, ref contour);
        return contours;
    }

    private static int Step(char letter)
    {
        return letter switch
        {
            'H' or 'V' => 1,
            'M' or 'L' or 'T' => 2,
            'S' or 'Q' => 4,
            'C' => 6,
            'A' => 7,
            _ => 0,
        };
    }

    private static void Close(List<List<OutlinePoint>> contours, ref List<OutlinePoint> contour)
    {
        if (contour == null)
        {
            return;
        }

        if (contour.Count > 2)
        {
            contours.Add(contour);
        }

        contour = null;
    }

    private static void EndOnCurve(List<OutlinePoint> contour)
    {
        var last = contour[contour.Count - 1];
        if (last.IsControl)
        {
            contour.Add(new OutlinePoint(contour[0].X, contour[0].Y));
        }
        else if (contour.Count > 2 && !contour[contour.Count - 2].IsControl && (Vector2)last == (Vector2)contour[0])
        {
            contour.RemoveAt(contour.Count - 1);
        }
    }

    private static List<OutlinePoint> Begin(ref List<OutlinePoint> contour, ref Vector2 start, Vector2 current)
    {
        if (contour == null)
        {
            contour = [new OutlinePoint(current)];
            start = current;
        }

        return contour;
    }

    private static Vector2 LineTo(ref List<OutlinePoint> contour, ref Vector2 start, Vector2 current, Vector2 end)
    {
        Begin(ref contour, ref start, current).Add(new OutlinePoint(end));
        return end;
    }

    private static Vector2 CubicTo(ref List<OutlinePoint> contour, ref Vector2 start, Vector2 current, Vector2 first,
        Vector2 second, Vector2 end)
    {
        var points = Begin(ref contour, ref start, current);
        points.Add(new OutlinePoint(first, true));
        points.Add(new OutlinePoint(second, true));
        points.Add(new OutlinePoint(end));
        return end;
    }

    private static Vector2 QuadraticTo(ref List<OutlinePoint> contour, ref Vector2 start, Vector2 current, Vector2 control,
        Vector2 end)
    {
        return CubicTo(ref contour, ref start, current, current + (control - current) * (2.0 / 3),
            end + (control - end) * (2.0 / 3), end);
    }

    private static Vector2 ArcTo(ref List<OutlinePoint> contour, ref Vector2 start, Vector2 current, double rx, double ry,
        double rotation, bool largeArc, bool sweep, Vector2 end)
    {
        rx = Math.Abs(rx);
        ry = Math.Abs(ry);
        if (current == end)
        {
            return end;
        }

        if (rx < 1e-12 || ry < 1e-12)
        {
            return LineTo(ref contour, ref start, current, end);
        }

        var phi = rotation * Math.PI / 180;
        var cos = Math.Cos(phi);
        var sin = Math.Sin(phi);
        var dx = (current.X - end.X) / 2;
        var dy = (current.Y - end.Y) / 2;
        var x1 = cos * dx + sin * dy;
        var y1 = -sin * dx + cos * dy;
        var lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
        if (lambda > 1)
        {
            rx *= Math.Sqrt(lambda);
            ry *= Math.Sqrt(lambda);
        }

        var numerator = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
        var denominator = rx * rx * y1 * y1 + ry * ry * x1 * x1;
        var factor = Math.Sqrt(Math.Max(0, numerator / denominator)) * (largeArc == sweep ? -1 : 1);
        var cx1 = factor * rx * y1 / ry;
        var cy1 = -factor * ry * x1 / rx;
        var cx = cos * cx1 - sin * cy1 + (current.X + end.X) / 2;
        var cy = sin * cx1 + cos * cy1 + (current.Y + end.Y) / 2;
        var theta = Angle(1, 0, (x1 - cx1) / rx, (y1 - cy1) / ry);
        var delta = Angle((x1 - cx1) / rx, (y1 - cy1) / ry, (-x1 - cx1) / rx, (-y1 - cy1) / ry);
        if (!sweep && delta > 0)
        {
            delta -= 2 * Math.PI;
        }
        else if (sweep && delta < 0)
        {
            delta += 2 * Math.PI;
        }

        var pieces = (int)Math.Ceiling(Math.Abs(delta) / MaxArcPiece - 1e-9);
        var piece = delta / Math.Max(1, pieces);
        var handle = 4.0 / 3 * Math.Tan(piece / 4);
        for (var i = 0; i < pieces; i++)
        {
            var a0 = theta + i * piece;
            var a1 = a0 + piece;
            var p0 = Point(cx, cy, rx, ry, cos, sin, a0);
            var p3 = i == pieces - 1 ? end : Point(cx, cy, rx, ry, cos, sin, a1);
            var d0 = Derivative(rx, ry, cos, sin, a0);
            var d1 = Derivative(rx, ry, cos, sin, a1);
            current = CubicTo(ref contour, ref start, current, p0 + d0 * handle, p3 - d1 * handle, p3);
        }

        return end;
    }

    private static double Angle(double ux, double uy, double vx, double vy)
    {
        return Math.Atan2(ux * vy - uy * vx, ux * vx + uy * vy);
    }

    private static Vector2 Point(double cx, double cy, double rx, double ry, double cos, double sin, double angle)
    {
        var x = rx * Math.Cos(angle);
        var y = ry * Math.Sin(angle);
        return new Vector2(cx + cos * x - sin * y, cy + sin * x + cos * y);
    }

    private static Vector2 Derivative(double rx, double ry, double cos, double sin, double angle)
    {
        var x = -rx * Math.Sin(angle);
        var y = ry * Math.Cos(angle);
        return new Vector2(cos * x - sin * y, sin * x + cos * y);
    }

    private static List<List<OutlinePoint>> Rect(double x, double y, double width, double height, double? rx, double? ry)
    {
        if (width <= 0 || height <= 0)
        {
            return [];
        }

        var radiusX = Math.Min(Math.Max(0, rx ?? ry ?? 0), width / 2);
        var radiusY = Math.Min(Math.Max(0, ry ?? rx ?? 0), height / 2);
        if (radiusX <= 0 || radiusY <= 0)
        {
            return
            [
                [
                    new OutlinePoint(x, y), new OutlinePoint(x + width, y), new OutlinePoint(x + width, y + height),
                    new OutlinePoint(x, y + height),
                ],
            ];
        }

        var right = x + width;
        var bottom = y + height;
        List<OutlinePoint> contour = [new OutlinePoint(x + radiusX, y)];
        var start = new Vector2(x + radiusX, y);
        var current = LineTo(ref contour, ref start, start, new Vector2(right - radiusX, y));
        current = ArcTo(ref contour, ref start, current, radiusX, radiusY, 0, false, true, new Vector2(right, y + radiusY));
        current = LineTo(ref contour, ref start, current, new Vector2(right, bottom - radiusY));
        current = ArcTo(ref contour, ref start, current, radiusX, radiusY, 0, false, true, new Vector2(right - radiusX, bottom));
        current = LineTo(ref contour, ref start, current, new Vector2(x + radiusX, bottom));
        current = ArcTo(ref contour, ref start, current, radiusX, radiusY, 0, false, true, new Vector2(x, bottom - radiusY));
        current = LineTo(ref contour, ref start, current, new Vector2(x, y + radiusY));
        ArcTo(ref contour, ref start, current, radiusX, radiusY, 0, false, true, new Vector2(x + radiusX, y));
        return [contour];
    }

    private static List<List<OutlinePoint>> Ellipse(double cx, double cy, double rx, double ry)
    {
        if (rx <= 0 || ry <= 0)
        {
            return [];
        }

        List<OutlinePoint> contour = [new OutlinePoint(cx + rx, cy)];
        var start = new Vector2(cx + rx, cy);
        var current = ArcTo(ref contour, ref start, start, rx, ry, 0, true, true, new Vector2(cx - rx, cy));
        ArcTo(ref contour, ref start, current, rx, ry, 0, true, true, new Vector2(cx + rx, cy));
        return [contour];
    }

    private static List<List<OutlinePoint>> Polygon(List<double> numbers)
    {
        var contour = new List<OutlinePoint>();
        for (var i = 0; i + 1 < numbers.Count; i += 2)
        {
            contour.Add(new OutlinePoint(numbers[i], numbers[i + 1]));
        }

        return contour.Count > 2 ? [contour] : [];
    }

    private static double Number(XElement element, string name) => Optional(element, name) ?? 0;

    private static double? Optional(XElement element, string name)
    {
        var text = ((string)element.Attribute(name))?.Trim();
        if (string.IsNullOrEmpty(text) || text == "auto")
        {
            return null;
        }

        if (text.EndsWith("px", StringComparison.Ordinal))
        {
            text = text.Substring(0, text.Length - 2);
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }
}
