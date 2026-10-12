namespace alfa_test_proj.Core.Models;

public sealed record ReportSendResult(string Recipient, string? AiComment);

public sealed record CachedRatesInfo(DateOnly RequestedDate, DateOnly RatesDate, DateTimeOffset LoadedAt);

public sealed record ConfigurationStatus(bool IsValid, string Message, string ConfigPath);
