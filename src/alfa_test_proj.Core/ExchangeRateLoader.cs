using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace alfa_test_proj.Core;

public class ExchangeRateLoader
{
    public async Task<List<CurrencyRate>> GetExchangeRates()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        const string url = "https://www.cbr.ru/scripts/XML_daily.asp";
        using HttpClient client = new HttpClient();

        byte[] rawResponse = await client.GetByteArrayAsync(url);
        string xmlString = Encoding.GetEncoding("windows-1251").GetString(rawResponse);

        var xml = XDocument.Parse(xmlString);
        var culture = CultureInfo.GetCultureInfo("ru-RU"); // правила парсинга xml, принятые в рф; нужно в первую очередь для свапа разделителя с точки на запятую

        var rates = xml.Element("ValCurs").Elements("Valute").Select(v =>
        {
            decimal nominal = decimal.Parse(v.Element("Nominal").Value, culture);
            decimal value = decimal.Parse(v.Element("Value").Value, culture);

            return new CurrencyRate
            {
                CharCode = v.Element("CharCode").Value,
                Name = v.Element("Name").Value,
                Value = value / nominal
            };
        }).ToList();
        return rates;
    }
}