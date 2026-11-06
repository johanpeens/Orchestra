using TeamPortal.Domain.Identity;

namespace TeamPortal.Domain.Entities;

public class StepImage
{
    public int Id { get; set; }

    public int ProcessStepId { get; set; }

    public ProcessStep ProcessStep { get; set; } = null!;

    /// <summary>Original filename, kept for the download prompt only - never used on disk.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// A generated relative path or blob key. Never the caller's filename and
    /// never a full disk path - see IFileStore.SaveAsync.
    /// </summary>
    public string StoredPath { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public int SortOrder { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public int? UploadedByUserId { get; set; }

    public AppUser? UploadedByUser { get; set; }
}
