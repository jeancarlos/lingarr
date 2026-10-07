using System;
using System.Net.Http;
using System.Net.Http.Headers;
using Lingarr.Server.Services.Translation;
using Xunit;

namespace Lingarr.Server.Tests.Services.Translation;

public class QuotaRetryDelayTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private static HttpResponseHeaders Headers(params (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage();
        foreach (var (name, value) in headers)
        {
            response.Headers.TryAddWithoutValidation(name, value);
        }
        return response.Headers;
    }

    [Fact]
    public void FromHeaders_ParsesRetryAfterDeltaSeconds()
    {
        var result = QuotaRetryDelay.FromHeaders(Headers(("Retry-After", "186372")), Now);

        Assert.Equal(TimeSpan.FromSeconds(186372), result);
    }

    [Fact]
    public void FromHeaders_ParsesRetryAfterHttpDate()
    {
        var date = (Now + TimeSpan.FromSeconds(90)).ToString("r");

        var result = QuotaRetryDelay.FromHeaders(Headers(("Retry-After", date)), Now);

        Assert.Equal(TimeSpan.FromSeconds(90), result);
    }

    [Fact]
    public void FromHeaders_ClampsPastRetryAfterDateToZero()
    {
        var date = (Now - TimeSpan.FromSeconds(90)).ToString("r");

        var result = QuotaRetryDelay.FromHeaders(Headers(("Retry-After", date)), Now);

        Assert.Equal(TimeSpan.Zero, result);
    }

    [Fact]
    public void FromHeaders_ParsesRateLimitResetDuration()
    {
        var result = QuotaRetryDelay.FromHeaders(Headers(("x-ratelimit-reset-requests", "6m0s")), Now);

        Assert.Equal(TimeSpan.FromMinutes(6), result);
    }

    [Fact]
    public void FromHeaders_ParsesMillisecondResetDuration()
    {
        var result = QuotaRetryDelay.FromHeaders(Headers(("x-ratelimit-reset-tokens", "20ms")), Now);

        Assert.Equal(TimeSpan.FromMilliseconds(20), result);
    }

    [Fact]
    public void FromHeaders_TakesLargestResetWhenBothArePresent()
    {
        var result = QuotaRetryDelay.FromHeaders(
            Headers(("x-ratelimit-reset-requests", "1s"), ("x-ratelimit-reset-tokens", "2m30s")),
            Now);

        Assert.Equal(TimeSpan.FromSeconds(150), result);
    }

    [Fact]
    public void FromHeaders_PrefersRetryAfterOverRateLimitResets()
    {
        var result = QuotaRetryDelay.FromHeaders(
            Headers(("Retry-After", "10"), ("x-ratelimit-reset-requests", "5m")),
            Now);

        Assert.Equal(TimeSpan.FromSeconds(10), result);
    }

    [Fact]
    public void FromHeaders_ReturnsNullWithoutRelevantHeaders()
    {
        Assert.Null(QuotaRetryDelay.FromHeaders(Headers(), Now));
    }

    [Fact]
    public void FromHeaders_ReturnsNullForUnparsableResetValue()
    {
        Assert.Null(QuotaRetryDelay.FromHeaders(Headers(("x-ratelimit-reset-requests", "soon")), Now));
    }

    [Fact]
    public void FullJitter_StaysWithinExponentialCeiling()
    {
        var ceiling = TimeSpan.FromSeconds(5 * 8);

        for (var seed = 0; seed < 20; seed++)
        {
            var delay = QuotaRetryDelay.FullJitter(TimeSpan.FromSeconds(5), 3, new Random(seed));
            Assert.InRange(delay, TimeSpan.Zero, ceiling);
        }
    }

    [Fact]
    public void FullJitter_IsCappedAtMaxInProcessWait()
    {
        var delay = QuotaRetryDelay.FullJitter(TimeSpan.FromMinutes(10), 5, new Random(1));

        Assert.InRange(delay, TimeSpan.Zero, QuotaRetryDelay.MaxInProcessWait);
    }

    [Fact]
    public void FullJitter_ReturnsZeroForZeroBase()
    {
        Assert.Equal(TimeSpan.Zero, QuotaRetryDelay.FullJitter(TimeSpan.Zero, 3, new Random(1)));
    }

    [Theory]
    [InlineData(1, 300)]
    [InlineData(1800, 1800)]
    [InlineData(720000, 21600)]
    public void ClampReschedule_KeepsDelayBetweenFiveMinutesAndSixHours(int delaySeconds, int expectedSeconds)
    {
        var result = QuotaRetryDelay.ClampReschedule(TimeSpan.FromSeconds(delaySeconds));

        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), result);
    }
}
