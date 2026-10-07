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
