namespace alfa_test_proj.Core.Models;

public sealed record DailySummary(
    IReadOnlyList<CurrencyRateChange> TopGainers,
    IReadOnlyList<CurrencyRateChange> TopLosers,
    decimal? AverageChangePercent,
    int ComparedCount);
