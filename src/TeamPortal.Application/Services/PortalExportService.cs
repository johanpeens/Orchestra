using System.Globalization;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;

namespace TeamPortal.Application.Services;

public interface IPortalExportService
{
    /// <summary>
    /// Every folder, process, step, owner, department and status.
    /// </summary>
    /// <param name="includeImageData">
    /// Reads each screenshot off disk and embeds it as a data URI. Off by
    /// default: a handful of images turns a readable page into megabytes, and
    /// most of the time the URL is enough.
    /// </param>
    Task<PortalExportDto> ExportAsync(
        bool includeImageData = false, CancellationToken ct = default);
}

internal sealed class PortalExportService : IPortalExportService
{
    private readonly IProcessFolderService _folders;
    private readonly IProcessService _processes;
    private readonly IProcessStepService _steps;
    private readonly IDepartmentService _departments;
    private readonly IEmployeeService _employees;
    private readonly IStepStatusService _statuses;
    private readonly IFileReader? _files;

    public PortalExportService(
        IProcessFolderService folders,
        IProcessService processes,
        IProcessStepService steps,
        IDepartmentService departments,
        IEmployeeService employees,
        IStepStatusService statuses,
        IFileReader? files = null)
    {
        _folders = folders;
        _processes = processes;
        _steps = steps;
        _departments = departments;
        _employees = employees;
        _statuses = statuses;
        _files = files;
    }

    public async Task<PortalExportDto> ExportAsync(
        bool includeImageData = false, CancellationToken ct = default)
    {
        IReadOnlyList<ProcessFolderDto> folders = await _folders.GetWithCountsAsync(ct);

        var exported = new List<ExportFolderDto>(folders.Count);

        int processCount = 0;
        int stepCount = 0;
        int screenshotCount = 0;

        foreach (ProcessFolderDto folder in folders)
        {
            IReadOnlyList<ProcessListItemDto> processes =
                await _processes.GetByFolderAsync(folder.Id, ct);

            var exportedProcesses = new List<ExportProcessDto>(processes.Count);

            foreach (ProcessListItemDto process in processes)
            {
                IReadOnlyList<ProcessStepDto> steps =
                    await _steps.GetByProcessAsync(process.Id, ct);

                var exportedSteps = new List<ExportStepDto>(steps.Count);

                foreach (ProcessStepDto step in steps)
                {
                    IReadOnlyList<ExportScreenshotDto> screenshots =
                        Screenshots(step, includeImageData);

                    screenshotCount += screenshots.Count;

                    exportedSteps.Add(new ExportStepDto(
                        step.Id,
                        step.StepNumber,
                        step.Name,
                        step.Description,
                        step.DepartmentName,
                        step.StatusName,
                        [.. step.Owners.Select(Describe)],
                        screenshots));
                }

                stepCount += exportedSteps.Count;

                exportedProcesses.Add(new ExportProcessDto(
                    process.Id,
                    process.SortOrder,
                    process.Name,
                    process.Description,
                    process.DepartmentName,
                    process.StatusName,
                    exportedSteps));
            }

            processCount += exportedProcesses.Count;

            exported.Add(new ExportFolderDto(folder.Id, folder.Name, exportedProcesses));
        }

        IReadOnlyList<DepartmentListItemDto> departments = await _departments.GetWithCountsAsync(ct);
        IReadOnlyList<StepStatusDto> statuses = await _statuses.GetAllAsync(ct);
        IReadOnlyList<EmployeeCardDto> people = await _employees.GetAllAsync(ct);

        return new PortalExportDto(
            DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            folders.Count,
            processCount,
            stepCount,
            screenshotCount,
            exported,
            [.. departments.Select(d =>
                new ExportDepartmentDto(d.Id, d.Name, d.ColourHex, d.EmployeeCount))],
            [.. statuses.Select(s => new ExportStatusDto(s.Id, s.Name, s.ColourHex))])
        {
            People =
            [
                .. people.Select(p => new ExportPersonDto(
                    p.Id,
                    p.FullName,
                    p.JobTitle,
                    p.DepartmentName,
                    p.StaffType.ToString()))
            ]
        };
    }

    /// <summary>"Sarah (Project Manager)", or just the name when there is no title.</summary>
    private static string Describe(StepOwnerDto owner) =>
        string.IsNullOrWhiteSpace(owner.JobTitle)
            ? owner.FullName
            : $"{owner.FullName} ({owner.JobTitle})";

    private IReadOnlyList<ExportScreenshotDto> Screenshots(
        ProcessStepDto step, bool includeImageData) =>
    [
        .. step.Images.Select(image => new ExportScreenshotDto(
            image.FileName,
            image.Url,
            image.SizeBytes,
            image.UploadedAt,
            includeImageData ? TryReadAsDataUri(image) : null))
    ];

    /// <summary>
    /// The image as a data URI, or null if it cannot be read. A missing file is
    /// not worth failing the whole export over - the URL is still there, and the
    /// caller can see for themselves that the data is absent.
    /// </summary>
    private string? TryReadAsDataUri(StepImageDto image)
    {
        if (_files is null)
        {
            return null;
        }

        try
        {
            using Stream? content = _files.OpenRead(MediaPaths.StoredPath(image.Url));

            if (content is null)
            {
                return null;
            }

            using var buffer = new MemoryStream();
            content.CopyTo(buffer);

            return $"data:{ContentTypeFor(image.FileName)};base64,{Convert.ToBase64String(buffer.ToArray())}";
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Turns "/media/steps/abc.png" back into "steps/abc.png". The DTO carries
    /// the URL because that is what the UI needs; the reader wants the path it
    /// was stored under.
    /// </summary>
    private static string StoredPathFrom(string url) =>
        url.StartsWith("/media/", StringComparison.OrdinalIgnoreCase)
            ? url["/media/".Length..]
            : url.TrimStart('/');

    private static string ContentTypeFor(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
}
