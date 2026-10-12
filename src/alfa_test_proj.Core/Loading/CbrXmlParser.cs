using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using alfa_test_proj.Core.Errors;
using alfa_test_proj.Core.Models;
using Microsoft.Extensions.Logging;

namespace alfa_test_proj.Core.Loading;

// literally the most careful and accurate xml-parser on the wild west
internal static class CbrXmlParser
{
    private static readonly NumberFormatInfo CbrNumberFormat = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = " ",
    };

    private const string CbrDateFormat = "dd.MM.yyyy";

    private static readonly Encoding Windows1251;

    static CbrXmlParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Windows1251 = Encoding.GetEncoding("windows-1251");
    }

    public static RatesSnapshot ParseDaily(byte[] content, ILogger logger)
    {
        var root = LoadRoot(content, logger);

        var date = ParseDateAttribute(root, "Date")
                   ?? throw new InvalidRatesResponseException(
                       "Ответ ЦБ РФ не содержит даты курсов. Попробуйте позже или выберите другую дату.");

        var rates = new List<CurrencyRate>();
        foreach (var valute in root.Elements("Valute"))
        {
            var charCode = valute.Element("CharCode")?.Value.Trim();
            var name = valute.Element("Name")?.Value.Trim();

            if (string.IsNullOrEmpty(charCode) ||
                !TryParseDecimal(valute.Element("Nominal")?.Value, out var nominal) || nominal <= 0 ||
                !TryParseDecimal(valute.Element("Value")?.Value, out var value) || value <= 0)
            {
                logger.LogWarning("Не удалось разобрать валюту {CharCode} на {Date}, она пропущена",
                    charCode ?? "<без кода>", date);
                continue;
            }

            rates.Add(new CurrencyRate
            {
                Id = valute.Attribute("ID")?.Value ?? string.Empty,
                CharCode = charCode,
                Name = string.IsNullOrEmpty(name) ? charCode : name,
                // Страхуемся от случаев с номиналом > 1
                Value = value / nominal,
            });
        }

        if (rates.Count == 0)
        {
            throw new InvalidRatesResponseException(
                $"ЦБ РФ не вернул ни одного курса на {date:dd.MM.yyyy}, выберите другую дату.");
        }

        logger.LogInformation("Разобрано {Count} курсов на {Date:dd.MM.yyyy}", rates.Count, date);
        return new RatesSnapshot(date, rates);
    }

    public static IReadOnlyList<RatePoint> ParseDynamic(byte[] content, ILogger logger)
    {
        var root = LoadRoot(content, logger);

        var points = new List<RatePoint>();
        foreach (var record in root.Elements("Record"))
        {
            var date = ParseDateAttribute(record, "Date");
            if (date is null ||
                !TryParseDecimal(record.Element("Nominal")?.Value, out var nominal) || nominal <= 0 ||
                !TryParseDecimal(record.Element("Value")?.Value, out var value) || value <= 0)
            {
                logger.LogWarning("Не удалось разобрать запись динамики курса: {Record}", record.ToString());
                continue;
            }

            points.Add(new RatePoint(date.Value, value / nominal));
        }

        return points.OrderBy(p => p.Date).ToList();
    }

    private static XElement LoadRoot(byte[] content, ILogger logger)
    {
        if (content.Length == 0)
        {
            throw new InvalidRatesResponseException("Сервис ЦБ РФ вернул пустой ответ, попробуйте позже");
        }

        var xml = Windows1251.GetString(content);

        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (XmlException ex)
        {
            logger.LogError(ex, "Ответ ЦБ РФ не является корректным XML, голова ответа: {Head}",
                xml.Length > 300 ? xml[..300] : xml);
            throw new InvalidRatesResponseException(
                "Сервис ЦБ РФ вернул некорректный ответ (не XML), попробуйте позже", ex);
        }

        var root = document.Root;
        if (root is null || root.Name.LocalName != "ValCurs")
        {
            logger.LogError("Неожиданный корневой элемент в ответе ЦБ РФ: {Root}", root?.Name.LocalName);
            throw new InvalidRatesResponseException(
                "Сервис ЦБ РФ вернул ответ неожиданного формата, попробуйте позже");
        }

        return root;
    }

    private static DateOnly? ParseDateAttribute(XElement element, string attributeName)
    {
        var raw = element.Attribute(attributeName)?.Value;
        return DateOnly.TryParseExact(raw, CbrDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None,
            out var date)
            ? date
            : null;
    }

    private static bool TryParseDecimal(string? raw, out decimal value) =>
        decimal.TryParse(raw?.Trim(), NumberStyles.Number, CbrNumberFormat, out value);
}
