namespace Lingarr.Server.Services.Translation;

/// <summary>
/// Per-request prompt context: the media title and the optional per-show glossary.
/// </summary>
/// <param name="Title">The show or movie title.</param>
/// <param name="Glossary">The glossary loaded from the media folder, if one exists.</param>
public sealed record PromptContext(string? Title, ShowGlossary? Glossary);
