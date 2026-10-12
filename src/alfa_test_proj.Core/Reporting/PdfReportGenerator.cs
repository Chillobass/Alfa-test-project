using alfa_test_proj.Core.Errors;
using alfa_test_proj.Core.Models;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace alfa_test_proj.Core.Reporting;

internal sealed class PdfReportGenerator
{
    // в Lato нет знака рубля, поэтому в PDF пишем "руб."
    private const string FontFamily = "Lato";

    private const string Ink = "#101828";
    private const string Muted = "#667085";
    private const string Line = "#E4E7EC";
    private const string Surface = "#F7F8FA";
    private const string Accent = "#C8102E";
    private const string Up = "#067647";
    private const string UpBg = "#ECFDF3";
    private const string Down = "#B42318";
    private const string DownBg = "#FEF3F2";

    private static readonly string[] ChartColors = ["#1F4E8C", "#7A5AF8"];

    private readonly ILogger<PdfReportGenerator> _logger;

    static PdfReportGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public PdfReportGenerator(ILogger<PdfReportGenerator> logger)
    {
        _logger = logger;
    }

    public byte[] Generate(RatesReport report)
    {
        try
        {
            var pdf = Document.Create(container => container.Page(page => ComposePage(page, report)))
                .WithMetadata(new DocumentMetadata
                {
                    Title = $"Курсы валют ЦБ РФ на {Format.Date(report.RatesDate)}",
                    Author = "Курсы валют ЦБ РФ",
                    Subject = "Официальные курсы иностранных валют к рублю",
                    CreationDate = DateTimeOffset.Now,
                })
                .GeneratePdf();

            _logger.LogInformation("PDF-отчёт на {Date:dd.MM.yyyy} сформирован, {Size} байт", report.RatesDate,
                pdf.Length);
            return pdf;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка формирования PDF на {Date:dd.MM.yyyy}", report.RatesDate);
            throw new ReportGenerationException($"Не удалось сформировать PDF-отчёт: {ex.Message}", ex);
        }
    }

    private static void ComposePage(PageDescriptor page, RatesReport report)
    {
        page.Size(PageSizes.A4);
        page.Margin(36);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontSize(9.5f).FontColor(Ink));

