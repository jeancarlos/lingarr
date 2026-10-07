namespace Lingarr.Contracts.Exceptions;

/// <summary>
/// Raised when a translation provider keeps reporting quota or rate-limit exhaustion
/// </summary>
public class TranslationQuotaException : TranslationException
{
    public TimeSpan RetryAfter { get; }

    public TranslationQuotaException(string message, TimeSpan retryAfter, Exception? exception = null)
        : base(message, exception)
    {
        RetryAfter = retryAfter;
    }

    public static TranslationQuotaException? FindIn(Exception? exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current is TranslationQuotaException quota)
            {
                return quota;
            }
        }

        return null;
    }
}
