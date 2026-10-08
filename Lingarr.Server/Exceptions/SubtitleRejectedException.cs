namespace Lingarr.Server.Exceptions;

/// <summary>
/// Raised when a source subtitle is refused before translation (validation or the cue cap).
/// It stays a cancellation, but unlike a user cancel it keeps the media hash, because the same
/// unchanged file would be refused again and automation would otherwise queue it every run.
/// </summary>
public class SubtitleRejectedException(string message) : TaskCanceledException(message);
