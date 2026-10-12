using System.Net;
using System.Text;
using alfa_test_proj.Core.Models;

namespace alfa_test_proj.Core.Tests;

internal static class TestData
{
    static TestData() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static byte[] Cp1251(string text) => Encoding.GetEncoding("windows-1251").GetBytes(text);

    public static string Daily(string date, params (string Code, int Nominal, string Value)[] rates)
    {
        var sb = new StringBuilder();
        sb.Append($"""<?xml version="1.0" encoding="windows-1251"?><ValCurs Date="{date}" name="Foreign Currency Market">""");
        foreach (var (code, nominal, value) in rates)
        {
            sb.Append($"""<Valute ID="ID_{code}"><NumCode>000</NumCode><CharCode>{code}</CharCode><Nominal>{nominal}</Nominal><Name>Валюта {code}</Name><Value>{value}</Value></Valute>""");
        }

        sb.Append("</ValCurs>");
        return sb.ToString();
    }

    public static string Dynamic(params (string Date, string Value)[] records)
    {
        var sb = new StringBuilder("""<?xml version="1.0" encoding="windows-1251"?><ValCurs ID="ID_USD" name="Foreign Currency Market Dynamic">""");
        foreach (var (date, value) in records)
        {
            sb.Append($"""<Record Date="{date}" Id="ID_USD"><Nominal>1</Nominal><Value>{value}</Value></Record>""");
        }

        sb.Append("</ValCurs>");
        return sb.ToString();
    }

    public static RatesSnapshot Snapshot(DateOnly date, params (string Code, decimal Value)[] rates) =>
        new(date, rates.Select(r => new CurrencyRate { Id = "ID_" + r.Code, CharCode = r.Code, Name = "Валюта " + r.Code, Value = r.Value }).ToList());

    public static RatesReport Report()
    {
        var changes = new List<CurrencyRateChange>
        {
            new("CHF", "Швейцарский франк", 101.5m, 100m, 1.5m),
            new("CNY", "Юань", 11.2m, 11m, 1.81818m),
            new("EUR", "Евро", 93m, 94m, -1.06383m),
            new("JPY", "Иен", 0.53m, 0.54m, -1.85185m),
            new("USD", "Доллар США", 81m, 80m, 1.25m),
        };
        return new RatesReport
        {
            RequestedDate = new DateOnly(2026, 10, 11),
            RatesDate = new DateOnly(2026, 10, 10),
            PreviousRatesDate = new DateOnly(2026, 10, 9),
            Rates = changes,
            Summary = Processing.RatesAnalyzer.Summarize(changes),
            History =
            [
                new RateHistory("USD", "Доллар США",
                    Enumerable.Range(0, 7).Select(i => new RatePoint(new DateOnly(2026, 10, 2).AddDays(i), 80 + i * 0.3m)).ToList()),
            ],
            LoadedAt = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.FromHours(3)),
        };
    }
}

internal sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!);
        return Task.FromResult(respond(request));
    }

    public static HttpResponseMessage Xml(string xml) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(TestData.Cp1251(xml)) };

    public static string Query(HttpRequestMessage request, string name) =>
        request.RequestUri!.Query.TrimStart('?').Split('&')
            .Select(p => p.Split('='))
            .First(p => p[0] == name)[1];
}
