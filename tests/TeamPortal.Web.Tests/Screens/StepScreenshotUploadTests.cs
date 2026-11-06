using System.Reflection;
using TeamPortal.Web.Components.Dialogs;

namespace TeamPortal.Web.Tests.Screens;

public class StepScreenshotUploadTests
{
    private static readonly Type Dialog = typeof(AddStepDialog);

    [Fact]
    public void ThePickedFile_IsNotAHandleIntoTheBrowser()
    {
        // The whole bug in one assertion. An IBrowserFile field means the dialog
        // is holding a pointer to a DOM element it removes moments later.
        FieldInfo field = Dialog
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(f => f.Name.Contains("_pickedFile", StringComparison.Ordinal));

        field.FieldType.FullName.Should().NotContain(
            "IBrowserFile",
            "the input that owns the handle is gone by the time Save runs");
    }

    [Fact]
    public void ThePickedFile_KeepsTheBytes()
    {
        FieldInfo field = Dialog
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(f => f.Name.Contains("_pickedFile", StringComparison.Ordinal));

        Type picked = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;

        IReadOnlyList<string> properties =
        [
            .. picked.GetProperties().Select(p => p.Name)
        ];

        properties.Should().Contain("Content");
        properties.Should().Contain("Name");
        properties.Should().Contain("ContentType");

        picked.GetProperty("Content")!.PropertyType.Should().Be<byte[]>();
    }

    [Fact]
    public void TheFileIsReadWhenItIsPicked_NotWhenItIsSaved()
    {
        // Reading the source is cruder than reflection and catches what
        // reflection cannot: WHERE the read happens.
        string source = Code();

        int pickHandler = source.IndexOf("OnFilePickedAsync", StringComparison.Ordinal);
        int openStream = source.IndexOf("OpenReadStream", StringComparison.Ordinal);

        pickHandler.Should().BeGreaterThan(0);
        openStream.Should().BeGreaterThan(0, "the file still has to be read somewhere");

        // The only OpenReadStream in the file sits inside the pick handler, which
        // is declared before everything that runs at save time.
        source.IndexOf("OpenReadStream", openStream + 1, StringComparison.Ordinal)
            .Should().Be(-1, "one read, in one place");

        openStream.Should().BeGreaterThan(
            pickHandler, "the read belongs inside the handler that runs while the input exists");
    }

    [Fact]
    public void BuildingTheUpload_TouchesNothingInTheBrowser()
    {
        string source = Code();

        int build = source.IndexOf("private FileUpload? BuildUpload", StringComparison.Ordinal);

        build.Should().BeGreaterThan(0);

        // Everything from BuildUpload to the end of the file.
        string tail = source[build..];

        tail.Should().NotContain("OpenReadStream");
        tail.Should().NotContain("IBrowserFile");
    }

    [Fact]
    public void EachSave_GetsItsOwnStream()
    {
        // A MemoryStream read to the end once is empty the second time. A failed
        // save the user retries would otherwise upload nothing at all.
        string source = Code();

        source.Should().Contain(
            "new MemoryStream(_pickedFile.Content",
            "the stream is built per call, not held on the field");
    }

    [Fact]
    public void TheSizeLimitIsCheckedBeforeTheFileIsRead()
    {
        // Otherwise a 500 MB pick is pulled across the circuit and then refused.
        string source = Code();

        int limit = source.IndexOf("MaxSizeBytes", StringComparison.Ordinal);
        int read = source.IndexOf("OpenReadStream", StringComparison.Ordinal);

        limit.Should().BeLessThan(read);
    }

    /// <summary>
    /// The file with its comments stripped. Matching raw source would match the
    /// comments explaining the bug as readily as the bug itself - the first
    /// version of these tests failed for exactly that reason.
    /// </summary>
    private static string Code()
    {
        IEnumerable<string> lines = File.ReadAllLines(FindSource())
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal));

        return string.Join(Environment.NewLine, lines);
    }

    private static string FindSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();

        return Path.Combine(
            directory!.FullName, "src", "TeamPortal.Web", "Components", "Dialogs",
            "AddStepDialog.razor.cs");
    }
}
