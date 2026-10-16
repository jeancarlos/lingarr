using System.Text.RegularExpressions;

namespace Lingarr.Server.Services.Translation;

/// <summary>
/// Normalizes the raw content of a structured output completion into the JSON object the parser expects.
/// </summary>
public static class StructuredResponseNormalizer
{
    /// <summary>
    /// Strips markdown fences and surrounding prose, and wraps a bare translations array in its object.
    /// </summary>
    /// <param name="content">Raw message content returned by the model.</param>
    /// <returns>The content narrowed to a JSON object, or the input unchanged when no JSON was found.</returns>
    public static string Normalize(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        content = RepairKeys(content);
        for (var start = 0; start < content.Length; start++)
        {
            var open = content[start];
            if (open != '[' && open != '{')
            {
                continue;
            }

            var end = content.LastIndexOf(open == '[' ? ']' : '}');
            if (end <= start)
            {
                continue;
            }

            var candidate = content.Substring(start, end - start + 1);
            if (!IsJson(candidate))
            {
                continue;
            }

            return open == '[' ? $"{{\"translations\":{candidate}}}" : candidate;
        }

        return content;
    }

    /// <summary>
    /// Restores a mangled position key such as <c>"position:3,</c>, <c>"position3,</c> or <c>"position"&gt;3,</c>.
    /// </summary>
    /// <param name="content">Raw or sliced model output.</param>
    /// <returns>The content with every position key rewritten as <c>"position":N</c>.</returns>
    public static string RepairKeys(string content) =>
        MangledPositionKey.Replace(content, "{\"position\":$1,\"line\":");

    private static readonly Regex MangledPositionKey =
        new(@"\{\s*""position[^\d{}]{0,4}(\d+)\s*,\s*""line""\s*:", RegexOptions.Compiled);

    private static bool IsJson(string candidate)
    {
        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(candidate);
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
