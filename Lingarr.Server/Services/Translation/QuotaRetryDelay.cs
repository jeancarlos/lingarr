using System.Globalization;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace Lingarr.Server.Services.Translation;

public static partial class QuotaRetryDelay
{
    public const int MaxQuotaAttempts = 4;
    public static readonly TimeSpan MaxInProcessWait = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MinReschedule = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaxReschedule = TimeSpan.FromHours(6);

    public static TimeSpan? FromHeaders(HttpResponseHeaders headers, DateTimeOffset now)
    {
        if (headers.RetryAfter is { } retryAfter)
        {
            if (retryAfter.Delta is { } delta)
            {
                return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
            }

            if (retryAfter.Date is { } date)
            {
                var wait = date - now;
                return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
            }
        }

        TimeSpan? reset = null;
        foreach (var name in new[] { "x-ratelimit-reset-requests", "x-ratelimit-reset-tokens" })
        {
            if (headers.TryGetValues(name, out var values) &&
                ParseDuration(values.FirstOrDefault()) is { } parsed &&
                (reset is null || parsed > reset))
            {
                reset = parsed;
            }
        }

        return reset;
    }

    public static TimeSpan FullJitter(TimeSpan baseDelay, int attempt, Random random)
    {
        var ceiling = Math.Min(
            MaxInProcessWait.TotalMilliseconds,
            baseDelay.TotalMilliseconds * Math.Pow(2, attempt));
        return TimeSpan.FromMilliseconds(random.NextDouble() * ceiling);
    }

    public static TimeSpan ClampReschedule(TimeSpan delay) =>
        delay < MinReschedule ? MinReschedule
        : delay > MaxReschedule ? MaxReschedule
        : delay;

    private static TimeSpan? ParseDuration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var matches = DurationRegex().Matches(value);
        if (matches.Count == 0)
        {
            return null;
        }

        var milliseconds = 0d;
        foreach (Match match in matches)
        {
            var amount = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            milliseconds += match.Groups[2].Value.ToLowerInvariant() switch
            {
                "h" => amount * 3_600_000,
                "m" => amount * 60_000,
                "s" => amount * 1_000,
                _ => amount
            };
        }

        return TimeSpan.FromMilliseconds(milliseconds);
    }

    [GeneratedRegex(@"(\d+(?:\.\d+)?)(ms|h|m|s)", RegexOptions.IgnoreCase)]
    private static partial Regex DurationRegex();
}
