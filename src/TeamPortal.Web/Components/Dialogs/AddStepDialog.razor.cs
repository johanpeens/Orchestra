using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using MudBlazor;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;
using TeamPortal.Application.Validators;
using TeamPortal.Web.Components.Shared;

namespace TeamPortal.Web.Components.Dialogs;

public partial class AddStepDialog : ComponentBase
{
    private readonly List<int> _ownerIds = [];

    private IReadOnlyList<DepartmentListItemDto> _departments = [];
    private IReadOnlyList<EmployeeOptionDto> _people = [];
    private IReadOnlyList<StepImageDto> _existingImages = [];
    private PickedFile? _pickedFile;
    private string? _uploadError;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private int? _departmentId;
    private bool _showOwnerPicker;

    /// <summary>The Step tab, where the one required field lives.</summary>
    private const int StepTab = 0;

    /// <summary>Which tab is open.</summary>
    private int _activeTab = StepTab;
    private bool _saving;
    private string? _formError;
    private string? _nameError;

    /// <summary>
    /// The counts go on the tab labels, so somebody can see there is a person or
    /// a screenshot attached without opening the tab to find out.
    /// </summary>
    private string OwnerTabLabel =>
        _ownerIds.Count == 0 ? "People" : $"People ({_ownerIds.Count})";

