namespace alfa_test_proj.Core.Models;

public sealed record CurrencyRateChange(
    string CharCode,
    string Name,
    decimal Rate,
    decimal? PreviousRate,
    decimal? ChangePercent);
