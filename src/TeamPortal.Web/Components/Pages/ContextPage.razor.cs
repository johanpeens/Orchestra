using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Services;

namespace TeamPortal.Web.Components.Pages;

public partial class ContextPage : ComponentBase
{
    /// <summary>
    /// Indented, and nulls left in. Both are deliberate: this is read by a person
    /// deciding whether it looks right before it is read by a machine, and a
    /// missing property is harder to interpret than an explicit null.
    /// </summary>
    private static readonly JsonSerializerOptions Format = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private PortalExportDto? _export;
    private string _json = string.Empty;
    private bool _includeImageData;
    private bool _loading = true;
    private bool _copied;
    private string? _error;

    [Inject]
    private IPortalExportService Export { get; set; } = null!;

    [Inject]
    private IJSRuntime Js { get; set; } = null!;

    /// <summary>Rough, and rounded - it is a warning about size, not a measurement.</summary>
    private string SizeLabel
    {
        get
        {
            long bytes = System.Text.Encoding.UTF8.GetByteCount(_json);

            return bytes < 1024 ? $"{bytes} B"
                : bytes < 1024 * 1024 ? $"{bytes / 1024.0:0.#} KB"
                : $"{bytes / 1024.0 / 1024.0:0.#} MB";
        }
    }

    /// <summary>
    /// "1 folder", not "1 folders". The counts line is the first thing anyone
    /// reads on this page, and getting it wrong there reads as carelessness
    /// about everything below it.
    /// </summary>
    private static string Plural(int count, string one, string? many = null) =>
        count == 1 ? $"1 {one}" : $"{count} {many ?? one + "s"}";

    /// <summary>
    /// One node of the tree as its own JSON, serialised on demand rather than
    /// up front. A closed accordion row costs nothing this way, and almost all
    /// of them are closed almost all of the time.
    /// </summary>
    private static string Node<T>(T node) => JsonSerializer.Serialize(node, Format);

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task SetIncludeImagesAsync(bool include)
    {
        _includeImageData = include;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _copied = false;
        _error = null;

        try
        {
            _export = await Export.ExportAsync(_includeImageData);
            _json = JsonSerializer.Serialize(_export, Format);
        }
        catch (InvalidOperationException ex)
        {
            _error = $"The context could not be built. {ex.Message}";
            _json = string.Empty;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task CopyAsync()
    {
        try
        {
            await Js.InvokeVoidAsync("navigator.clipboard.writeText", _json);
            _copied = true;
        }
        catch (JSException)
        {
            // Clipboard access is refused on an insecure origin and in some
            // locked-down browsers. The JSON is on screen and selectable, so this
            // is a convenience that failed rather than a broken page.
            _error = "The browser would not let us write to the clipboard. " +
                     "Select the text below and copy it instead.";
        }
    }
}
