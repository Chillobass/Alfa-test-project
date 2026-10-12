namespace alfa_test_proj.Core.Models;

public sealed class CurrencyRate
{
    public string Id { get; init; } = string.Empty;

    public required string CharCode { get; init; }

    public required string Name { get; init; }

    public required decimal Value { get; init; }
}
