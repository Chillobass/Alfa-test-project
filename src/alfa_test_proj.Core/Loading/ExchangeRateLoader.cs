using System.Globalization;
using alfa_test_proj.Core.Errors;
using alfa_test_proj.Core.Models;
using Microsoft.Extensions.Logging;

namespace alfa_test_proj.Core.Loading;

internal sealed class ExchangeRateLoader
{
    public const string DefaultBaseUrl = "https://www.cbr.ru/scripts/";

    private const string DailyResource = "XML_daily.asp";
    private const string DynamicResource = "XML_dynamic.asp";

    private const int MaxPreviousDaySteps = 14;

    private readonly HttpClient _client;
    private readonly ILogger<ExchangeRateLoader> _logger;

    public ExchangeRateLoader(HttpClient httpClient, ILogger<ExchangeRateLoader> logger)
    {
        _client = httpClient;
        _logger = logger;
    }

    public async Task<RatesSnapshot> GetDailyRatesAsync(DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        var url = DailyResource;
        if (date.HasValue)
        {
            // по идее можно использовать и CultureInfo.GetCultureInfo("ru-RU"), но InvariantCulture стабильнее
            url += $"?date_req={date.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";
        }

        var content = await DownloadAsync(url, cancellationToken);
        return CbrXmlParser.ParseDaily(content, _logger);
    }

    public async Task<RatesSnapshot> GetPreviousAvailableRatesAsync(RatesSnapshot current,
        CancellationToken cancellationToken = default)
    {
        var candidate = current.Date.AddDays(-1);
        for (var step = 0; step < MaxPreviousDaySteps; step++)
        {
            var snapshot = await GetDailyRatesAsync(candidate, cancellationToken);
            if (snapshot.Date < current.Date)
            {
                _logger.LogInformation("Предыдущий доступный день для {Current:dd.MM.yyyy}: {Previous:dd.MM.yyyy}",
                    current.Date, snapshot.Date);
                return snapshot;
            }

            // ЦБ вернул ту же (или более позднюю) дату - значит на candidate действовали те же курсы.
            candidate = candidate.AddDays(-1);
        }

        throw new InvalidRatesResponseException(
            $"Не удалось найти курсы ЦБ РФ за день, предшествующий {current.Date:dd.MM.yyyy}.");
    }

    public async Task<IReadOnlyList<RatePoint>> GetHistoryAsync(string currencyId, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var url = $"{DynamicResource}?date_req1={from.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}" +
                  $"&date_req2={to.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}" +
                  $"&VAL_NM_RQ={Uri.EscapeDataString(currencyId)}";

        var content = await DownloadAsync(url, cancellationToken);
        return CbrXmlParser.ParseDynamic(content, _logger);
    }

    private async Task<byte[]> DownloadAsync(string url, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Запрос к ЦБ РФ: {Url}", new Uri(_client.BaseAddress!, url));
        try
        {
            return await _client.GetByteArrayAsync(url, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is not null)
        {
            _logger.LogError(ex, "ЦБ РФ ответил HTTP {StatusCode} на запрос {Url}", (int)ex.StatusCode, url);
            throw new RatesSourceUnavailableException(
                $"Сайт ЦБ РФ временно недоступен (код ответа {(int)ex.StatusCode}). Попробуйте позже.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Ошибка сети при запросе {Url}", url);
            throw new RatesSourceUnavailableException(
                "Не удалось подключиться к сайту ЦБ РФ. Проверьте подключение к интернету.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Таймаут запроса {Url}", url);
            throw new RatesSourceUnavailableException(
                "Сайт ЦБ РФ не ответил вовремя. Проверьте подключение к интернету и попробуйте ещё раз.", ex);
        }
    }
}
