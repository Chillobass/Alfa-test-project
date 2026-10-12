using alfa_test_proj.Core.Caching;
using alfa_test_proj.Core.Configuration;
using alfa_test_proj.Core.Email;
using alfa_test_proj.Core.Llm;
using alfa_test_proj.Core.Loading;
using alfa_test_proj.Core.Logging;
using alfa_test_proj.Core.Reporting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace alfa_test_proj.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCurrencyReportCore(this IServiceCollection services,
        Action<CoreOptions>? configure = null)
    {
        var options = new CoreOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.TryAddSingleton(TimeProvider.System);

        services.AddLogging(logging => logging
            .AddFile(options.LogsDirectory)
            .AddFilter("System.Net.Http.HttpClient", LogLevel.Warning));

        services.AddHttpClient<ExchangeRateLoader>(client =>
        {
            client.BaseAddress = new Uri(options.CbrBaseUrl);
            client.Timeout = options.CbrTimeout;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; CbrRatesReport/1.0)");
        });
        services.AddHttpClient<LlmCommentGenerator>();

        services.AddSingleton(sp =>
            new RatesCache(options.CacheDirectory, sp.GetRequiredService<ILogger<RatesCache>>()));
        services.AddSingleton(sp =>
            new AppSettingsLoader(options.ResolvedConfigPath, sp.GetRequiredService<ILogger<AppSettingsLoader>>()));
        services.AddSingleton<PdfReportGenerator>();
        services.AddSingleton<SmtpReportSender>();

        // Transient, потому что зависит от typed HttpClient-ов (а их время жизни управляется фабрикой)
        services.AddTransient<ICurrencyReportService, CurrencyReportService>();
        return services;
    }
}
