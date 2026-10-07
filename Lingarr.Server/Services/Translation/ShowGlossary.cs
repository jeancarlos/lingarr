using System.Text;

namespace Lingarr.Server.Services.Translation;

/// <summary>
/// A per-show glossary read from a <c>.lingarr-glossary.txt</c> file in the show or movie folder.
/// </summary>
/// <param name="Context">Free-text series context from the first <c># context:</c> line, if any.</param>
/// <param name="Entries">Ordered source-to-target renderings.</param>
public sealed record ShowGlossary(string? Context, IReadOnlyList<KeyValuePair<string, string>> Entries)
{
    public const string GlossaryFileName = ".lingarr-glossary.txt";
    private const string ContextPrefix = "# context:";
    private const string Separator = " = ";

    /// <summary>
    /// Parses glossary text: one optional "# context:" line (first wins), "Source = Target" pairs,
    /// ignoring other comments, blank and malformed lines.
    /// </summary>
    public static ShowGlossary Parse(string text)
    {
        string? context = null;
        var entries = new List<KeyValuePair<string, string>>();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith('#'))
            {
                if (context is null && line.StartsWith(ContextPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var value = line[ContextPrefix.Length..].Trim();
                    if (value.Length > 0)
                    {
                        context = value;
                    }
                }

                continue;
            }

            var separatorIndex = line.IndexOf(Separator, StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var source = line[..separatorIndex].Trim();
            var target = line[(separatorIndex + Separator.Length)..].Trim();
            if (source.Length == 0 || target.Length == 0)
            {
                continue;
            }

            entries.Add(new KeyValuePair<string, string>(source, target));
        }

        return new ShowGlossary(context, entries);
    }

    /// <summary>
    /// Loads and parses the glossary file from the given folder.
    /// </summary>
    /// <returns>The parsed glossary, or null when the folder or file does not exist.</returns>
    public static ShowGlossary? Load(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        var path = Path.Combine(folder, GlossaryFileName);
        return File.Exists(path) ? Parse(File.ReadAllText(path)) : null;
    }

    /// <summary>
    /// Renders the glossary as a prompt block; empty when there is nothing to render.
    /// </summary>
    public string Render(string targetLanguage)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(Context))
        {
            builder.Append("Series context: ").Append(Context).Append('\n');
        }

        if (Entries.Count > 0)
        {
            builder.Append("Always use these established ").Append(targetLanguage).Append(" renderings:\n");
            foreach (var entry in Entries)
            {
                builder.Append("- ").Append(entry.Key).Append(" → ").Append(entry.Value).Append('\n');
            }
        }

        return builder.ToString();
    }
}
