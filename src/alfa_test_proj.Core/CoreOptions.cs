using alfa_test_proj.Core.Loading;

namespace alfa_test_proj.Core;

public sealed class CoreOptions
{
    public string DataDirectory { get; set; } = AppContext.BaseDirectory;

    public string? ConfigPath { get; set; }

    public string CbrBaseUrl { get; set; } = ExchangeRateLoader.DefaultBaseUrl;

    public TimeSpan CbrTimeout { get; set; } = TimeSpan.FromSeconds(20);

    public string ResolvedConfigPath => ConfigPath ?? Path.Combine(DataDirectory, "config.json");

    public string LogsDirectory => Path.Combine(DataDirectory, "logs");

    public string CacheDirectory => Path.Combine(DataDirectory, "cache");

    public string ReportsDirectory => Path.Combine(DataDirectory, "reports");
}
