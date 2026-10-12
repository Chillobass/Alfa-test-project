using System.Net;
using alfa_test_proj.Core.Errors;
using alfa_test_proj.Core.Loading;
using Microsoft.Extensions.DependencyInjection;

namespace alfa_test_proj.Core.Tests;

public sealed class CurrencyReportServiceTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("cbr-service-").FullName;
    private bool _offline;

    private ICurrencyReportService CreateService(out FakeHandler handler)
    {
        handler = new FakeHandler(request =>
        {
            if (_offline)
            {
                throw new HttpRequestException("network is unreachable");
            }

            if (request.RequestUri!.AbsolutePath.EndsWith("XML_dynamic.asp"))
            {
                return FakeHandler.Xml(TestData.Dynamic(("08.10.2026", "79,5"), ("09.10.2026", "80,0"), ("10.10.2026", "81,0")));
            }

            return FakeHandler.Query(request, "date_req") switch
            {
                "10/10/2026" => FakeHandler.Xml(TestData.Daily("10.10.2026", ("USD", 1, "81,0"), ("EUR", 1, "90,0"))),
                "09/10/2026" => FakeHandler.Xml(TestData.Daily("09.10.2026", ("USD", 1, "80,0"), ("EUR", 1, "91,0"))),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        });

        var fake = handler;
        var services = new ServiceCollection();
        services.AddCurrencyReportCore(o => o.DataDirectory = _directory);
        services.AddHttpClient<ExchangeRateLoader>().ConfigurePrimaryHttpMessageHandler(() => fake);
        return services.BuildServiceProvider().GetRequiredService<ICurrencyReportService>();
    }

    [Fact]
    public async Task LoadRates_ComputesReport()
    {
        var service = CreateService(out _);

        var report = await service.LoadRatesAsync(new DateOnly(2026, 10, 10));

        Assert.Equal(new DateOnly(2026, 10, 9), report.PreviousRatesDate);
        Assert.Equal(1.25m, report.Rates.Single(r => r.CharCode == "USD").ChangePercent);
        Assert.Equal("USD", Assert.Single(report.Summary.TopGainers).CharCode);
        Assert.Equal("EUR", Assert.Single(report.Summary.TopLosers).CharCode);
        Assert.Equal(2, report.History.Count);
        Assert.False(report.IsFromCache);
    }

    [Fact]
    public async Task LoadRates_FallsBackToCacheWhenOffline()
    {
        var service = CreateService(out _);
        await service.LoadRatesAsync(new DateOnly(2026, 10, 10));

        _offline = true;
        var report = await service.LoadRatesAsync(new DateOnly(2026, 10, 10));

        Assert.True(report.IsFromCache);
        Assert.Equal(2, report.Rates.Count);
        Assert.Single(service.GetCachedEntries());
    }

    [Fact]
    public async Task LoadRates_OfflineWithoutCache_Throws()
    {
        var service = CreateService(out _);
        _offline = true;

        await Assert.ThrowsAsync<RatesSourceUnavailableException>(() =>
            service.LoadRatesAsync(new DateOnly(2026, 10, 10)));
    }

    [Fact]
    public async Task LoadRates_FutureDate_Throws()
    {
        var service = CreateService(out var handler);

        await Assert.ThrowsAsync<CurrencyReportException>(() =>
            service.LoadRatesAsync(DateOnly.FromDateTime(DateTime.Today.AddDays(1))));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SavePdf_WorksOffline()
    {
        var service = CreateService(out _);
        var report = await service.LoadRatesAsync(new DateOnly(2026, 10, 10));
        _offline = true;

        var path = await service.SavePdfAsync(report);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task SendReport_InvalidRecipient_ThrowsBeforeAnythingElse()
    {
        var service = CreateService(out _);
        var report = await service.LoadRatesAsync(new DateOnly(2026, 10, 10));

        await Assert.ThrowsAsync<InvalidRecipientException>(() => service.SendReportAsync(report, "not-an-email"));
    }

    [Fact]
    public async Task SendReport_MissingConfig_ThrowsConfigurationException()
    {
        var service = CreateService(out _);
        var report = await service.LoadRatesAsync(new DateOnly(2026, 10, 10));

        await Assert.ThrowsAsync<ConfigurationException>(() => service.SendReportAsync(report, "to@example.com"));
        Assert.False(service.CheckConfiguration().IsValid);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Файловый логгер может ещё держать файл — не критично для теста.
        }
    }
}
