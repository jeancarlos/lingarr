using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Lingarr.Core.Data;
using Lingarr.Core.Entities;
using Lingarr.Core.Enum;
using Lingarr.Server.Services.Translation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Lingarr.Server.Tests.Services.Translation;

public class PromptContextServiceTests
{
    private static LingarrDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<LingarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LingarrDbContext(options);
    }

    private static string CreateFolderWithGlossary()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(folder, ".lingarr-glossary.txt"),
            "# context: Ninja anime.\nLeaf Village = Aldeia da Folha\n");
        return folder;
    }

    [Fact]
    public async Task GetPromptContext_ShouldReturnNull_WhenMediaIdIsNull()
    {
        await using var dbContext = CreateDbContext();
        var service = new PromptContextService(dbContext);

        Assert.Null(await service.GetPromptContext(null, MediaType.Movie, CancellationToken.None));
    }

    [Fact]
    public async Task GetPromptContext_ShouldReturnNull_WhenMediaIsUnknown()
    {
        await using var dbContext = CreateDbContext();
        var service = new PromptContextService(dbContext);

        Assert.Null(await service.GetPromptContext(42, MediaType.Movie, CancellationToken.None));
        Assert.Null(await service.GetPromptContext(42, MediaType.Episode, CancellationToken.None));
    }

    [Fact]
    public async Task GetPromptContext_ShouldResolveMovieTitleAndGlossary()
    {
        var folder = CreateFolderWithGlossary();
        try
        {
            await using var dbContext = CreateDbContext();
            dbContext.Movies.Add(new Movie
            {
                RadarrId = 1,
                Title = "The Movie",
                FileName = "movie.mkv",
                Path = folder,
                DateAdded = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();
            var movieId = (await dbContext.Movies.SingleAsync()).Id;
            var service = new PromptContextService(dbContext);

            var context = await service.GetPromptContext(movieId, MediaType.Movie, CancellationToken.None);

            Assert.NotNull(context);
            Assert.Equal("The Movie", context!.Title);
            Assert.NotNull(context.Glossary);
            Assert.Equal("Ninja anime.", context.Glossary!.Context);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task GetPromptContext_ShouldResolveShowTitleAndShowFolder_ForEpisode()
    {
        var folder = CreateFolderWithGlossary();
        try
        {
            await using var dbContext = CreateDbContext();
            var show = new Show
            {
                SonarrId = 1,
                Title = "Naruto Kai",
                Path = folder,
                DateAdded = DateTimeOffset.UtcNow
            };
            var season = new Season { SeasonNumber = 1, Show = show };
            dbContext.Episodes.Add(new Episode
            {
                SonarrId = 10,
                EpisodeNumber = 1,
                Title = "Enter Naruto!",
                Season = season
            });
            await dbContext.SaveChangesAsync();
            var episodeId = (await dbContext.Episodes.SingleAsync()).Id;
            var service = new PromptContextService(dbContext);

            var context = await service.GetPromptContext(episodeId, MediaType.Episode, CancellationToken.None);

            Assert.NotNull(context);
            Assert.Equal("Naruto Kai", context!.Title);
            Assert.NotNull(context.Glossary);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task GetPromptContext_ShouldReturnTitleWithoutGlossary_WhenFileIsAbsent()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            await using var dbContext = CreateDbContext();
            dbContext.Movies.Add(new Movie
            {
                RadarrId = 2,
                Title = "Plain Movie",
                FileName = "plain.mkv",
                Path = folder,
                DateAdded = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();
            var movieId = (await dbContext.Movies.SingleAsync()).Id;
            var service = new PromptContextService(dbContext);

            var context = await service.GetPromptContext(movieId, MediaType.Movie, CancellationToken.None);

            Assert.NotNull(context);
            Assert.Equal("Plain Movie", context!.Title);
            Assert.Null(context.Glossary);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }
}
