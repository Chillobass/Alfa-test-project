using alfa_test_proj.Core.Processing;

namespace alfa_test_proj.Core.Tests;

public class RatesAnalyzerTests
{
    private static readonly DateOnly Today = new(2026, 10, 10);
    private static readonly DateOnly Yesterday = new(2026, 10, 9);

    [Fact]
    public void CalculateChanges_ComputesPercentAgainstPreviousDay()
    {
        var current = TestData.Snapshot(Today, ("USD", 82m), ("EUR", 90m));
        var previous = TestData.Snapshot(Yesterday, ("USD", 80m), ("EUR", 100m));

        var changes = RatesAnalyzer.CalculateChanges(current, previous);

        Assert.Equal(-10m, changes.Single(c => c.CharCode == "EUR").ChangePercent);
        Assert.Equal(2.5m, changes.Single(c => c.CharCode == "USD").ChangePercent);
        Assert.Equal(80m, changes.Single(c => c.CharCode == "USD").PreviousRate);
    }

    [Fact]
    public void CalculateChanges_NewCurrencyHasNoChange()
    {
        var current = TestData.Snapshot(Today, ("USD", 82m), ("AED", 22m));
        var previous = TestData.Snapshot(Yesterday, ("USD", 80m));

        var aed = RatesAnalyzer.CalculateChanges(current, previous).Single(c => c.CharCode == "AED");

        Assert.Null(aed.PreviousRate);
        Assert.Null(aed.ChangePercent);
    }

    [Fact]
    public void CalculateChanges_SortedByCode()
    {
        var current = TestData.Snapshot(Today, ("USD", 1m), ("AUD", 1m), ("EUR", 1m));

        var codes = RatesAnalyzer.CalculateChanges(current, current).Select(c => c.CharCode);

        Assert.Equal(new[] { "AUD", "EUR", "USD" }, codes);
    }

    [Fact]
    public void Summarize_PicksTopThreeGainersAndLosers()
    {
        var current = TestData.Snapshot(Today,
            ("A", 110m), ("B", 105m), ("C", 103m), ("D", 101m), ("E", 99m), ("F", 95m), ("G", 90m), ("H", 80m));
        var previous = TestData.Snapshot(Yesterday,
            ("A", 100m), ("B", 100m), ("C", 100m), ("D", 100m), ("E", 100m), ("F", 100m), ("G", 100m), ("H", 100m));

        var summary = RatesAnalyzer.Summarize(RatesAnalyzer.CalculateChanges(current, previous));

        Assert.Equal(new[] { "A", "B", "C" }, summary.TopGainers.Select(c => c.CharCode));
        Assert.Equal(new[] { "H", "G", "F" }, summary.TopLosers.Select(c => c.CharCode));
        Assert.Equal(-2.125m, summary.AverageChangePercent);
        Assert.Equal(8, summary.ComparedCount);
    }

    [Fact]
    public void Summarize_OnlyGrowingCurrenciesInTopGainers()
    {
        var current = TestData.Snapshot(Today, ("A", 101m), ("B", 100m), ("C", 99m));
        var previous = TestData.Snapshot(Yesterday, ("A", 100m), ("B", 100m), ("C", 100m));

        var summary = RatesAnalyzer.Summarize(RatesAnalyzer.CalculateChanges(current, previous));

        Assert.Equal("A", Assert.Single(summary.TopGainers).CharCode);
        Assert.Equal("C", Assert.Single(summary.TopLosers).CharCode);
    }

    [Fact]
    public void Summarize_AverageIgnoresCurrenciesWithoutPreviousRate()
    {
        var current = TestData.Snapshot(Today, ("A", 102m), ("NEW", 50m));
        var previous = TestData.Snapshot(Yesterday, ("A", 100m));

        var summary = RatesAnalyzer.Summarize(RatesAnalyzer.CalculateChanges(current, previous));

        Assert.Equal(2m, summary.AverageChangePercent);
    }
}
