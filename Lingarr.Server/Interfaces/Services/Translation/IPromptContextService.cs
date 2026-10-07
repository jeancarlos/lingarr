using Lingarr.Core.Enum;
using Lingarr.Server.Services.Translation;

namespace Lingarr.Server.Interfaces.Services.Translation;

public interface IPromptContextService
{
    /// <summary>
    /// Resolves the media title and per-show glossary for a translation request.
    /// </summary>
    /// <param name="mediaId">The media entity id from the translation request, if any.</param>
    /// <param name="mediaType">The media type of the request.</param>
    /// <param name="cancellationToken">Token to cancel the lookup.</param>
    /// <returns>The prompt context, or null when the media cannot be resolved.</returns>
    Task<PromptContext?> GetPromptContext(int? mediaId, MediaType mediaType, CancellationToken cancellationToken);
}