    private string ScreenshotTabLabel =>
        _pickedFile is not null || _existingImages.Count > 0 ? "Screenshot (1)" : "Screenshot";

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Inject]
    private IProcessStepService Steps { get; set; } = null!;

    [Inject]
    private IDepartmentService Departments { get; set; } = null!;

    [Inject]
    private IEmployeeService Employees { get; set; } = null!;

    [Inject]
    private IDialogService Dialogs { get; set; } = null!;

    /// <summary>The process a new step belongs to. Required when adding.</summary>
    [Parameter]
    public int ProcessId { get; set; }

    /// <summary>Null opens the dialog in "add" mode.</summary>
    [Parameter]
    public int? StepId { get; set; }

    private bool IsNew => StepId is null;

    private bool CanSave => !_saving && !string.IsNullOrWhiteSpace(_name);

    /// <summary>
    /// Drives the browser's file picker filter. It is a convenience only - the
    /// real gate is IFileStore, which checks the byte signature, because an
    /// accept attribute is trivially bypassed.
    /// </summary>
    private static string AcceptedTypes =>
        string.Join(',', FileUploadValidator.AllowedContentTypes);

    /// <summary>People not already on the step, so the same person cannot be added twice.</summary>
    private IEnumerable<EmployeeOptionDto> AvailablePeople =>
        _people.Where(p => !_ownerIds.Contains(p.Id));

    protected override async Task OnInitializedAsync()
    {
        _departments = await Departments.GetWithCountsAsync();
        _people = await Employees.GetOptionsAsync();

        if (StepId is not int id)
        {
            return;
        }

        ProcessStepDto? step = await Steps.GetByIdAsync(id);

        if (step is null)
        {
            _formError = "That step could not be found. It may have been deleted.";
            return;
        }

        _name = step.Name;
        _description = step.Description ?? string.Empty;
        _departmentId = step.DepartmentId;
        _ownerIds.AddRange(step.Owners.Select(o => o.EmployeeId));
        _existingImages = step.Images;
    }

    /// <summary>
    /// Rejects the obvious problems here so the user hears about them straight
    /// away rather than after pressing Save. The same rules run again in the
    /// file store, which is the authority.
    /// </summary>
    private async Task OnFilePickedAsync(IBrowserFile? file)
    {
        _uploadError = null;

        if (file is null)
        {
            _pickedFile = null;
            return;
        }

        // Checked before reading, so a 500 MB file is refused rather than pulled
        // across the circuit first.
        if (file.Size > FileUploadValidator.MaxSizeBytes)
        {
            _uploadError =
                $"{file.Name} is too big. The limit is {FileUploadValidator.MaxSizeBytes / 1024 / 1024} MB.";
            _pickedFile = null;
            return;
        }

        if (!FileUploadValidator.AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            _uploadError = "Only PNG, JPG and WEBP images can be uploaded.";
            _pickedFile = null;
            return;
        }

        // Read NOW, not at save time.
        //
        // An IBrowserFile is a handle to a file held by the <input type="file">
        // element in the browser. The markup below replaces that input with a
        // summary row the moment something is picked, so the element - and the
        // JS map it owns - is gone before Save runs. Reading it then throws
        // "Cannot read properties of null (reading '_blazorFilesById')", every
        // time. The bytes have to be taken while the input still exists.
        try
        {
            using var buffer = new MemoryStream();

            await using (Stream source = file.OpenReadStream(FileUploadValidator.MaxSizeBytes))
            {
                await source.CopyToAsync(buffer);
            }

            _pickedFile = new PickedFile(
                file.Name, file.ContentType, buffer.Length, buffer.ToArray());
        }
        catch (Exception ex) when (ex is IOException or JSException)
        {
            // The circuit dropped, or the file changed on disk between the pick
            // and the read. Neither is worth a stack trace on screen.
            _uploadError = "That file could not be read. Try picking it again.";
            _pickedFile = null;
        }
    }

    private void ClearFile()
    {
        _pickedFile = null;
        _uploadError = null;
    }

    private static string FormatSize(long bytes) =>
        bytes < 1024 ? $"{bytes} B"
        : bytes < 1024 * 1024 ? $"{bytes / 1024.0:0.#} KB"
        : $"{bytes / 1024.0 / 1024.0:0.#} MB";

    private void ToggleOwnerPicker() => _showOwnerPicker = !_showOwnerPicker;

    /// <summary>
    /// Feeds the autocomplete. Matches on name and job title, because half the
    /// time you remember what somebody does before you remember what they are
    /// called. Empty text lists everyone, so the control still works as a plain
    /// dropdown for a small team.
    /// </summary>
    private Task<IEnumerable<EmployeeOptionDto>> SearchPeopleAsync(
        string? term, CancellationToken ct)
    {
        IEnumerable<EmployeeOptionDto> available = AvailablePeople;

        if (!string.IsNullOrWhiteSpace(term))
        {
            available = available.Where(p =>
                p.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (p.JobTitle?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return Task.FromResult(available.OrderBy(p => p.FullName, StringComparer.OrdinalIgnoreCase)
            .AsEnumerable());
    }

    private void AddOwner(EmployeeOptionDto? person)
    {
        // Null arrives when the box is cleared, which is not a selection.
        if (person is null)
        {
            return;
        }

        // Guard here as well as in the list the search returns: the validator
        // rejects duplicates, and a silently-dropped selection would confuse.
        if (!_ownerIds.Contains(person.Id))
        {
            _ownerIds.Add(person.Id);
        }

        _showOwnerPicker = false;
        ClearErrors();
    }

    /// <summary>Fallback for the avatar when somebody has no photo.</summary>
    private static string Initials(string fullName)
    {
        string[] parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
        };
    }

    private void RemoveOwner(int employeeId)
    {
        _ownerIds.Remove(employeeId);
        ClearErrors();
    }

    private void ClearErrors()
    {
        _formError = null;
        _nameError = null;
    }

    private async Task SaveAsync()
    {
        if (_saving)
        {
            return;
        }

        ClearErrors();
        _saving = true;

        try
        {
            string? description = string.IsNullOrWhiteSpace(_description) ? null : _description.Trim();

            FileUpload? screenshot = BuildUpload();

            Result result = IsNew
                ? await Steps.CreateAsync(new CreateStepRequest(
                    ProcessId, _name.Trim(), description, _departmentId, _ownerIds, screenshot))
                : await Steps.UpdateAsync(new UpdateStepRequest(
                    StepId!.Value, _name.Trim(), description, _departmentId,
                    OwnerIds: _ownerIds, Screenshot: screenshot));

            if (result.IsFailure)
            {
                foreach (ValidationError error in result.Errors)
                {
                    if (error.Field == nameof(CreateStepRequest.Name))
                    {
                        _nameError ??= error.Message;
                    }
                    else
                    {
                        _formError ??= error.Message;
                    }
                }

                // Send them to the tab the complaint is about. Save lives outside
                // the tabs, so an error on the Step tab is otherwise invisible to
                // somebody who pressed Save from Screenshot.
                if (_nameError is not null)
                {
                    _activeTab = StepTab;
                }

                return;
            }

            MudDialog.Close(DialogResult.Ok(true));
        }
        finally
        {
            _saving = false;
        }
    }

    /// <summary>
    /// Wraps the bytes taken at pick time. A fresh stream every call, so a failed
    /// save that the user retries does not hand the file store a stream that has
    /// already been read to the end.
    /// </summary>
    private FileUpload? BuildUpload() =>
        _pickedFile is null
            ? null
            : new FileUpload(
                _pickedFile.Name,
                _pickedFile.ContentType,
                _pickedFile.Size,
                new MemoryStream(_pickedFile.Content, writable: false));

    /// <summary>
    /// A picked file, already read. Deliberately NOT an IBrowserFile - that is a
    /// handle to a DOM element this dialog removes as soon as a file is chosen.
    /// </summary>
    private sealed record PickedFile(string Name, string ContentType, long Size, byte[] Content);

    private async Task DeleteAsync()
    {
        if (StepId is not int id)
        {
            return;
        }

        bool confirmed = await ConfirmDialog.ShowAsync(
            Dialogs,
            $"Delete step '{_name}'?",
            "Delete step",
            warning: "The steps after it will be renumbered.",
            confirmText: "Delete Step");

        if (!confirmed)
        {
            return;
        }

        _saving = true;

        try
        {
            Result result = await Steps.DeleteAsync(id);

            if (result.IsFailure)
            {
                _formError = result.ErrorSummary;
                return;
            }

            MudDialog.Close(DialogResult.Ok(true));
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel() => MudDialog.Cancel();

    /// <summary>Opens the dialog. Returns true when something was saved or deleted.</summary>
    public static async Task<bool> ShowAsync(
        IDialogService dialogs, int processId, int? stepId = null)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        var parameters = new DialogParameters<AddStepDialog>
        {
            { x => x.ProcessId, processId },
            { x => x.StepId, stepId }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        IDialogReference dialog = await dialogs.ShowAsync<AddStepDialog>(
            stepId is null ? "Add Step" : "Edit Step", parameters, options);

        DialogResult? result = await dialog.Result;

        return result is { Canceled: false, Data: true };
    }
}
