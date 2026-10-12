namespace alfa_test_proj.Core.Models;

public sealed class RatesReport
{
    public required DateOnly RequestedDate { get; init; }

    public required DateOnly RatesDate { get; init; }

    public required DateOnly PreviousRatesDate { get; init; }

    public required IReadOnlyList<CurrencyRateChange> Rates { get; init; }

    public required DailySummary Summary { get; init; }

    public IReadOnlyList<RateHistory> History { get; init; } = [];

    public required DateTimeOffset LoadedAt { get; init; }

    public bool IsFromCache { get; init; }
}
