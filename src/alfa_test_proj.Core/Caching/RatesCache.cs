using System.Globalization;
using System.Text.Json;
using alfa_test_proj.Core.Models;
using Microsoft.Extensions.Logging;

namespace alfa_test_proj.Core.Caching;

internal sealed record RatesDataSet(
    DateOnly RequestedDate,
    RatesSnapshot Current,
    RatesSnapshot Previous,
    IReadOnlyList<RateHistory> History,
    DateTimeOffset LoadedAt);

internal sealed class RatesCache
{
    public const int MaxEntries = 30;

    private const string FilePrefix = "rates-";
    private const string FileDateFormat = "yyyy-MM-dd";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _directory;
    private readonly ILogger<RatesCache> _logger;

    public RatesCache(string directory, ILogger<RatesCache> logger)
    {
        _directory = directory;
        _logger = logger;
    }

    public async Task SaveAsync(RatesDataSet data, CancellationToken cancellationToken = default)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            var path = GetPath(data.RequestedDate);
            var tempPath = path + ".tmp";

            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, data, JsonOptions, cancellationToken);
            }

            File.Move(tempPath, path, overwrite: true); // атомарная замена: не оставляем полузаписанный файл
            _logger.LogInformation("Данные на {Date:dd.MM.yyyy} сохранены в кэш: {Path}", data.RequestedDate, path);
            Trim();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Не удалось сохранить данные в кэш");
        }
    }

    public async Task<RatesDataSet?> TryLoadAsync(DateOnly requestedDate, CancellationToken cancellationToken = default)
    {
        var path = GetPath(requestedDate);
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            await using var stream = File.OpenRead(path);
            var data = await JsonSerializer.DeserializeAsync<RatesDataSet>(stream, JsonOptions, cancellationToken);
            if (data is null || data.Current.Rates.Count == 0)
            {
                _logger.LogWarning("Файл кэша {Path} пуст", path);
                return null;
            }

            _logger.LogInformation("Данные на {Date:dd.MM.yyyy} взяты из кэша (загружены {LoadedAt})",
                requestedDate, data.LoadedAt);
            return data;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
                                       or NotSupportedException)
        {
            _logger.LogWarning(ex, " файл с кэшем {Path} повреждён или недоступен", path);
            return null;
        }
    }

    public IReadOnlyList<CachedRatesInfo> List()
    {
        if (!Directory.Exists(_directory))
        {
            return [];
        }

        var result = new List<CachedRatesInfo>();
        foreach (var file in Directory.EnumerateFiles(_directory, FilePrefix + "*.json"))
        {
            try
            {
                using var stream = File.OpenRead(file);
                var data = JsonSerializer.Deserialize<RatesDataSet>(stream, JsonOptions);
                if (data is not null)
                {
                    result.Add(new CachedRatesInfo(data.RequestedDate, data.Current.Date, data.LoadedAt));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
                                           or NotSupportedException)
            {
                _logger.LogWarning(ex, "Пропущен повреждённый файл кэша {Path}", file);
            }
        }

        return result.OrderByDescending(r => r.LoadedAt).ToList();
    }

    private string GetPath(DateOnly requestedDate) =>
        Path.Combine(_directory,
            $"{FilePrefix}{requestedDate.ToString(FileDateFormat, CultureInfo.InvariantCulture)}.json");

    private void Trim()
    {
        var stale = new DirectoryInfo(_directory)
            .GetFiles(FilePrefix + "*.json")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Skip(MaxEntries);

        foreach (var file in stale)
        {
            try
            {
                file.Delete();
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Не удалось удалить старый файл кэша {Path}", file.FullName);
            }
        }
    }
}
