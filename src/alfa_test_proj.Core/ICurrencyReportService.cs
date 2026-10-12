using alfa_test_proj.Core.Errors;
using alfa_test_proj.Core.Models;

namespace alfa_test_proj.Core;

public interface ICurrencyReportService
{
    Task<RatesReport> LoadRatesAsync(DateOnly date, CancellationToken cancellationToken = default);

    IReadOnlyList<CachedRatesInfo> GetCachedEntries();

    Task<RatesReport> LoadFromCacheAsync(DateOnly requestedDate, CancellationToken cancellationToken = default);

    Task<string> SavePdfAsync(RatesReport report, IProgress<ReportStage>? progress = null,
        CancellationToken cancellationToken = default);

    Task<ReportSendResult> SendReportAsync(RatesReport report, string recipient,
        IProgress<ReportStage>? progress = null, CancellationToken cancellationToken = default);

    string? ValidateRecipient(string? recipient);

    ConfigurationStatus CheckConfiguration();
}
