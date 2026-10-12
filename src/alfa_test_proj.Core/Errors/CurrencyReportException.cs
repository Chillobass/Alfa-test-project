namespace alfa_test_proj.Core.Errors;

public class CurrencyReportException : Exception
{
    public CurrencyReportException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class RatesSourceUnavailableException : CurrencyReportException
{
    public RatesSourceUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class InvalidRatesResponseException : CurrencyReportException
{
    public InvalidRatesResponseException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class InvalidRecipientException : CurrencyReportException
{
    public InvalidRecipientException(string message) : base(message)
    {
    }
}

public sealed class EmailSendException : CurrencyReportException
{
    public EmailSendException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class ConfigurationException : CurrencyReportException
{
    public ConfigurationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class ReportGenerationException : CurrencyReportException
{
    public ReportGenerationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
