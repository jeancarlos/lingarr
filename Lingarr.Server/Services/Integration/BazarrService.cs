using System.Collections.Concurrent;
using Lingarr.Core.Enum;
using Lingarr.Server.Interfaces.Services.Integration;

namespace Lingarr.Server.Services.Integration;

public class BazarrService : IBazarrService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BazarrService> _logger;
    private readonly string? _url;
    private readonly string? _apiKey;
    private readonly TimeSpan _wait;
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<(MediaType, int), DateTimeOffset> _searchedAt = new();

    public BazarrService(IHttpClientFactory httpClientFactory, ILogger<BazarrService> logger)
        : this(
            httpClientFactory.CreateClient(nameof(BazarrService)),
            logger,
            Environment.GetEnvironmentVariable("BAZARR_URL"),
            Environment.GetEnvironmentVariable("BAZARR_API_KEY"),
            TimeSpan.FromMinutes(int.TryParse(Environment.GetEnvironmentVariable("BAZARR_RETRY_WAIT_MINUTES"), out var m) ? m : 30),
            TimeProvider.System)
    {
    }

    public BazarrService(
        HttpClient httpClient,
        ILogger<BazarrService> logger,
        string? url,
        string? apiKey,
        TimeSpan wait,
        TimeProvider clock)
    {
        _httpClient = httpClient;
        _logger = logger;
        _url = url?.TrimEnd('/');
        _apiKey = apiKey;
        _wait = wait;
        _clock = clock;
    }

    public async Task<bool> ReadyToTranslate(
        MediaType mediaType,
        int arrId,
        int? seriesId,
        IReadOnlyCollection<string> languages)
    {
        if (string.IsNullOrEmpty(_url) || string.IsNullOrEmpty(_apiKey))
        {
            return true;
        }

        var key = (mediaType, arrId);
        if (_searchedAt.TryGetValue(key, out var searchedAt))
        {
            return _clock.GetUtcNow() - searchedAt >= _wait;
        }

        try
        {
            foreach (var language in languages)
            {
                var target = mediaType == MediaType.Movie
                    ? $"movies/subtitles?radarrid={arrId}"
                    : $"episodes/subtitles?seriesid={seriesId}&episodeid={arrId}";
                using var request = new HttpRequestMessage(
                    HttpMethod.Patch,
                    $"{_url}/api/{target}&language={ToBazarrCode(language)}&forced=false&hi=false");
                request.Headers.Add("X-API-KEY", _apiKey);
                using var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Bazarr search request failed for {MediaType} {ArrId}; translation waits for the next cycle",
                mediaType, arrId);
            return false;
        }

        _searchedAt[key] = _clock.GetUtcNow();
        _logger.LogInformation(
            "Asked Bazarr to search {Languages} for {MediaType} {ArrId}; translating after {Wait} minutes if still missing",
            string.Join(", ", languages), mediaType, arrId, _wait.TotalMinutes);
        return false;
    }

    public static string ToBazarrCode(string language) => language.ToLowerInvariant() switch
    {
        "pt-br" => "pb",
        "zh-tw" => "zt",
        var code => code.Split('-')[0]
    };
}
