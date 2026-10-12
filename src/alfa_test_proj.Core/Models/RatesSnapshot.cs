namespace alfa_test_proj.Core.Models;

public sealed record RatesSnapshot(DateOnly Date, IReadOnlyList<CurrencyRate> Rates);
