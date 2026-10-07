using System;
using Lingarr.Contracts.Exceptions;
using Xunit;

namespace Lingarr.Server.Tests.Core;

public class TranslationQuotaExceptionTests
{
    [Fact]
    public void FindIn_ReturnsTheExceptionItself()
    {
        var quota = new TranslationQuotaException("out of quota", TimeSpan.FromMinutes(10));

        Assert.Same(quota, TranslationQuotaException.FindIn(quota));
    }

    [Fact]
    public void FindIn_WalksTheInnerExceptionChain()
    {
        var quota = new TranslationQuotaException("out of quota", TimeSpan.FromMinutes(10));
        var wrapped = new TranslationException("All configured batch translation services failed.",
            new TranslationException("service failed", quota));

        Assert.Same(quota, TranslationQuotaException.FindIn(wrapped));
    }

    [Fact]
    public void FindIn_ReturnsNullWhenNoQuotaFailureIsPresent()
    {
        var error = new TranslationException("failed", new InvalidOperationException("boom"));

        Assert.Null(TranslationQuotaException.FindIn(error));
        Assert.Null(TranslationQuotaException.FindIn(null));
    }
}
