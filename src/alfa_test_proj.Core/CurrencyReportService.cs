using alfa_test_proj.Core.Caching;
using alfa_test_proj.Core.Configuration;
using alfa_test_proj.Core.Email;
using alfa_test_proj.Core.Errors;
using alfa_test_proj.Core.Llm;
using alfa_test_proj.Core.Loading;
using alfa_test_proj.Core.Models;
using alfa_test_proj.Core.Processing;
using alfa_test_proj.Core.Reporting;
using Microsoft.Extensions.Logging;

namespace alfa_test_proj.Core;

internal sealed class CurrencyReportService : ICurrencyReportService
{
    internal static readonly string[] HistoryCurrencies = ["USD", "EUR"];

    internal const int HistoryPoints = 7;

    private const int HistoryLookbackDays = 20;

    private readonly ExchangeRateLoader _loader;
    private readonly RatesCache _cache;
    private readonly PdfReportGenerator _pdfGenerator;
    private readonly SmtpReportSender _sender;
    private readonly LlmCommentGenerator _commentGenerator;
    private readonly AppSettingsLoader _settingsLoader;
    private readonly CoreOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CurrencyReportService> _logger;

    public CurrencyReportService(
        ExchangeRateLoader loader,
        RatesCache cache,
        PdfReportGenerator pdfGenerator,
        SmtpReportSender sender,
        LlmCommentGenerator commentGenerator,
        AppSettingsLoader settingsLoader,
        CoreOptions options,
        TimeProvider timeProvider,
        ILogger<CurrencyReportService> logger)
    {
        _loader = loader;
        _cache = cache;
        _pdfGenerator = pdfGenerator;
        _sender = sender;
        _commentGenerator = commentGenerator;
        _settingsLoader = settingsLoader;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<RatesReport> LoadRatesAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        if (date > today)
        {
            throw new CurrencyReportException(
                $"Нельзя выбрать дату позже сегодняшней ({Format.Date(today)}).");
        }

        _logger.LogInformation("Загрузка курсов на {Date:dd.MM.yyyy}", date);

        RatesDataSet data;
        try
        {
            data = await FetchAsync(date, cancellationToken);
        }
        catch (RatesSourceUnavailableException ex)
        {
            var cached = await _cache.TryLoadAsync(date, cancellationToken);
            if (cached is null)
            {
                throw new RatesSourceUnavailableException(
                    ex.Message + " Сохранённых данных на эту дату нет.", ex.InnerException);
            }

            _logger.LogWarning("Сайт ЦБ недоступен - используются данные из кэша на {Date:dd.MM.yyyy}", date);
            return BuildReport(cached, fromCache: true);
        }

        await _cache.SaveAsync(data, cancellationToken);
        return BuildReport(data, fromCache: false);
    }

    public IReadOnlyList<CachedRatesInfo> GetCachedEntries() => _cache.List();

    public async Task<RatesReport> LoadFromCacheAsync(DateOnly requestedDate,
        CancellationToken cancellationToken = default)
    {
        var cached = await _cache.TryLoadAsync(requestedDate, cancellationToken)
                     ?? throw new CurrencyReportException(
                         $"В кэше нет данных на {Format.Date(requestedDate)}. Загрузите их при наличии сети.");
        return BuildReport(cached, fromCache: true);
    }

