using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lingarr.Server.Models.FileSystem;
using Lingarr.Server.Services.Subtitle;
using Xunit;

namespace Lingarr.Server.Tests.Services.Subtitle;

public class SubtitleCueCollapserTests
{
    private static SubtitleItem Cue(int position, int start, int end, string text) => new()
    {
        Position = position,
        StartTime = start,
        EndTime = end,
        Lines = [text],
        PlaintextLines = [text],
        TranslatedLines = []
    };

    [Fact]
    public void Collapse_MergesInterleavedKaraokeFramesIntoOneCuePerLine()
    {
        var frames = new List<SubtitleItem>
        {
            Cue(1, 0, 100, "Come and save me"), Cue(2, 0, 100, "You refill my place"),
            Cue(3, 100, 200, "Come and save me"), Cue(4, 100, 200, "You refill my place"),
            Cue(5, 200, 300, "Come and save me")
        };

        var result = SubtitleCueCollapser.Collapse(frames);

        Assert.Equal(2, result.Count);
        Assert.Equal(("Come and save me", 0, 300), (result[0].PlaintextLines[0], result[0].StartTime, result[0].EndTime));
        Assert.Equal(("You refill my place", 0, 200), (result[1].PlaintextLines[0], result[1].StartTime, result[1].EndTime));
        Assert.Equal([1, 2], result.Select(c => c.Position));
    }

    [Fact]
    public void Collapse_KeepsTheSameLineSpokenAgainAfterAGap()
    {
        var cues = new List<SubtitleItem> { Cue(1, 0, 1000, "Naruto!"), Cue(2, 5000, 6000, "Naruto!") };

        var result = SubtitleCueCollapser.Collapse(cues);

        Assert.Equal(2, result.Count);
        Assert.Equal([0, 5000], result.Select(c => c.StartTime));
    }

    [Fact]
    public void Collapse_LeavesOrdinaryDialogueUnchanged()
    {
        var cues = new List<SubtitleItem>
        {
            Cue(1, 0, 1500, "Where do you think you're going?"),
            Cue(2, 1600, 3000, "I won't let you take her!"),
            Cue(3, 3100, 4000, "Then fight me.")
        };

        var result = SubtitleCueCollapser.Collapse(cues);

        Assert.Equal(cues.Select(c => (c.Position, c.StartTime, c.EndTime, c.PlaintextLines[0])),
                     result.Select(c => (c.Position, c.StartTime, c.EndTime, c.PlaintextLines[0])));
    }

    [Fact]
    public async Task EnforceLimit_RejectsSubtitlesOverTheCap()
    {
        var cues = Enumerable.Range(1, 11).Select(i => Cue(i, i * 2000, i * 2000 + 1000, $"line {i}")).ToList();

        var ex = Assert.Throws<TaskCanceledException>(() => SubtitleCueCollapser.EnforceLimit(cues, 10));

        Assert.Contains("11", ex.Message);
        await Task.CompletedTask;
    }

    [Fact]
    public void EnforceLimit_ZeroDisablesTheCap()
    {
        var cues = Enumerable.Range(1, 50).Select(i => Cue(i, i * 2000, i * 2000 + 1000, $"line {i}")).ToList();

        SubtitleCueCollapser.EnforceLimit(cues, 0);
        SubtitleCueCollapser.EnforceLimit(cues, 50);
    }

    private static List<SubtitleItem> Distinct(int count) =>
        Enumerable.Range(1, count).Select(i => Cue(i, i * 2000, i * 2000 + 1000, $"line {i}")).ToList();

    [Fact]
    public void Prepare_UsesTheConfiguredLimit()
    {
        var settings = new Dictionary<string, string> { ["max_subtitle_cues"] = "2" };

        Assert.Throws<TaskCanceledException>(() => SubtitleCueCollapser.Prepare(Distinct(3), settings));
    }

    [Fact]
    public void Prepare_DefaultsTo3000WhenTheSettingIsMissing()
    {
        var settings = new Dictionary<string, string>();

        Assert.Equal(3000, SubtitleCueCollapser.Prepare(Distinct(3000), settings).Count);
        Assert.Throws<TaskCanceledException>(() => SubtitleCueCollapser.Prepare(Distinct(3001), settings));
    }

    [Fact]
    public void Prepare_ZeroDisablesTheCap()
    {
        var settings = new Dictionary<string, string> { ["max_subtitle_cues"] = "0" };

        Assert.Equal(5000, SubtitleCueCollapser.Prepare(Distinct(5000), settings).Count);
    }

    [Fact]
    public void Prepare_CollapsesBeforeCounting()
    {
        var frames = Enumerable.Range(0, 10).Select(i => Cue(i + 1, i * 100, i * 100 + 100, "Come and save me")).ToList();
        var settings = new Dictionary<string, string> { ["max_subtitle_cues"] = "2" };

        var result = SubtitleCueCollapser.Prepare(frames, settings);

        Assert.Equal(("Come and save me", 0, 1000), (Assert.Single(result).PlaintextLines[0], result[0].StartTime, result[0].EndTime));
    }
}
