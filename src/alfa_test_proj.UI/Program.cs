using alfa_test_proj.Core;
using alfa_test_proj.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Photino.Blazor;

internal class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var builder = PhotinoBlazorAppBuilder.CreateDefault(args);

        builder.Services.AddLogging(logging => logging
            .SetMinimumLevel(LogLevel.Information)
            .AddDebug());

        // Вся логика (загрузка, расчёты, PDF, почта, кэш, логи в файл) — в Core.
        // config.json, logs/, cache/ и reports/ — рядом с исполняемым файлом.
        builder.Services.AddCurrencyReportCore();

        builder.RootComponents.Add<App>("app");

        var app = builder.Build();

        app.MainWindow
            .SetTitle("Курсы валют ЦБ РФ")
            .SetSize(1240, 820)
            .SetMinSize(980, 640)
            .Center();

        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            logger.LogCritical(e.ExceptionObject as Exception, "Необработанное исключение");
            app.MainWindow.ShowMessage("Критическая ошибка",
                "Приложение столкнулось с непредвиденной ошибкой. Подробности записаны в лог (папка logs).\n\n" +
                (e.ExceptionObject as Exception)?.Message);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.LogError(e.Exception, "Необработанное исключение в фоновой задаче");
            e.SetObserved();
        };

        logger.LogInformation("Приложение запущено");
        app.Run();
    }
}
