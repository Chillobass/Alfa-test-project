using System.Text.RegularExpressions;
using MimeKit;

namespace alfa_test_proj.Core.Email;

internal static partial class EmailAddressValidator
{
    // Простая, но строгая к типичным опечаткам форма - local@domain.tld, без пробелов и лишних @
    [GeneratedRegex(@"^[^@\s<>()\[\],;:""]+@[A-Za-z0-9А-Яа-яЁё](?:[A-Za-z0-9А-Яа-яЁё\-]*[A-Za-z0-9А-Яа-яЁё])?(?:\.[A-Za-z0-9А-Яа-яЁё](?:[A-Za-z0-9А-Яа-яЁё\-]*[A-Za-z0-9А-Яа-яЁё])?)+$")]
    private static partial Regex AddressPattern();

    public static bool TryNormalize(string? input, out string normalized, out string error)
    {
        normalized = (input ?? string.Empty).Trim();
        error = string.Empty;

        if (normalized.Length == 0)
        {
            error = "Укажите адрес электронной почты получателя.";
            return false;
        }

        if (normalized.Length > 254)
        {
            error = "Адрес электронной почты слишком длинный.";
            return false;
        }

        if (!AddressPattern().IsMatch(normalized) ||
            normalized.Contains("..", StringComparison.Ordinal) ||
            normalized.StartsWith('.') ||
            normalized.Split('@')[0].EndsWith('.') ||
            !MailboxAddress.TryParse(normalized, out _))
        {
            error = $"«{normalized}» не похож на адрес электронной почты. Пример: name@example.com";
            return false;
        }

        return true;
    }
}
