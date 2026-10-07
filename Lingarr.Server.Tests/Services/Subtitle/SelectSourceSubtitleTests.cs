using System;
using System.Linq;
using System.IO;
using Lingarr.Server.Models.FileSystem;
using Lingarr.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Lingarr.Server.Tests.Services.Subtitle;

public class SelectSourceSubtitleTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("lingarr-select-").FullName;
    private readonly SubtitleService _service = new(NullLogger<SubtitleService>.Instance, new LanguageCodeService());

    public void Dispose() => Directory.Delete(_dir, true);

    private Subtitles Write(string fileName, int cues, string caption = "")
    {
        var path = Path.Combine(_dir, fileName);
        File.WriteAllText(path, string.Concat(Enumerable.Range(1, cues).Select(i => $"{i}\n00:00:0{i % 10},000 --> 00:00:0{i % 10},500\nline {i}\n\n")));
        return new Subtitles { Path = path, FileName = fileName, Language = "en", Caption = caption, Format = ".srt" };
    }

    [Fact]
    public void SelectSourceSubtitle_SameLanguage_PrefersTheLargerTrackOverListingOrder()
    {
        var signs = Write("ep.en.2.srt", 10, "2");
        var dialogue = Write("ep.en.srt", 300);

        var selected = _service.SelectSourceSubtitle([signs, dialogue], ["en"], "false");

        Assert.Equal(dialogue.Path, selected!.Subtitle.Path);
    }

    [Fact]
    public void SelectSourceSubtitle_IgnoreCaptions_StillPrefersUncaptionedEvenIfSmaller()
    {
        var sdh = Write("ep.en.sdh.srt", 400, "sdh");
        var plain = Write("ep.en.srt", 300);

        var selected = _service.SelectSourceSubtitle([sdh, plain], ["en"], "true");

        Assert.Equal(plain.Path, selected!.Subtitle.Path);
    }
}
