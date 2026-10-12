using System.Net;
using alfa_test_proj.Core.Errors;
using alfa_test_proj.Core.Loading;
using Microsoft.Extensions.Logging.Abstractions;

namespace alfa_test_proj.Core.Tests;

public class ExchangeRateLoaderTests
{
    private static ExchangeRateLoader CreateLoader(FakeHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri(ExchangeRateLoader.DefaultBaseUrl) },
            NullLogger<ExchangeRateLoader>.Instance);

    [Fact]
    public async Task GetDailyRates_SendsDateInDdMmYyyyWithSlashes()
    {
        var handler = new FakeHandler(_ => FakeHandler.Xml(TestData.Daily("09.10.2026", ("USD", 1, "80,0"))));

        await CreateLoader(handler).GetDailyRatesAsync(new DateOnly(2026, 10, 9));

        Assert.Equal("https://www.cbr.ru/scripts/XML_daily.asp?date_req=09/10/2026", handler.Requests[0].ToString());
    }

    [Fact]
    public async Task GetPreviousAvailableRates_UsesActualValCursDate()
    {
        // Понедельник 12.10: ЦБ отдаёт курсы субботы 10.10. Предыдущий день — пятница 09.10,
        // а не воскресенье 11.10 по календарю.
        var handler = new FakeHandler(request => FakeHandler.Query(request, "date_req") switch
        {
            "12/10/2026" or "11/10/2026" or "10/10/2026" => FakeHandler.Xml(TestData.Daily("10.10.2026", ("USD", 1, "81,0"))),
            "09/10/2026" => FakeHandler.Xml(TestData.Daily("09.10.2026", ("USD", 1, "80,0"))),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
        var loader = CreateLoader(handler);

        var current = await loader.GetDailyRatesAsync(new DateOnly(2026, 10, 12));
        var previous = await loader.GetPreviousAvailableRatesAsync(current);

        Assert.Equal(new DateOnly(2026, 10, 10), current.Date);
        Assert.Equal(new DateOnly(2026, 10, 9), previous.Date);
    }

    [Fact]
    public async Task GetPreviousAvailableRates_SkipsDaysWithSameDate()
    {
        // Сервис для 09.10 почему-то вернул ту же дату 10.10 — шагаем дальше назад.
        var handler = new FakeHandler(request => FakeHandler.Query(request, "date_req") switch
        {
            "09/10/2026" => FakeHandler.Xml(TestData.Daily("10.10.2026", ("USD", 1, "81,0"))),
            "08/10/2026" => FakeHandler.Xml(TestData.Daily("08.10.2026", ("USD", 1, "79,0"))),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound),
        });
        var current = TestData.Snapshot(new DateOnly(2026, 10, 10), ("USD", 81m));

        var previous = await CreateLoader(handler).GetPreviousAvailableRatesAsync(current);

        Assert.Equal(new DateOnly(2026, 10, 8), previous.Date);
    }

    [Fact]
    public async Task NetworkError_IsTranslatedToRatesSourceUnavailable()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("No such host is known"));

        var ex = await Assert.ThrowsAsync<RatesSourceUnavailableException>(() =>
            CreateLoader(handler).GetDailyRatesAsync(new DateOnly(2026, 10, 9)));

        Assert.Contains("подключение к интернету", ex.Message);
    }

    [Fact]
    public async Task HttpError_IsTranslatedToRatesSourceUnavailable()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var ex = await Assert.ThrowsAsync<RatesSourceUnavailableException>(() =>
            CreateLoader(handler).GetDailyRatesAsync(new DateOnly(2026, 10, 9)));

        Assert.Contains("503", ex.Message);
    }

    [Fact]
    public async Task Timeout_IsTranslatedToRatesSourceUnavailable()
    {
        var handler = new FakeHandler(_ => throw new TaskCanceledException("timeout"));

        await Assert.ThrowsAsync<RatesSourceUnavailableException>(() =>
            CreateLoader(handler).GetDailyRatesAsync(new DateOnly(2026, 10, 9)));
    }
}
