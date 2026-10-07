using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Lingarr.Core.Enum;
using Lingarr.Server.Services.Integration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Lingarr.Server.Tests.Services;

public class BazarrServiceTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Recorder : HttpMessageHandler
    {
        public readonly List<HttpRequestMessage> Requests = new();
        public HttpStatusCode Status = HttpStatusCode.NoContent;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(Status));
        }
    }

    private readonly Clock _clock = new();
    private readonly Recorder _http = new();

    private BazarrService Service(string? url = "http://bazarr:6767/") =>
        new(new HttpClient(_http), NullLogger<BazarrService>.Instance, url, "key", TimeSpan.FromMinutes(30), _clock);

    [Fact]
    public async Task FirstSighting_AsksBazarrPerLanguageAndHoldsTranslation()
    {
        var service = Service();

        var ready = await service.ReadyToTranslate(MediaType.Episode, 42, 7, new[] { "pt-BR", "es" });

        Assert.False(ready);
        Assert.Equal(2, _http.Requests.Count);
        Assert.Equal(HttpMethod.Patch, _http.Requests[0].Method);
        Assert.Equal(
            "http://bazarr:6767/api/episodes/subtitles?seriesid=7&episodeid=42&language=pb&forced=false&hi=false",
            _http.Requests[0].RequestUri!.ToString());
        Assert.Equal("es", System.Web.HttpUtility.ParseQueryString(_http.Requests[1].RequestUri!.Query)["language"]);
        Assert.Equal("key", string.Join("", _http.Requests[0].Headers.GetValues("X-API-KEY")));
    }

    [Fact]
    public async Task Movie_UsesTheRadarrEndpoint()
    {
        await Service().ReadyToTranslate(MediaType.Movie, 5, null, new[] { "pt-BR" });

        Assert.Equal(
            "http://bazarr:6767/api/movies/subtitles?radarrid=5&language=pb&forced=false&hi=false",
            _http.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task HoldsDuringTheWaitAndReleasesAfterItWithoutAskingAgain()
    {
        var service = Service();
        await service.ReadyToTranslate(MediaType.Movie, 5, null, new[] { "pt-BR" });

        _clock.Now += TimeSpan.FromMinutes(29);
        Assert.False(await service.ReadyToTranslate(MediaType.Movie, 5, null, new[] { "pt-BR" }));

        _clock.Now += TimeSpan.FromMinutes(1);
        Assert.True(await service.ReadyToTranslate(MediaType.Movie, 5, null, new[] { "pt-BR" }));
        Assert.Single(_http.Requests);
    }

    [Fact]
    public async Task FailedRequest_HoldsAndRetriesOnTheNextCycle()
    {
        var service = Service();
        _http.Status = HttpStatusCode.InternalServerError;

        Assert.False(await service.ReadyToTranslate(MediaType.Movie, 5, null, new[] { "pt-BR" }));

        _http.Status = HttpStatusCode.NoContent;
        _clock.Now += TimeSpan.FromHours(5);
        Assert.False(await service.ReadyToTranslate(MediaType.Movie, 5, null, new[] { "pt-BR" }));
        Assert.Equal(2, _http.Requests.Count);
    }

    [Fact]
    public async Task Unconfigured_NeverHoldsTranslation()
    {
        Assert.True(await Service(url: null).ReadyToTranslate(MediaType.Movie, 5, null, new[] { "pt-BR" }));
        Assert.Empty(_http.Requests);
    }

    [Theory]
    [InlineData("pt-BR", "pb")]
    [InlineData("zh-TW", "zt")]
    [InlineData("en", "en")]
    [InlineData("es-MX", "es")]
    public void MapsLingarrCodesToBazarrCodes(string lingarr, string bazarr) =>
        Assert.Equal(bazarr, BazarrService.ToBazarrCode(lingarr));

    [Fact]
    public async Task NewLanguageForAnAlreadySearchedItem_AsksBazarrForThatLanguage()
    {
        var service = Service();
        await service.ReadyToTranslate(MediaType.Episode, 42, 7, new[] { "pt-BR" });
        _clock.Now += TimeSpan.FromMinutes(31);

        var ready = await service.ReadyToTranslate(MediaType.Episode, 42, 7, new[] { "pt-BR", "es" });

        Assert.False(ready);
        Assert.Equal(2, _http.Requests.Count);
        Assert.Equal("es", System.Web.HttpUtility.ParseQueryString(_http.Requests[1].RequestUri!.Query)["language"]);
    }
}
