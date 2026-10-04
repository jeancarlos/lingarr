using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lingarr.Contracts.Models;
using Lingarr.Core.Enum;
using Lingarr.Server.Models.FileSystem;
using Moq;
using Xunit;

namespace Lingarr.Server.Tests.Services.MediaSubtitleProcessor;

public class BazarrRetryTests : MediaSubtitleProcessorTestBase
{
    private async Task<int> ArrangeMovieMissingTarget()
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
        return movie.RadarrId;
    }

    [Fact]
    public async Task ProcessMedia_WhenBazarrIsNotReady_ShouldNotTranslate()
    {
        var radarrId = await ArrangeMovieMissingTarget();
        BazarrServiceMock
            .Setup(b => b.ReadyToTranslate(MediaType.Movie, radarrId, null, It.IsAny<IReadOnlyCollection<string>>()))
            .ReturnsAsync(false);

        var result = await Processor.ProcessMedia(await DbContext.Movies.FindAsync(1), MediaType.Movie);

        Assert.False(result);
        TranslationRequestServiceMock.Verify(t => t.CreateRequest(It.IsAny<TranslateAbleSubtitle>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMedia_WhenBazarrIsNotReady_ShouldLeaveHashSoTheNextCycleRetries()
    {
        await ArrangeMovieMissingTarget();
        BazarrServiceMock
            .Setup(b => b.ReadyToTranslate(It.IsAny<MediaType>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<IReadOnlyCollection<string>>()))
            .ReturnsAsync(false);

        await Processor.ProcessMedia(await DbContext.Movies.FindAsync(1), MediaType.Movie);

        Assert.Null((await DbContext.Movies.FindAsync(1))!.MediaHash);
    }

    [Fact]
    public async Task ProcessMedia_ForAnEpisode_ShouldPassTheSonarrEpisodeAndSeriesIds()
    {
        var show = new Lingarr.Core.Entities.Show
        {
            SonarrId = 70, Title = "Show", Path = "/tv/show", DateAdded = System.DateTimeOffset.UtcNow
        };
        var season = new Lingarr.Core.Entities.Season { SeasonNumber = 1, Show = show };
        var episode = new Lingarr.Core.Entities.Episode
        {
            SonarrId = 420, EpisodeNumber = 1, Title = "Pilot", Season = season,
            Path = "/tv/show/s01", FileName = "s01e01"
        };
        await DbContext.Episodes.AddAsync(episode);
        await DbContext.SaveChangesAsync();
        SubtitleServiceMock
            .Setup(s => s.GetSubtitles(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new List<Subtitles>
            {
                new() { Path = "/tv/show/s01/s01e01.en.srt", FileName = "s01e01.en", Language = "en", Caption = "", Format = ".srt" }
            });
        SetupStandardSettings();

        await Processor.ProcessMedia(episode, MediaType.Episode);

        BazarrServiceMock.Verify(b => b.ReadyToTranslate(
            MediaType.Episode, 420, 70, It.IsAny<IReadOnlyCollection<string>>()), Times.Once);
    }

    [Fact]
    public async Task ProcessMedia_WhenBazarrIsReady_ShouldAskForTheMissingLanguagesAndTranslate()
    {
        var radarrId = await ArrangeMovieMissingTarget();

        var result = await Processor.ProcessMedia(await DbContext.Movies.FindAsync(1), MediaType.Movie);

        Assert.True(result);
        BazarrServiceMock.Verify(b => b.ReadyToTranslate(
            MediaType.Movie, radarrId, null,
            It.Is<IReadOnlyCollection<string>>(l => l.Count == 1 && l.Contains("ro"))), Times.Once);
        TranslationRequestServiceMock.Verify(t => t.CreateRequest(It.IsAny<TranslateAbleSubtitle>()), Times.Once);
    }
}