    public async Task<string> SavePdfAsync(RatesReport report, IProgress<ReportStage>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(ReportStage.BuildingPdf);
        var pdf = await Task.Run(() => _pdfGenerator.Generate(report), cancellationToken);

        progress?.Report(ReportStage.SavingFile);
        var fileName = $"cbr-rates-{report.RatesDate:yyyy-MM-dd}_{_timeProvider.GetLocalNow():HHmmss}.pdf";
        var path = Path.Combine(_options.ReportsDirectory, fileName);
        try
        {
            Directory.CreateDirectory(_options.ReportsDirectory);
            await File.WriteAllBytesAsync(path, pdf, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Не удалось сохранить PDF в {Path}", path);
            throw new ReportGenerationException($"Не удалось сохранить PDF-файл в {path}: {ex.Message}", ex);
        }

        _logger.LogInformation("PDF-отчёт сохранён: {Path}", path);
        return path;
    }

    public async Task<ReportSendResult> SendReportAsync(RatesReport report, string recipient,
        IProgress<ReportStage>? progress = null, CancellationToken cancellationToken = default)
    {
        // Сначала дешёвые проверки, чтобы не формировать PDF зря
        if (!EmailAddressValidator.TryNormalize(recipient, out var address, out var error))
        {
            _logger.LogWarning("Некорректный адрес получателя: {Recipient}", recipient);
            throw new InvalidRecipientException(error);
        }

        var smtp = _settingsLoader.LoadSmtpSettings();

        progress?.Report(ReportStage.BuildingPdf);
        var pdf = await Task.Run(() => _pdfGenerator.Generate(report), cancellationToken);

        string? comment = null;
        var llm = _settingsLoader.TryLoadLlmSettings();
        if (llm is not null)
        {
            progress?.Report(ReportStage.GeneratingComment);
            comment = await _commentGenerator.TryGenerateAsync(report, llm, cancellationToken);
        }

        progress?.Report(ReportStage.Sending);
        var message = ReportEmailComposer.Compose(report, smtp, address, pdf, comment);
        await _sender.SendAsync(smtp, message, cancellationToken);

        return new ReportSendResult(address, comment);
    }

    public string? ValidateRecipient(string? recipient) =>
        EmailAddressValidator.TryNormalize(recipient, out _, out var error) ? null : error;

    public ConfigurationStatus CheckConfiguration()
    {
        try
        {
            var smtp = _settingsLoader.LoadSmtpSettings();
            var llm = _settingsLoader.TryLoadLlmSettings();
            var message = $"SMTP: {smtp.Host}:{smtp.Port}, отправитель {smtp.FromAddress}" +
                          (llm is null ? string.Empty : $"; комментарий ИИ: {llm.Model}");
            return new ConfigurationStatus(true, message, _settingsLoader.ConfigPath);
        }
        catch (ConfigurationException ex)
        {
            return new ConfigurationStatus(false, ex.Message, _settingsLoader.ConfigPath);
        }
    }

    private async Task<RatesDataSet> FetchAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var current = await _loader.GetDailyRatesAsync(date, cancellationToken);
        var previous = await _loader.GetPreviousAvailableRatesAsync(current, cancellationToken);
        var history = await LoadHistoryAsync(current, cancellationToken);
        return new RatesDataSet(date, current, previous, history, _timeProvider.GetLocalNow());
    }

    private async Task<IReadOnlyList<RateHistory>> LoadHistoryAsync(RatesSnapshot current,
        CancellationToken cancellationToken)
    {
        var result = new List<RateHistory>();
        foreach (var code in HistoryCurrencies)
        {
            var rate = current.Rates.FirstOrDefault(r => r.CharCode == code);
            if (rate is null || string.IsNullOrEmpty(rate.Id))
            {
                _logger.LogWarning("Нет данных для графика {Code}: валюта отсутствует в ответе ЦБ", code);
                continue;
            }

            try
            {
                var points = await _loader.GetHistoryAsync(rate.Id, current.Date.AddDays(-HistoryLookbackDays),
                    current.Date, cancellationToken);
                var lastPoints = points
                    .Where(p => p.Date <= current.Date)
                    .TakeLast(HistoryPoints)
                    .ToList();
                result.Add(new RateHistory(code, rate.Name, lastPoints));
            }
            catch (CurrencyReportException ex)
            {
                _logger.LogWarning(ex, "Не удалось загрузить динамику {Code} - график будет пропущен", code);
            }
        }

        return result;
    }

    private static RatesReport BuildReport(RatesDataSet data, bool fromCache)
    {
        var changes = RatesAnalyzer.CalculateChanges(data.Current, data.Previous);
        return new RatesReport
        {
            RequestedDate = data.RequestedDate,
            RatesDate = data.Current.Date,
            PreviousRatesDate = data.Previous.Date,
            Rates = changes,
            Summary = RatesAnalyzer.Summarize(changes),
            History = data.History,
            LoadedAt = data.LoadedAt,
            IsFromCache = fromCache,
        };
    }
}
