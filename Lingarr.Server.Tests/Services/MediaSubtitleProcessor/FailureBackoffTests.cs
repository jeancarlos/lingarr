using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lingarr.Contracts.Models;
using Lingarr.Core.Entities;
using Lingarr.Core.Enum;
using Lingarr.Server.Models.FileSystem;
using Moq;
using Xunit;

namespace Lingarr.Server.Tests.Services.MediaSubtitleProcessor;

public class FailureBackoffTests : MediaSubtitleProcessorTestBase
{
    private async Task<Movie> ArrangeMovieMissingTarget(params (TranslationStatus Status, double HoursAgo)[] previous)
    {
        var movie = await CreateTestMovie();
        SubtitleServiceMock
            .Setup(s => s.GetSubtitles(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new List<Subtitles>
            {
                new()
                {
                    Path = "/movies/test/test.movie.en.srt",
                    FileName = "test.movie.en",
                    Language = "en",
                    Caption = "",
                    Format = ".srt"
                }
            });
        SetupStandardSettings();

        foreach (var (status, hoursAgo) in previous)
        {
            DbContext.TranslationRequests.Add(new TranslationRequest
            {
                MediaId = movie.Id,
                Title = "Test Movie",
                SourceLanguage = "en",
                TargetLanguage = "ro",
                SubtitleToTranslate = "/movies/test/test.movie.en.srt",
                MediaType = MediaType.Movie,
                Status = status,
                CompletedAt = DateTimeOffset.UtcNow.AddHours(-hoursAgo)
            });
        }
        await DbContext.SaveChangesAsync();
        return movie;
    }

    private void VerifyCreated(Times times) =>
        TranslationRequestServiceMock.Verify(s => s.CreateRequest(It.IsAny<TranslateAbleSubtitle>()), times);

    [Fact]
    public async Task ProcessMedia_RightAfterAFailure_ShouldWaitAndLeaveTheHashForALaterRetry()
    {
        var movie = await ArrangeMovieMissingTarget((TranslationStatus.Failed, 0.1));

        var result = await Processor.ProcessMedia(movie, MediaType.Movie);

        Assert.False(result);
        VerifyCreated(Times.Never());
        Assert.Null((await DbContext.Movies.FindAsync(movie.Id))!.MediaHash);
    }

    [Fact]
    public async Task ProcessMedia_RightAfterACancellation_ShouldWait()
    {
        var movie = await ArrangeMovieMissingTarget((TranslationStatus.Cancelled, 0.1));

        Assert.False(await Processor.ProcessMedia(movie, MediaType.Movie));
        VerifyCreated(Times.Never());
    }

    [Fact]
    public async Task ProcessMedia_OnceTheBackoffHasPassed_ShouldRetry()
    {
        var movie = await ArrangeMovieMissingTarget((TranslationStatus.Failed, 30), (TranslationStatus.Failed, 7));

        Assert.True(await Processor.ProcessMedia(movie, MediaType.Movie));
        VerifyCreated(Times.Once());
    }

    [Fact]
    public async Task ProcessMedia_AfterThreeRecentFailures_ShouldGiveUpAndStoreTheHash()
    {
        var movie = await ArrangeMovieMissingTarget(
            (TranslationStatus.Failed, 50), (TranslationStatus.Failed, 30), (TranslationStatus.Failed, 10));

        Assert.False(await Processor.ProcessMedia(movie, MediaType.Movie));
        VerifyCreated(Times.Never());
        Assert.NotNull((await DbContext.Movies.FindAsync(movie.Id))!.MediaHash);
    }

    [Fact]
    public async Task ProcessMedia_WithFailuresOlderThanAWeek_ShouldRetry()
    {
        var movie = await ArrangeMovieMissingTarget(
            (TranslationStatus.Failed, 200), (TranslationStatus.Failed, 190), (TranslationStatus.Failed, 180));

        Assert.True(await Processor.ProcessMedia(movie, MediaType.Movie));
        VerifyCreated(Times.Once());
    }
}
