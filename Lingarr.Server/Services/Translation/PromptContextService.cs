using Lingarr.Core.Data;
using Lingarr.Core.Enum;
using Lingarr.Server.Interfaces.Services.Translation;
using Lingarr.Server.Services.Translation.Base;
using Microsoft.EntityFrameworkCore;

namespace Lingarr.Server.Services.Translation;

public class PromptContextService : IPromptContextService
{
    private readonly LingarrDbContext _dbContext;

    public PromptContextService(LingarrDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<PromptContext?> GetPromptContext(
        int? mediaId,
        MediaType mediaType,
        CancellationToken cancellationToken)
    {
        if (mediaId is null)
        {
            return null;
        }

        switch (mediaType)
        {
            case MediaType.Movie:
                var movie = await _dbContext.Movies
                    .Where(m => m.Id == mediaId)
                    .Select(m => new { m.Title, m.Path })
                    .FirstOrDefaultAsync(cancellationToken);
                return movie is null ? null : new PromptContext(movie.Title, ShowGlossary.Load(movie.Path));
            case MediaType.Episode:
                var show = await _dbContext.Episodes
                    .Where(e => e.Id == mediaId)
                    .Select(e => new { e.Season.Show.Title, e.Season.Show.Path })
                    .FirstOrDefaultAsync(cancellationToken);
                return show is null ? null : new PromptContext(show.Title, ShowGlossary.Load(show.Path));
            default:
                return null;
        }
    }

    /// <summary>
    /// Applies the prompt context to every LLM-backed service in the fallback chain.
    /// </summary>
    public static void Apply(IEnumerable<TranslationServiceEntry> services, PromptContext? promptContext)
    {
        if (promptContext is null)
        {
            return;
        }

        foreach (var entry in services)
        {
            if (entry.Service is BaseLanguageService languageService)
            {
                languageService.SetPromptContext(promptContext);
            }
        }
    }
}
