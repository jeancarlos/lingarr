namespace Lingarr.Server.Services.Translation;

/// <summary>
/// Parses the newline separated "Name: Value" header list configured for a translation service.
/// </summary>
public static class CustomHeaderParser
{
    /// <summary>
    /// Reads a header block into name/value pairs, skipping blank lines, comments and malformed entries.
    /// </summary>
    /// <param name="headers">Header block, one "Name: Value" pair per line.</param>
    /// <returns>The parsed headers in the order they were declared.</returns>
    public static IReadOnlyList<(string Name, string Value)> Parse(string? headers)
    {
        if (string.IsNullOrWhiteSpace(headers))
        {
            return [];
        }

        var parsed = new List<(string Name, string Value)>();
        foreach (var rawLine in headers.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            var name = line[..separator].Trim();
            if (name.Length == 0)
            {
                continue;
            }

            parsed.Add((name, line[(separator + 1)..].Trim()));
        }

        return parsed;
    }
}
