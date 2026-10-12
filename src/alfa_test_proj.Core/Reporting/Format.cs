using System.Globalization;

namespace alfa_test_proj.Core.Reporting;

public static class Format
{
    private static readonly NumberFormatInfo RuNumbers = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = " ",
        NegativeSign = "−",
    };

    public static string Rate(decimal value) =>
        value.ToString(Math.Abs(value) < 1 ? "#,##0.000000" : "#,##0.0000", RuNumbers);

    public static string Percent(decimal? value)
    {
        if (value is null)
        {
            return "-";
        }

        var rounded = Math.Round(value.Value, 2, MidpointRounding.AwayFromZero);
        if (rounded == 0)
        {
            rounded = 0.00m;
        }

        var sign = rounded > 0 ? "+" : string.Empty;
        return $"{sign}{rounded.ToString("#,##0.00", RuNumbers)} %";
    }

    public static string Plural(int count, string one, string few, string many)
    {
        var n = Math.Abs(count) % 100;
        if (n is >= 11 and <= 14)
        {
            return many;
        }

        return (n % 10) switch
        {
            1 => one,
            2 or 3 or 4 => few,
            _ => many,
        };
    }

    public static string Date(DateOnly date) => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
}
