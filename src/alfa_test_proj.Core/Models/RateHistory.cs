using System.Text.Json.Serialization;

namespace alfa_test_proj.Core.Models;

public sealed record RatePoint(DateOnly Date, decimal Value);

public sealed record RateHistory(string CharCode, string Name, IReadOnlyList<RatePoint> Points)
{
    [JsonIgnore]
    public decimal? PeriodChangePercent =>
        Points.Count >= 2 && Points[0].Value != 0
            ? (Points[^1].Value - Points[0].Value) / Points[0].Value * 100m
            : null;
}
