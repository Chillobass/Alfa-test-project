using alfa_test_proj.Core.Configuration;
using alfa_test_proj.Core.Errors;
using Microsoft.Extensions.Logging.Abstractions;

namespace alfa_test_proj.Core.Tests;

public sealed class AppSettingsLoaderTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("cbr-config-").FullName;

    private AppSettingsLoader Loader(string? json)
    {
        var path = Path.Combine(_directory, "config.json");
        if (json is not null)
        {
            File.WriteAllText(path, json);
        }

        return new AppSettingsLoader(path, NullLogger<AppSettingsLoader>.Instance);
    }

    [Fact]
    public void MissingFile_Throws()
    {
        var ex = Assert.Throws<ConfigurationException>(() => Loader(null).LoadSmtpSettings());
        Assert.Contains("config.example.json", ex.Message);
    }

    [Fact]
    public void InvalidJson_Throws() =>
        Assert.Throws<ConfigurationException>(() => Loader("{ \"Smtp\": { \"Host\": ").LoadSmtpSettings());

    [Fact]
    public void MissingSmtpSection_Throws() =>
        Assert.Throws<ConfigurationException>(() => Loader("{}").LoadSmtpSettings());

    [Fact]
    public void TemplateValues_Throw()
    {
        var json = File.ReadAllText(FindExampleConfig());
        var ex = Assert.Throws<ConfigurationException>(() => Loader(json).LoadSmtpSettings());
        Assert.Contains("шаблона", ex.Message);
    }

    [Theory]
    [InlineData("\"Port\": 465", "\"Port\": 0")]
    [InlineData("\"FromAddress\": \"me@yandex.ru\"", "\"FromAddress\": \"not-an-email\"")]
    [InlineData("\"Security\": \"SslOnConnect\"", "\"Security\": \"Tls13\"")]
    [InlineData("\"Password\": \"app-password\"", "\"Password\": \"\"")]
    public void InvalidValues_Throw(string original, string replacement) =>
        Assert.Throws<ConfigurationException>(() => Loader(ValidJson.Replace(original, replacement)).LoadSmtpSettings());

    [Fact]
    public void ValidConfig_IsLoaded_CommentsAndTrailingCommasAllowed()
    {
        var smtp = Loader(ValidJson).LoadSmtpSettings();

        Assert.Equal("smtp.yandex.ru", smtp.Host);
        Assert.Equal(465, smtp.Port);
        Assert.Equal("SslOnConnect", smtp.Security);
    }

    [Fact]
    public void LlmSettings_DisabledByDefault() =>
        Assert.Null(Loader(ValidJson).TryLoadLlmSettings());

    private const string ValidJson = """
        {
          // комментарий
          "Smtp": {
            "Host": "smtp.yandex.ru",
            "Port": 465,
            "UserName": "me@yandex.ru",
            "Password": "app-password",
            "FromAddress": "me@yandex.ru",
            "Security": "SslOnConnect",
          }
        }
        """;

    private static string FindExampleConfig()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "config.example.json")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new FileNotFoundException("config.example.json"), "config.example.json");
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
