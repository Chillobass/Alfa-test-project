using alfa_test_proj.Core.Configuration;
using alfa_test_proj.Core.Email;
using alfa_test_proj.Core.Reporting;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;

namespace alfa_test_proj.Core.Tests;

public class ReportOutputTests
{
    [Fact]
    public void Subject_ContainsRatesDate() =>
        Assert.Equal("Курсы валют ЦБ РФ на 10.10.2026", ReportEmailComposer.BuildSubject(TestData.Report()));

    [Fact]
    public void Body_ContainsTopGainersAndLosers()
    {
        var body = ReportEmailComposer.BuildBody(TestData.Report(), aiComment: null);

        Assert.Contains("Топ-3 роста", body);
        Assert.Contains("CNY", body);
        Assert.Contains("+1,82", body);
        Assert.Contains("Топ-3 падения", body);
        Assert.Contains("JPY", body);
        Assert.DoesNotContain("Комментарий", body);
    }

    [Fact]
    public void Body_IncludesAiCommentWhenPresent() =>
        Assert.Contains("Рубль укрепился.", ReportEmailComposer.BuildBody(TestData.Report(), "Рубль укрепился."));

    [Fact]
    public void Compose_AttachesPdf()
    {
        var smtp = new SmtpSettings { Host = "smtp.test", FromAddress = "from@example.com" };

        var message = ReportEmailComposer.Compose(TestData.Report(), smtp, "to@example.com", [1, 2, 3], null);

        var attachment = Assert.Single(message.Attachments.OfType<MimePart>());
        Assert.Equal("cbr-rates-2026-10-10.pdf", attachment.FileName);
        Assert.Equal("application/pdf", attachment.ContentType.MimeType);
        Assert.Equal("to@example.com", message.To.Mailboxes.Single().Address);
    }

    [Fact]
    public void Pdf_IsGenerated()
    {
        var pdf = new PdfReportGenerator(NullLogger<PdfReportGenerator>.Instance).Generate(TestData.Report());

        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void History_PeriodChangeIsComputedFromFirstAndLastPoint() =>
        Assert.Equal(2.25m, TestData.Report().History[0].PeriodChangePercent);

    [Theory]
    [InlineData(1.234, "+1,23 %")]
    [InlineData(-0.456, "−0,46 %")]
    [InlineData(-0.001, "0,00 %")]
    public void Percent_IsFormattedInRussianStyle(double value, string expected) =>
        Assert.Equal(expected, Format.Percent((decimal)value).Replace(' ', ' '));

    [Theory]
    [InlineData(81.25, "81,2500")]
    [InlineData(1234.5, "1 234,5000")]
    [InlineData(0.00308258, "0,003083")]
    public void Rate_IsFormattedWithEnoughPrecision(double value, string expected) =>
        Assert.Equal(expected, Format.Rate((decimal)value).Replace(' ', ' '));

    [Theory]
    [InlineData(1, "валюта")]
    [InlineData(3, "валюты")]
    [InlineData(11, "валют")]
    [InlineData(42, "валюты")]
    [InlineData(45, "валют")]
    public void Plural_Works(int count, string expected) =>
        Assert.Equal(expected, Format.Plural(count, "валюта", "валюты", "валют"));
}
