using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lingarr.Server.Models.FileSystem;

namespace Lingarr.Server.Services.Subtitle;

public static class SubtitleCueCollapser
{
    private const int MaxGapMs = 500;
    public const int DefaultMaxCues = 3000;

    public static List<SubtitleItem> Collapse(List<SubtitleItem> subtitles)
    {
        var result = new List<SubtitleItem>();
        var lastByText = new Dictionary<string, SubtitleItem>();
        foreach (var cue in subtitles.OrderBy(c => c.StartTime).ThenBy(c => c.Position))
        {
            var key = string.Join("\n", cue.Lines);
            if (lastByText.TryGetValue(key, out var previous) && cue.StartTime <= previous.EndTime + MaxGapMs)
            {
                previous.EndTime = Math.Max(previous.EndTime, cue.EndTime);
                continue;
            }

            result.Add(cue);
            lastByText[key] = cue;
        }

        if (result.Count == subtitles.Count)
        {
            return subtitles;
        }

        for (var i = 0; i < result.Count; i++)
        {
            result[i].Position = i + 1;
        }

        return result;
    }

    public static void EnforceLimit(List<SubtitleItem> subtitles, int limit)
    {
        if (limit > 0 && subtitles.Count > limit)
        {
            throw new TaskCanceledException(
                $"Subtitle has {subtitles.Count} cues after collapsing repeats, above the max_subtitle_cues limit of {limit}.");
        }
    }
}
