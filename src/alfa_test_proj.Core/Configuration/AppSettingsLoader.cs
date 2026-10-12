using System.Text.Json;
using alfa_test_proj.Core.Email;
using alfa_test_proj.Core.Errors;
using Microsoft.Extensions.Logging;
using MailKit.Security;

namespace alfa_test_proj.Core.Configuration;

internal sealed class AppSettingsLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    // это значения из config.example.json. Если они остались, значит конфиг не заполнен
    private const string ExampleHost = "smtp.example.com";
    private const string ExamplePassword = "CHANGE_ME";

    private readonly ILogger<AppSettingsLoader> _logger;

    public AppSettingsLoader(string configPath, ILogger<AppSettingsLoader> logger)
    {
        ConfigPath = Path.GetFullPath(configPath);
        _logger = logger;
    }

    public string ConfigPath { get; }

    public AppSettings Load()
    {
        if (!File.Exists(ConfigPath))
        {
            throw new ConfigurationException(
                $"Не найден файл конфигурации {ConfigPath}. " +
                "Скопируйте config.example.json в config.json и заполните параметры SMTP.");
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                   ?? throw new ConfigurationException($"Файл конфигурации {ConfigPath} пуст.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Некорректный JSON в {Path}", ConfigPath);
            var position = ex.LineNumber is { } line ? $" (строка {line + 1})" : string.Empty;
            throw new ConfigurationException(
                $"Файл конфигурации {ConfigPath} содержит некорректный JSON{position}. Сверьтесь с config.example.json.",
                ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Не удалось прочитать {Path}", ConfigPath);
            throw new ConfigurationException($"Не удалось прочитать файл конфигурации {ConfigPath}: {ex.Message}", ex);
        }
    }

    public SmtpSettings LoadSmtpSettings() => ValidateSmtp(Load().Smtp);

    public LlmSettings? TryLoadLlmSettings()
    {
        try
        {
            var llm = Load().Llm;
            if (llm is not { Enabled: true })
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(llm.Endpoint) || !Uri.TryCreate(llm.Endpoint, UriKind.Absolute, out _) ||
                string.IsNullOrWhiteSpace(llm.Model))
            {
                _logger.LogWarning("Секция Llm заполнена некорректно (Endpoint/Model) - комментарий не будет добавлен");
                return null;
            }

            return llm;
        }
        catch (ConfigurationException ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать настройки LLM");
            return null;
        }
    }

    private SmtpSettings ValidateSmtp(SmtpSettings? smtp)
    {
        if (smtp is null)
        {
            throw Invalid("в файле нет секции \"Smtp\"");
        }

        if (string.IsNullOrWhiteSpace(smtp.Host))
        {
            throw Invalid("не указан Smtp.Host");
        }

        if (smtp.Host.Equals(ExampleHost, StringComparison.OrdinalIgnoreCase) || smtp.Password == ExamplePassword)
        {
            throw Invalid("остались значения из шаблона config.example.json - укажите реальный SMTP-сервер и пароль");
        }

        if (smtp.Port is < 1 or > 65535)
        {
            throw Invalid($"некорректный Smtp.Port ({smtp.Port})");
        }

        if (!EmailAddressValidator.TryNormalize(smtp.FromAddress, out _, out _))
        {
            throw Invalid("некорректный адрес отправителя Smtp.FromAddress");
        }

        if (!string.IsNullOrWhiteSpace(smtp.UserName) && string.IsNullOrEmpty(smtp.Password))
        {
            throw Invalid("указан Smtp.UserName, но не указан Smtp.Password");
        }

        if (!Enum.TryParse<SecureSocketOptions>(smtp.Security, ignoreCase: true, out _))
        {
            throw Invalid("Smtp.Security должен быть одним из данных: Auto, SslOnConnect, StartTls, StartTlsWhenAvailable, None");
        }

        if (smtp.TimeoutSeconds <= 0)
        {
            throw Invalid("Smtp.TimeoutSeconds должен быть больше нуля");
        }

        return smtp;
    }

    private ConfigurationException Invalid(string reason) =>
        new($"Некорректная конфигурация SMTP в {ConfigPath}: {reason}.");
}
