using System.Globalization;
using System.Net;
using System.Text;
using alfa_test_proj.Core.Models;

namespace alfa_test_proj.Core.Reporting;

public static class RateChartSvg
{
    private const double Width = 520;
    private const double Height = 220;
    private const double PaddingLeft = 62;
    private const double PaddingRight = 18;
    private const double PaddingTop = 16;
    private const double PaddingBottom = 34;
    private const int GridLines = 4;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string? Render(RateHistory history, string color = "#1F4E8C", string fontFamily = "Lato")
    {
        var points = history.Points.OrderBy(p => p.Date).ToList();
        if (points.Count < 2)
        {
            return null;
        }

        var min = points.Min(p => p.Value);
        var max = points.Max(p => p.Value);

        var span = max - min;
        if (span == 0)
        {
            span = Math.Max(max * 0.005m, 0.0001m);
        }

        var yMin = (double)(min - span * 0.15m);
        var yMax = (double)(max + span * 0.15m);

        var plotWidth = Width - PaddingLeft - PaddingRight;
        var plotHeight = Height - PaddingTop - PaddingBottom;

        double X(int index) => PaddingLeft + plotWidth * index / (points.Count - 1);
        double Y(decimal value) => PaddingTop + plotHeight * (1 - ((double)value - yMin) / (yMax - yMin));

        var font = WebUtility.HtmlEncode(fontFamily);
        var svg = new StringBuilder();
        svg.Append(Invariant,
            $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {Width} {Height}" width="{Width}" height="{Height}">""");

        for (var i = 0; i <= GridLines; i++)
        {
            var value = yMin + (yMax - yMin) * i / GridLines;
            var y = PaddingTop + plotHeight * (1 - (double)i / GridLines);
            svg.Append(Invariant,
                $"""<rect x="{PaddingLeft:0.##}" y="{y - 0.5:0.##}" width="{plotWidth:0.##}" height="1" fill="#E4E7EC"/>""");
            svg.Append(Invariant,
                $"""<text x="{PaddingLeft - 8:0.##}" y="{y + 4:0.##}" font-family="{font}" font-size="12" fill="#667085" text-anchor="end">{FormatAxis(value)}</text>""");
        }

        var area = new StringBuilder();
        area.Append(Invariant, $"M {X(0):0.##} {PaddingTop + plotHeight:0.##} ");
        for (var i = 0; i < points.Count; i++)
        {
            area.Append(Invariant, $"L {X(i):0.##} {Y(points[i].Value):0.##} ");
        }

        area.Append(Invariant, $"L {X(points.Count - 1):0.##} {PaddingTop + plotHeight:0.##} Z");
        svg.Append($"""<path d="{area}" fill="{color}" fill-opacity="0.08" stroke="none"/>""");

        var line = string.Join(" ",
            points.Select((p, i) => string.Create(Invariant, $"{X(i):0.##},{Y(p.Value):0.##}")));
        svg.Append(
            $"""<polyline points="{line}" fill="none" stroke="{color}" stroke-width="2.5" stroke-linejoin="round" stroke-linecap="round"/>""");

        for (var i = 0; i < points.Count; i++)
        {
            var x = X(i);
            var y = Y(points[i].Value);
            var isLast = i == points.Count - 1;
            svg.Append(Invariant,
                $"""<circle cx="{x:0.##}" cy="{y:0.##}" r="{(isLast ? 4.5 : 3):0.#}" fill="{(isLast ? color : "#FFFFFF")}" stroke="{color}" stroke-width="2"/>""");
            svg.Append(Invariant,
                $"""<text x="{x:0.##}" y="{Height - 12:0.##}" font-family="{font}" font-size="12" fill="#667085" text-anchor="middle">{points[i].Date.ToString("dd.MM", Invariant)}</text>""");
        }

        svg.Append("</svg>");
        return svg.ToString();
    }

    private static string FormatAxis(double value) =>
        value.ToString(value >= 100 ? "0.0" : "0.00", Invariant).Replace('.', ',');
}
