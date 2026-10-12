using System.Text;
using alfa_test_proj.Core.Configuration;
using alfa_test_proj.Core.Models;
using alfa_test_proj.Core.Reporting;
using MimeKit;

namespace alfa_test_proj.Core.Email;

internal static class ReportEmailComposer
{
    public static string BuildSubject(RatesReport report) =>
        $"Курсы валют ЦБ РФ на {report.RatesDate:dd.MM.yyyy}";

    public static string BuildPdfFileName(RatesReport report) =>
        $"cbr-rates-{report.RatesDate:yyyy-MM-dd}.pdf";

    public static string BuildBody(RatesReport report, string? aiComment)
    {
        var body = new StringBuilder();
        body.AppendLine($"Курсы валют ЦБ РФ на {report.RatesDate:dd.MM.yyyy} " +
                        $"(изменение относительно {report.PreviousRatesDate:dd.MM.yyyy}).");
        body.AppendLine();

        AppendTop(body, "Топ-3 роста:", report.Summary.TopGainers, "за день ни одна валюта не выросла");
        body.AppendLine();
        AppendTop(body, "Топ-3 падения:", report.Summary.TopLosers, "за день ни одна валюта не подешевела");
        body.AppendLine();

        if (report.Summary.AverageChangePercent is { } average)
        {
            body.AppendLine($"Среднее изменение по всем валютам: {Format.Percent(average)}.");
            body.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(aiComment))
        {
            body.AppendLine("Комментарий (сгенерирован языковой моделью):");
            body.AppendLine(aiComment.Trim());
            body.AppendLine();
        }

        body.AppendLine("Полный отчёт с таблицей курсов - во вложении (PDF).");
        body.AppendLine();
        body.AppendLine("--");
        body.AppendLine("Письмо сформировано автоматически приложением «Курсы валют ЦБ РФ».");
        body.AppendLine("Источник данных: Банк России, https://www.cbr.ru");
        return body.ToString();
    }

    public static MimeMessage Compose(RatesReport report, SmtpSettings smtp, string recipient, byte[] pdf,
        string? aiComment)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName ?? string.Empty, smtp.FromAddress.Trim()));
        message.To.Add(MailboxAddress.Parse(recipient));
        message.Subject = BuildSubject(report);

        var builder = new BodyBuilder { TextBody = BuildBody(report, aiComment) };
        builder.Attachments.Add(BuildPdfFileName(report), pdf, new ContentType("application", "pdf"));
        message.Body = builder.ToMessageBody();
        return message;
    }

    private static void AppendTop(StringBuilder body, string title, IReadOnlyList<CurrencyRateChange> items,
        string emptyText)
    {
        body.AppendLine(title);
        if (items.Count == 0)
        {
            body.AppendLine($"  - {emptyText}");
            return;
        }

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            body.AppendLine($"  {i + 1}. {item.CharCode} ({item.Name}): {Format.Rate(item.Rate)} ₽, " +
                            $"{Format.Percent(item.ChangePercent)}");
        }
    }
}
