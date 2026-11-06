namespace TeamPortal.Application.Common;

/// <summary>
/// The declared content type is the caller's claim. These bytes are the evidence.
/// </summary>
public static class FileSignature
{
    public static bool Matches(byte[] bytes, string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/png" => IsPng(bytes),
            "image/jpeg" => IsJpeg(bytes),
            "image/webp" => IsWebp(bytes),
            _ => false
        };

    // 89 50 4E 47 0D 0A 1A 0A
    private static bool IsPng(byte[] b) =>
        b.Length >= 8 &&
        b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 &&
        b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A;

    // FF D8 FF
    private static bool IsJpeg(byte[] b) =>
        b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF;

    // "RIFF" .... "WEBP"
    private static bool IsWebp(byte[] b) =>
        b.Length >= 12 &&
        b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 &&
        b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50;

    /// <summary>Builds a minimal valid header of the given type. Used by tests.</summary>
    public static byte[] HeaderFor(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/png" => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0],
        "image/jpeg" => [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0],
        "image/webp" => [0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50],
        _ => [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]
    };
}
