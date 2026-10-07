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
    private readonly ConcurrentDictionary<(MediaType, int, string), DateTimeOffset> _searchedAt = new();

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

        var unsearched = languages.Where(language => !_searchedAt.ContainsKey((mediaType, arrId, language))).ToList();
        if (unsearched.Count == 0)
        {
            var now = _clock.GetUtcNow();
            return languages.All(language => now - _searchedAt[(mediaType, arrId, language)] >= _wait);
        }

        try
        {
            foreach (var language in unsearched)
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

        var searchedAt = _clock.GetUtcNow();
        foreach (var language in unsearched)
        {
            _searchedAt[(mediaType, arrId, language)] = searchedAt;
        }

        _logger.LogInformation(
            "Asked Bazarr to search {Languages} for {MediaType} {ArrId}; translating after {Wait} minutes if still missing",
            string.Join(", ", unsearched), mediaType, arrId, _wait.TotalMinutes);
        return false;
    }

    public static string ToBazarrCode(string language) => language.ToLowerInvariant() switch
    {
        "pt-br" => "pb",
        "zh-tw" => "zt",
        var code => code.Split('-')[0]
    };
}