        page.Header().Element(c => ComposeHeader(c, report));
        page.Content().PaddingTop(18).Column(column =>
        {
            column.Spacing(22);
            column.Item().Element(c => ComposeSummary(c, report));
            if (report.History.Any(h => h.Points.Count >= 2))
            {
                column.Item().Element(c => ComposeCharts(c, report));
            }

            column.Item().Element(c => ComposeTable(c, report));
        });
        page.Footer().Element(c => ComposeFooter(c, report));
    }

    private static void ComposeHeader(IContainer container, RatesReport report)
    {
        container.Column(column =>
        {
            column.Item().ShowOnce().Element(c => ComposeFullHeader(c, report));
            column.Item().SkipOnce().Column(compact =>
            {
                compact.Item().Row(row =>
                {
                    row.RelativeItem().Text($"Курсы валют ЦБ РФ на {Format.Date(report.RatesDate)}")
                        .FontSize(9).SemiBold();
                    row.AutoItem().Text($"изменение относительно {Format.Date(report.PreviousRatesDate)}")
                        .FontSize(8).FontColor(Muted);
                });
                compact.Item().PaddingTop(6).LineHorizontal(1).LineColor(Accent);
            });
        });
    }

    private static void ComposeFullHeader(IContainer container, RatesReport report)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text("ОФИЦИАЛЬНЫЕ КУРСЫ БАНКА РОССИИ")
                        .FontSize(8).SemiBold().FontColor(Accent).LetterSpacing(0.08f);
                    left.Item().PaddingTop(4).Text("Курсы валют ЦБ РФ").FontSize(22).Bold();
                    left.Item().PaddingTop(2).Text(text =>
                    {
                        text.Span("на ").FontColor(Muted).FontSize(11);
                        text.Span(Format.Date(report.RatesDate)).SemiBold().FontSize(11);
                        text.Span($"  ·  изменение относительно {Format.Date(report.PreviousRatesDate)}")
                            .FontColor(Muted).FontSize(11);
                    });
                    if (report.RequestedDate != report.RatesDate)
                    {
                        left.Item().PaddingTop(2)
                            .Text($"Запрошена дата {Format.Date(report.RequestedDate)}; " +
                                  $"по данным ЦБ на неё действуют курсы, установленные на {Format.Date(report.RatesDate)}.")
                            .FontSize(8).FontColor(Muted);
                    }
                });

                row.ConstantItem(150).AlignRight().AlignBottom().Column(right =>
                {
                    right.Item().AlignRight().Text("Сформирован").FontSize(8).FontColor(Muted);
                    right.Item().AlignRight().Text(DateTime.Now.ToString("dd.MM.yyyy HH:mm")).FontSize(9);
                });
            });

            column.Item().PaddingTop(12).LineHorizontal(2).LineColor(Accent);
        });
    }

    private static void ComposeSummary(IContainer container, RatesReport report)
    {
        var summary = report.Summary;
        var comparableCount = summary.ComparedCount;

        container.Column(column =>
        {
            column.Item().PaddingBottom(8).Text("Итоги дня").FontSize(14).Bold();
            column.Item().Row(row =>
            {
                row.Spacing(10);
                row.RelativeItem().Element(c =>
                    ComposeTopCard(c, "Топ-3 роста", summary.TopGainers, Up, UpBg, "Выросших валют нет"));
                row.RelativeItem().Element(c =>
                    ComposeTopCard(c, "Топ-3 падения", summary.TopLosers, Down, DownBg, "Подешевевших валют нет"));
                row.RelativeItem().Background(Surface).Border(1).BorderColor(Line).Padding(12).Column(card =>
                {
                    card.Item().Text("Среднее изменение").FontSize(9).SemiBold().FontColor(Muted);
                    var average = summary.AverageChangePercent;
                    card.Item().PaddingTop(10).Text(Format.Percent(average)).FontSize(22).Bold()
                        .FontColor(average switch { > 0 => Up, < 0 => Down, _ => Ink });
                    card.Item().PaddingTop(4).Text($"по {comparableCount} {Format.Plural(comparableCount, "валюте", "валютам", "валютам")} из {report.Rates.Count}")
                        .FontSize(8).FontColor(Muted);
                });
            });
        });
    }

    private static void ComposeTopCard(IContainer container, string title, IReadOnlyList<CurrencyRateChange> items,
        string color, string background, string emptyText)
    {
        container.Border(1).BorderColor(Line).Column(card =>
        {
            card.Item().Background(background).PaddingVertical(7).PaddingHorizontal(12)
                .Text(title).FontSize(9).SemiBold().FontColor(color);

            card.Item().PaddingHorizontal(12).PaddingVertical(8).Column(list =>
            {
                list.Spacing(6);
                if (items.Count == 0)
                {
                    list.Item().Text(emptyText).FontColor(Muted).FontSize(9);
                    return;
                }

                foreach (var item in items)
                {
                    list.Item().Row(row =>
                    {
                        row.ConstantItem(34).Text(item.CharCode).Bold();
                        row.RelativeItem().Text(item.Name).FontSize(8).FontColor(Muted).ClampLines(1);
                        row.ConstantItem(56).AlignRight().Text(Format.Percent(item.ChangePercent)).SemiBold()
                            .FontColor(color);
                    });
                }
            });
        });
    }

    private static void ComposeCharts(IContainer container, RatesReport report)
    {
        var histories = report.History.Where(h => h.Points.Count >= 2).ToList();

        container.Column(column =>
        {
            column.Item().PaddingBottom(8).Text("Динамика USD и EUR за последние 7 дней").FontSize(14).Bold();
            column.Item().Row(row =>
            {
                row.Spacing(10);
                for (var i = 0; i < histories.Count; i++)
                {
                    var history = histories[i];
                    var color = ChartColors[i % ChartColors.Length];
                    var svg = RateChartSvg.Render(history, color, FontFamily);
                    var first = history.Points[0];
                    var last = history.Points[^1];
                    var periodChange = history.PeriodChangePercent;

                    row.RelativeItem().Border(1).BorderColor(Line).Padding(12).Column(card =>
                    {
                        card.Item().Row(head =>
                        {
                            head.RelativeItem().Text(text =>
                            {
                                text.Span(history.CharCode).Bold().FontSize(11);
                                text.Span($"  {history.Name}").FontColor(Muted).FontSize(8);
                            });
                            head.AutoItem().AlignRight().Text(text =>
                            {
                                text.Span($"{Format.Rate(last.Value)} руб.  ").SemiBold();
                                text.Span(Format.Percent(periodChange)).FontSize(8)
                                    .FontColor(periodChange switch { > 0 => Up, < 0 => Down, _ => Muted });
                            });
                        });
                        card.Item().PaddingTop(2)
                            .Text($"{Format.Date(first.Date)} - {Format.Date(last.Date)}").FontSize(7.5f)
                            .FontColor(Muted);
                        if (svg is not null)
                        {
                            card.Item().PaddingTop(6).Svg(svg);
                        }
                    });
                }

                if (histories.Count == 1)
                {
                    row.RelativeItem();
                }
            });
        });
    }

    private static void ComposeTable(IContainer container, RatesReport report)
    {
        container.Column(column =>
        {
            column.Item().PaddingBottom(8).Row(row =>
            {
                row.RelativeItem().Text("Курсы валют").FontSize(14).Bold();
                row.AutoItem().AlignBottom().Text($"{report.Rates.Count} {Format.Plural(report.Rates.Count, "валюта", "валюты", "валют")} · руб. за 1 единицу валюты")
                    .FontSize(8).FontColor(Muted);
            });

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(42);
                    columns.RelativeColumn();
                    columns.ConstantColumn(82);
                    columns.ConstantColumn(82);
                    columns.ConstantColumn(66);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Код");
                    header.Cell().Element(HeaderCell).Text("Валюта");
                    header.Cell().Element(HeaderCell).AlignRight().Text($"Курс на {report.RatesDate:dd.MM}");
                    header.Cell().Element(HeaderCell).AlignRight().Text($"Курс на {report.PreviousRatesDate:dd.MM}");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Изм., %");
                });

                for (var i = 0; i < report.Rates.Count; i++)
                {
                    var rate = report.Rates[i];
                    var background = i % 2 == 0 ? "#FFFFFF" : Surface;
                    var changeColor = rate.ChangePercent switch { > 0 => Up, < 0 => Down, _ => Muted };

                    table.Cell().Element(c => BodyCell(c, background)).Text(rate.CharCode).SemiBold();
                    table.Cell().Element(c => BodyCell(c, background)).Text(rate.Name);
                    table.Cell().Element(c => BodyCell(c, background)).AlignRight().Text(Format.Rate(rate.Rate));
                    table.Cell().Element(c => BodyCell(c, background)).AlignRight()
                        .Text(rate.PreviousRate is { } previous ? Format.Rate(previous) : "-").FontColor(Muted);
                    table.Cell().Element(c => BodyCell(c, background)).AlignRight()
                        .Text(Format.Percent(rate.ChangePercent)).SemiBold().FontColor(changeColor);
                }
            });
        });

        static IContainer HeaderCell(IContainer cell) => cell
            .BorderBottom(1).BorderColor(Ink)
            .PaddingVertical(6).PaddingHorizontal(6)
            .DefaultTextStyle(x => x.FontSize(8).SemiBold().FontColor(Muted));

        static IContainer BodyCell(IContainer cell, string background) => cell
            .Background(background)
            .BorderBottom(0.5f).BorderColor(Line)
            .PaddingVertical(4).PaddingHorizontal(6);
    }

    private static void ComposeFooter(IContainer container, RatesReport report)
    {
        container.BorderTop(0.5f).BorderColor(Line).PaddingTop(6).Row(row =>
        {
            var source = report.IsFromCache
                ? $"Источник: Банк России, cbr.ru · данные из локального кэша, загружены {report.LoadedAt:dd.MM.yyyy HH:mm}"
                : $"Источник: Банк России, cbr.ru · данные загружены {report.LoadedAt:dd.MM.yyyy HH:mm}";
            row.RelativeItem().Text(source).FontSize(7.5f).FontColor(Muted);
            row.AutoItem().Text(text =>
            {
                text.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Muted));
                text.Span("Стр. ");
                text.CurrentPageNumber();
                text.Span(" из ");
                text.TotalPages();
            });
        });
    }
}
