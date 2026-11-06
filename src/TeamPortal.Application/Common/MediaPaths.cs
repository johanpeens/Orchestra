namespace TeamPortal.Application.Common;

public static class MediaPaths
{
    /// <summary>
    /// Served by MediaEndpoints, behind RequireAuthorization. Leading and
    /// trailing slash, so concatenation is all a caller needs - including inside
    /// an EF expression tree, where a method call would not translate.
    /// </summary>
    public const string Prefix = "/media/";

    /// <summary>The URL for a stored path like <c>steps/abc.png</c>.</summary>
    public static string Url(string storedPath) =>
        string.IsNullOrWhiteSpace(storedPath) ? string.Empty : Prefix + storedPath.TrimStart('/');

    /// <summary>
    /// The stored path back out of a URL. Tolerates a value that was never
    /// prefixed, so a row written before this existed still resolves.
    /// </summary>
    public static string StoredPath(string url) =>
        string.IsNullOrWhiteSpace(url)
            ? string.Empty
            : url.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
                ? url[Prefix.Length..]
                : url.TrimStart('/');
}
