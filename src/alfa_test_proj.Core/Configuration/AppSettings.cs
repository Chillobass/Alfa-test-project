namespace alfa_test_proj.Core.Configuration;

internal sealed class AppSettings
{
    public SmtpSettings? Smtp { get; set; }

    public LlmSettings? Llm { get; set; }
}

internal sealed class SmtpSettings
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = string.Empty;

    public string? FromName { get; set; }

    public string Security { get; set; } = "Auto";

    public int TimeoutSeconds { get; set; } = 30;
}

internal sealed class LlmSettings
{
    public bool Enabled { get; set; }

    public string Endpoint { get; set; } = "https://api.openai.com/v1/chat/completions";

    public string? ApiKey { get; set; }

    public string Model { get; set; } = "gpt-4o-mini";

    public int TimeoutSeconds { get; set; } = 20;
}
