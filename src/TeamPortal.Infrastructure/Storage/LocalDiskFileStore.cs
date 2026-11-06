using FluentValidation;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;

namespace TeamPortal.Infrastructure.Storage;

public sealed class FileStoreOptions
{
    /// <summary>Absolute path to the folder uploads live under.</summary>
    public string RootPath { get; set; } = string.Empty;
}

internal sealed class LocalDiskFileStore : IFileStore, IFileReader
{
    private const string StepsFolder = "steps";

    private readonly IValidator<FileUpload> _validator;
    private readonly string _root;

    public LocalDiskFileStore(IValidator<FileUpload> validator, FileStoreOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _validator = validator;
        _root = Path.GetFullPath(options.RootPath);
    }

    public async Task<Result<string>> SaveAsync(FileUpload file, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (await _validator.CheckAsync<FileUpload, string>(file, ct) is { } invalid)
        {
            return invalid;
        }

        byte[] bytes;

        using (var buffer = new MemoryStream())
        {
            await file.Content.CopyToAsync(buffer, ct);
            bytes = buffer.ToArray();
        }

        // The declared content type is the caller's claim. The magic number is
        // the evidence. Renaming payload.exe to photo.png changes the first but
        // not the second, which is exactly the attack this blocks.
        if (!FileSignature.Matches(bytes, file.ContentType))
        {
            return Result<string>.Invalid(
                nameof(file.FileName),
                "That file is not the image type it claims to be. Upload a real PNG, JPG or WEBP.");
        }

        // Never reuse the caller's filename on disk. A name like "../../web.config"
        // must not be able to escape anywhere. The original is kept in
        // StepImage.FileName for display only.
        string storedPath = $"{StepsFolder}/{Guid.NewGuid():N}{ExtensionFor(file.ContentType)}";
        string absolute = Resolve(storedPath);

        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        await File.WriteAllBytesAsync(absolute, bytes, ct);

        return Result<string>.Success(storedPath);
    }

    public string GetUrl(string storedPath) => MediaPaths.Url(storedPath);

    public Task<Result> DeleteAsync(string storedPath, CancellationToken ct = default)
    {
        string? absolute = TryResolve(storedPath);

        if (absolute is not null && File.Exists(absolute))
        {
            File.Delete(absolute);
        }

        // Deleting something that is already gone is the outcome the caller
        // wanted, so it is a success either way.
        return Task.FromResult(Result.Success());
    }

    /// <summary>
    /// Opens a stored file for reading, or null if it is not there. Not part of
    /// IFileStore - only MediaController needs it, and only after it has decided
    /// the caller may see the file.
    /// </summary>
    public Stream? OpenRead(string storedPath)
    {
        string? absolute = TryResolve(storedPath);

        return absolute is not null && File.Exists(absolute)
            ? File.OpenRead(absolute)
            : null;
    }

    private static string ExtensionFor(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/webp" => ".webp",
        _ => ".bin"
    };

    /// <summary>
    /// Turns a stored path into an absolute one, refusing anything that lands
    /// outside the configured root. Combining a caller-supplied path with a root
    /// is the classic traversal bug - "../../web.config" resolves cleanly and
    /// points somewhere it must never reach.
    /// </summary>
    private string Resolve(string storedPath) =>
        TryResolve(storedPath)
        ?? throw new InvalidOperationException(
            $"'{storedPath}' resolves outside the configured file store root.");

    private string? TryResolve(string storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return null;
        }

        string candidate = Path.GetFullPath(Path.Combine(_root, storedPath));

        // Compared with the separator appended, so "/data/uploads-evil" does not
        // pass as being inside "/data/uploads".
        string rootWithSeparator =
            _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;

        return candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)
            ? candidate
            : null;
    }
}
