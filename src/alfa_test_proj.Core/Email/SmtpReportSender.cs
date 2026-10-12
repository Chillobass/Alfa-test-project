using System.Net.Sockets;
using alfa_test_proj.Core.Configuration;
using alfa_test_proj.Core.Errors;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace alfa_test_proj.Core.Email;

internal sealed class SmtpReportSender
{
    private readonly ILogger<SmtpReportSender> _logger;

    public SmtpReportSender(ILogger<SmtpReportSender> logger)
    {
        _logger = logger;
    }

    public async Task SendAsync(SmtpSettings settings, MimeMessage message, CancellationToken cancellationToken)
    {
        var security = Enum.Parse<SecureSocketOptions>(settings.Security, ignoreCase: true);
        var server = $"{settings.Host}:{settings.Port}";

        using var client = new SmtpClient();
        client.Timeout = (int)TimeSpan.FromSeconds(settings.TimeoutSeconds).TotalMilliseconds;

        try
        {
            _logger.LogInformation("Подключение к SMTP {Server} ({Security})", server, security);
            await client.ConnectAsync(settings.Host, settings.Port, security, cancellationToken);

            if (!string.IsNullOrWhiteSpace(settings.UserName))
            {
                await client.AuthenticateAsync(settings.UserName, settings.Password ?? string.Empty,
                    cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);
            _logger.LogInformation("Письмо «{Subject}» отправлено на {Recipients}", message.Subject,
                string.Join(", ", message.To));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки письма через {Server}", server);
            throw new EmailSendException(Describe(ex, server), ex);
        }
    }

    private static string Describe(Exception ex, string server) => ex switch
    {
        AuthenticationException =>
            $"SMTP-сервер {server} отклонил логин или пароль. Проверьте Smtp.UserName и Smtp.Password " +
            "(для Яндекс/Mail.ru/Gmail нужен пароль приложения).",
        SslHandshakeException =>
            $"Не удалось установить защищённое соединение с {server}. Проверьте Smtp.Port и Smtp.Security " +
            "(обычно 465 - SslOnConnect, 587 - StartTls).",
        SmtpCommandException { ErrorCode: SmtpErrorCode.RecipientNotAccepted } cmd =>
            $"SMTP-сервер не принял адрес получателя {cmd.Mailbox?.Address}: {cmd.Message}",
        SmtpCommandException { ErrorCode: SmtpErrorCode.SenderNotAccepted } cmd =>
            $"SMTP-сервер не принял адрес отправителя {cmd.Mailbox?.Address}. " +
            $"Обычно он должен совпадать с логином. Ответ сервера: {cmd.Message}",
        SmtpCommandException cmd =>
            $"SMTP-сервер отклонил письмо (код {(int)cmd.StatusCode}): {cmd.Message}",
        NotSupportedException =>
            $"SMTP-сервер {server} не поддерживает выбранный режим шифрования или аутентификации " +
            $"(Smtp.Security). Попробуйте значение Auto. Ответ: {ex.Message}",
        SmtpProtocolException =>
            $"Ошибка протокола при общении с SMTP-сервером {server}. Проверьте адрес сервера и порт.",
        SocketException =>
            $"Не удалось подключиться к SMTP-серверу {server}. Проверьте подключение к интернету и Smtp.Host/Smtp.Port.",
        TimeoutException or OperationCanceledException =>
            $"SMTP-сервер {server} не ответил вовремя. Проверьте подключение к интернету и настройки сервера.",
        IOException =>
            $"Соединение с SMTP-сервером {server} было прервано. Проверьте Smtp.Port и Smtp.Security.",
        _ => $"Не удалось отправить письмо: {ex.Message}",
    };
}
