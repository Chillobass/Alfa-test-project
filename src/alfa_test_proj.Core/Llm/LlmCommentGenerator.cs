using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using alfa_test_proj.Core.Configuration;
using alfa_test_proj.Core.Models;
using alfa_test_proj.Core.Reporting;
using Microsoft.Extensions.Logging;

namespace alfa_test_proj.Core.Llm;

internal sealed class LlmCommentGenerator
{
    private const int MaxCommentLength = 600;

    private readonly HttpClient _client;
    private readonly ILogger<LlmCommentGenerator> _logger;

    public LlmCommentGenerator(HttpClient client, ILogger<LlmCommentGenerator> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<string?> TryGenerateAsync(RatesReport report, LlmSettings settings,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(settings.TimeoutSeconds, 1)));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint);
            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            }

            var payload = new ChatRequest(
                settings.Model,
                [
                    new ChatMessage("system",
                        "Ты финансовый обозреватель. Пиши нейтрально и по делу. " +
                        "Ответь 1–2 предложениями без markdown, без приветствий и без инвестиционных советов."),
                    new ChatMessage("user", BuildPrompt(report)),
                ],
                Temperature: 0.4,
                MaxTokens: 200);
            // StringContent вместо JsonContent, тк с ним уходит Content-Length, а не chunked-тело, которое понимают не все openAI-совместимые серверы.
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            _logger.LogInformation("Запрос комментария у языковой модели {Model} ({Endpoint})", settings.Model,
                settings.Endpoint);
            using var response = await _client.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(timeout.Token);
                _logger.LogWarning("LLM API вернул HTTP {StatusCode}: {Body}", (int)response.StatusCode,
                    error.Length > 500 ? error[..500] : error);
                return null;
            }

            var completion = await response.Content.ReadFromJsonAsync<ChatResponse>(timeout.Token);
            var text = completion?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("LLM API вернул пустой ответ");
                return null;
            }

            text = text.Replace("\r", " ").Replace("\n", " ").Trim('"', ' ', '«', '»');
            if (text.Length > MaxCommentLength)
            {
                text = text[..MaxCommentLength].TrimEnd() + "…";
            }

            _logger.LogInformation("Получен комментарий языковой модели: {Comment}", text);
            return text;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException
                                       or NotSupportedException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Не удалось получить комментарий языковой модели - письмо уйдёт без него");
            return null;
        }
    }

    internal static string BuildPrompt(RatesReport report)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine($"Официальные курсы ЦБ РФ на {Format.Date(report.RatesDate)} " +
                          $"по сравнению с {Format.Date(report.PreviousRatesDate)}.");
        prompt.AppendLine("Сильнее всего выросли: " + Describe(report.Summary.TopGainers));
        prompt.AppendLine("Сильнее всего упали: " + Describe(report.Summary.TopLosers));
        prompt.AppendLine($"Среднее изменение по всем валютам: {Format.Percent(report.Summary.AverageChangePercent)}.");

        foreach (var code in new[] { "USD", "EUR", "CNY" })
        {
            var rate = report.Rates.FirstOrDefault(r => r.CharCode == code);
            if (rate is not null)
            {
                prompt.AppendLine($"{code}: {Format.Rate(rate.Rate)} ₽ ({Format.Percent(rate.ChangePercent)}).");
            }
        }

        prompt.AppendLine("Кратко прокомментируй, что произошло с рублём за день.");
        return prompt.ToString();
    }

    private static string Describe(IReadOnlyList<CurrencyRateChange> items) =>
        items.Count == 0
            ? "нет"
            : string.Join(", ", items.Select(i => $"{i.CharCode} {Format.Percent(i.ChangePercent)}"));

    private sealed record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatChoice([property: JsonPropertyName("message")] ChatMessage? Message);

    private sealed record ChatResponse([property: JsonPropertyName("choices")] IReadOnlyList<ChatChoice>? Choices);
}
