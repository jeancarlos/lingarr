using Lingarr.Core.Enum;

namespace Lingarr.Server.Interfaces.Services.Integration;

public interface IBazarrService
{
    Task<bool> ReadyToTranslate(MediaType mediaType, int arrId, int? seriesId, IReadOnlyCollection<string> languages);
}
