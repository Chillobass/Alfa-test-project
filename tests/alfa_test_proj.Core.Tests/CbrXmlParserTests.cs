using alfa_test_proj.Core.Errors;
using alfa_test_proj.Core.Loading;
using Microsoft.Extensions.Logging.Abstractions;

namespace alfa_test_proj.Core.Tests;

public class CbrXmlParserTests
{
    [Fact]
    public void ParseDaily_DividesByNominal_AndReadsCommaDecimal()
    {
        var xml = TestData.Daily("10.10.2026", ("USD", 1, "81,2500"), ("JPY", 100, "53,9000"));

        var snapshot = CbrXmlParser.ParseDaily(TestData.Cp1251(xml), NullLogger.Instance);

        Assert.Equal(new DateOnly(2026, 10, 10), snapshot.Date);
        Assert.Equal(81.25m, snapshot.Rates.Single(r => r.CharCode == "USD").Value);
        Assert.Equal(0.539m, snapshot.Rates.Single(r => r.CharCode == "JPY").Value);
        Assert.Equal("ID_USD", snapshot.Rates.Single(r => r.CharCode == "USD").Id);
    }

    [Fact]
    public void ParseDaily_DecodesWindows1251Names()
    {
        var xml = TestData.Daily("10.10.2026", ("EUR", 1, "93,4000"));

        var snapshot = CbrXmlParser.ParseDaily(TestData.Cp1251(xml), NullLogger.Instance);

        Assert.Equal("Валюта EUR", snapshot.Rates[0].Name);
    }

    [Fact]
    public void ParseDaily_SkipsBrokenCurrency()
    {
        var xml = TestData.Daily("10.10.2026", ("USD", 1, "81,25"), ("BAD", 0, "1,0"), ("XXX", 1, "abc"));

        var snapshot = CbrXmlParser.ParseDaily(TestData.Cp1251(xml), NullLogger.Instance);

        Assert.Equal("USD", Assert.Single(snapshot.Rates).CharCode);
    }

    [Fact]
    public void ParseDaily_EmptyResponse_Throws() =>
        Assert.Throws<InvalidRatesResponseException>(() => CbrXmlParser.ParseDaily([], NullLogger.Instance));

    [Fact]
    public void ParseDaily_Html_Throws() =>
        Assert.Throws<InvalidRatesResponseException>(() =>
            CbrXmlParser.ParseDaily(TestData.Cp1251("<html><body>Service unavailable</body></html>"), NullLogger.Instance));

    [Fact]
    public void ParseDaily_NotXml_Throws() =>
        Assert.Throws<InvalidRatesResponseException>(() =>
            CbrXmlParser.ParseDaily(TestData.Cp1251("Error in parameters"), NullLogger.Instance));

    [Fact]
    public void ParseDaily_NoCurrencies_Throws() =>
        Assert.Throws<InvalidRatesResponseException>(() =>
            CbrXmlParser.ParseDaily(TestData.Cp1251(TestData.Daily("01.01.1990")), NullLogger.Instance));

    [Fact]
    public void ParseDaily_NoDateAttribute_Throws() =>
        Assert.Throws<InvalidRatesResponseException>(() =>
            CbrXmlParser.ParseDaily(TestData.Cp1251("<ValCurs><Valute><CharCode>USD</CharCode><Nominal>1</Nominal><Value>1,0</Value></Valute></ValCurs>"),
                NullLogger.Instance));

    [Fact]
    public void ParseDynamic_ReturnsPointsOrderedByDate()
    {
        var xml = TestData.Dynamic(("03.10.2026", "82,0000"), ("02.10.2026", "81,5000"));

        var points = CbrXmlParser.ParseDynamic(TestData.Cp1251(xml), NullLogger.Instance);

        Assert.Equal(2, points.Count);
        Assert.Equal(new DateOnly(2026, 10, 2), points[0].Date);
        Assert.Equal(81.5m, points[0].Value);
    }
}
