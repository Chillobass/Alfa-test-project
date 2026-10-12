using alfa_test_proj.Core.Models;

namespace alfa_test_proj.Core.Processing;

internal static class RatesAnalyzer
{
    public const int TopCount = 3;

    public static IReadOnlyList<CurrencyRateChange> CalculateChanges(RatesSnapshot current, RatesSnapshot previous)
    {
        var previousByCode = previous.Rates
            .GroupBy(r => r.CharCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);

        return current.Rates
            .GroupBy(r => r.CharCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select(rate =>
            {
                decimal? previousValue = previousByCode.TryGetValue(rate.CharCode, out var p) ? p : null;
                decimal? change = previousValue is > 0
                    ? (rate.Value - previousValue.Value) / previousValue.Value * 100m
                    : null;
                return new CurrencyRateChange(rate.CharCode, rate.Name, rate.Value, previousValue, change);
            })
            .OrderBy(c => c.CharCode, StringComparer.Ordinal)
            .ToList();
    }

    public static DailySummary Summarize(IReadOnlyList<CurrencyRateChange> changes)
    {
        var comparable = changes.Where(c => c.ChangePercent.HasValue).ToList();

        var gainers = comparable
            .Where(c => c.ChangePercent > 0)
            .OrderByDescending(c => c.ChangePercent)
            .ThenBy(c => c.CharCode, StringComparer.Ordinal)
            .Take(TopCount)
            .ToList();

        var losers = comparable
            .Where(c => c.ChangePercent < 0)
            .OrderBy(c => c.ChangePercent)
            .ThenBy(c => c.CharCode, StringComparer.Ordinal)
            .Take(TopCount)
            .ToList();

        decimal? average = comparable.Count > 0
            ? comparable.Average(c => c.ChangePercent!.Value)
            : null;

        return new DailySummary(gainers, losers, average, comparable.Count);
    }
}
