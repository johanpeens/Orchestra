using System.Collections.Concurrent;
using FluentValidation;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;

namespace TeamPortal.Mock.Services;

internal sealed class MockFileStore : IFileStore
{
    private readonly ConcurrentDictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    private readonly IValidator<FileUpload> _validator;
    private readonly MockOptions _options;

    public MockFileStore(IValidator<FileUpload> validator, MockOptions options)
    {
        _validator = validator;
        _options = options;
    }

    public async Task<Result<string>> SaveAsync(FileUpload file, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (_options.WriteDelay > TimeSpan.Zero)
        {
            await Task.Delay(_options.WriteDelay, ct);
        }

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
        // must not be able to escape anywhere. The original is kept only for display.
        string storedPath = $"steps/{Guid.NewGuid():N}{ExtensionFor(file.ContentType)}";

        _files[storedPath] = bytes;

        return Result<string>.Success(storedPath);
    }

    public string GetUrl(string storedPath) => MediaPaths.Url(storedPath);

    public Task<Result> DeleteAsync(string storedPath, CancellationToken ct = default)
    {
        _files.TryRemove(storedPath, out _);
        return Task.FromResult(Result.Success());
    }

    /// <summary>Test and diagnostic hook. Not part of IFileStore.</summary>
    public bool Contains(string storedPath) => _files.ContainsKey(storedPath);

    public int Count => _files.Count;

    private static string ExtensionFor(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/webp" => ".webp",
        _ => ".bin"
    };
}
