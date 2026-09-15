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

        var objectStart = content.IndexOf('{');
        var arrayStart = content.IndexOf('[');

        // A leading array means the model skipped the wrapper object entirely.
        if (arrayStart != -1 && (objectStart == -1 || arrayStart < objectStart))
        {
            var arrayEnd = content.LastIndexOf(']');
            return arrayEnd > arrayStart
                ? $"{{\"translations\":{content.Substring(arrayStart, arrayEnd - arrayStart + 1)}}}"
                : content;
        }

        var objectEnd = content.LastIndexOf('}');
        return objectStart != -1 && objectEnd > objectStart
            ? content.Substring(objectStart, objectEnd - objectStart + 1)
            : content;
    }
}
