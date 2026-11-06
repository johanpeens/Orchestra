using System.Text.Json;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Services;
using TeamPortal.Services.Tests.Common;

namespace TeamPortal.Services.Tests.Contracts;

public abstract class PortalExportContractTests : IDisposable
{
    protected PortalExportContractTests() => Fixture = CreateFixture();

    protected ServiceFixture Fixture { get; }

    protected abstract ServiceFixture CreateFixture();

    private IPortalExportService Sut => Fixture.Get<IPortalExportService>();

    /// <summary>Cached - CA1869, and the same options the page uses.</summary>
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // --- Shape ---------------------------------------------------------------

    [Fact]
    public async Task ExportAsync_IncludesEveryFolder()
    {
        PortalExportDto export = await Sut.ExportAsync();

        export.Folders.Select(f => f.Name)
            .Should().Contain(["The Business", "Client Onboarding"]);
    }

    [Fact]
    public async Task ExportAsync_IncludesTheProcessesInEachFolder()
    {
        PortalExportDto export = await Sut.ExportAsync();

        ExportFolderDto onboarding = export.Folders.Single(f => f.Name == "Client Onboarding");

        onboarding.Processes.Should().HaveCount(7);
        onboarding.Processes.Select(p => p.Name).Should().Contain("Initial Contact");
    }

    [Fact]
    public async Task ExportAsync_IncludesTheStepsOfEachProcess()
    {
        // The steps are the whole point. A chatbot that knows the processes but
        // not what happens inside them cannot answer anything useful.
        PortalExportDto export = await Sut.ExportAsync();

        ExportProcessDto first = export.Folders
            .Single(f => f.Name == "Client Onboarding")
            .Processes.Single(p => p.Name == "Initial Contact");

        first.Steps.Should().HaveCount(4);
        first.Steps.Select(s => s.Name).Should().Contain("Receive the inquiry");
    }

    [Fact]
    public async Task ExportAsync_KeepsTheStepsInOrder()
    {
        // Walking off the end of one process moves on to the next, and that order
        // is the thing that makes this a flow rather than a list.
        PortalExportDto export = await Sut.ExportAsync();

        ExportProcessDto first = export.Folders
            .Single(f => f.Name == "Client Onboarding")
            .Processes.Single(p => p.Name == "Initial Contact");

        first.Steps.Select(s => s.Number).Should().Equal([1, 2, 3, 4]);
    }

    [Fact]
    public async Task ExportAsync_KeepsTheProcessesInOrder()
    {
        PortalExportDto export = await Sut.ExportAsync();

        IReadOnlyList<int> positions =
        [
            .. export.Folders.Single(f => f.Name == "Client Onboarding")
                .Processes.Select(p => p.Position)
        ];

        positions.Should().BeInAscendingOrder();
    }

    // --- Spelled out, not keyed ---------------------------------------------

    [Fact]
    public async Task ExportAsync_NamesDepartmentsRatherThanNumberingThem()
    {
        // Whatever reads this has no lookup tables. A departmentId of 3 is
        // useless; "Sales" is not.
        PortalExportDto export = await Sut.ExportAsync();

        ExportProcessDto process = export.Folders
            .SelectMany(f => f.Processes)
            .First(p => p.Department is not null);

        process.Department.Should().NotBeNullOrWhiteSpace();
        process.Department.Should().NotMatchRegex("^[0-9]+$");
    }

    [Fact]
    public async Task ExportAsync_NamesTheStatusRatherThanNumberingIt()
    {
        PortalExportDto export = await Sut.ExportAsync();

        export.Folders.SelectMany(f => f.Processes)
            .Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.Status));
    }

    [Fact]
    public async Task ExportAsync_NamesThePeopleResponsibleForAStep()
    {
        PortalExportDto export = await Sut.ExportAsync();

        ExportStepDto step = export.Folders
            .SelectMany(f => f.Processes)
            .SelectMany(p => p.Steps)
            .First(s => s.ResponsiblePeople.Count > 0);

        step.ResponsiblePeople.Should().Contain(p => p.Contains("Sarah", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExportAsync_IncludesTheJobTitleWithTheName()
    {
        // "Sarah" alone does not tell a chatbot why she owns that step.
        PortalExportDto export = await Sut.ExportAsync();

        ExportStepDto step = export.Folders
            .SelectMany(f => f.Processes)
            .SelectMany(p => p.Steps)
            .First(s => s.ResponsiblePeople.Count > 0);

        step.ResponsiblePeople.Should().Contain(p => p.Contains('(', StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExportAsync_IncludesTheDepartmentAndStatusLookups()
    {
        // So a reader can resolve a colour or check what statuses exist without
        // inferring the set from whatever happens to be in use.
        PortalExportDto export = await Sut.ExportAsync();

        export.Departments.Should().NotBeEmpty();
        export.Statuses.Should().NotBeEmpty();
        export.Statuses.Select(s => s.Name).Should().Contain("Not Started");
    }

    // --- Counts --------------------------------------------------------------

    [Fact]
    public async Task ExportAsync_CountsWhatItContains()
    {
        // The header counts are what the page shows before anyone reads the JSON,
        // so they have to agree with the body.
        PortalExportDto export = await Sut.ExportAsync();

        export.FolderCount.Should().Be(export.Folders.Count);
        export.ProcessCount.Should().Be(export.Folders.Sum(f => f.Processes.Count));
        export.StepCount.Should().Be(
            export.Folders.SelectMany(f => f.Processes).Sum(p => p.Steps.Count));
    }

    [Fact]
    public async Task ExportAsync_StampsWhenItWasBuilt()
    {
        // Context handed to a chatbot goes stale. The timestamp is how anyone
        // reading it later knows how much to trust it.
        PortalExportDto export = await Sut.ExportAsync();

        DateTime.Parse(export.GeneratedAtUtc, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind)
            .Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    // --- Screenshots ---------------------------------------------------------

    [Fact]
    public async Task ExportAsync_ByDefault_LeavesTheImageDataOut()
    {
        // A handful of screenshots turns a readable page into megabytes.
        PortalExportDto export = await Sut.ExportAsync();

        export.Folders
            .SelectMany(f => f.Processes)
            .SelectMany(p => p.Steps)
            .SelectMany(s => s.Screenshots)
            .Should().OnlyContain(i => i.DataUri == null);
    }

    [Fact]
    public async Task ExportAsync_AlwaysGivesEveryScreenshotAUrl()
    {
        PortalExportDto export = await Sut.ExportAsync();

        export.Folders
            .SelectMany(f => f.Processes)
            .SelectMany(p => p.Steps)
            .SelectMany(s => s.Screenshots)
            .Should().OnlyContain(i => !string.IsNullOrWhiteSpace(i.Url));
    }

    // --- It has to actually serialise ---------------------------------------

    [Fact]
    public async Task ExportAsync_ProducesSomethingThatSerialisesAndParses()
    {
        // The page hands this straight to JsonSerializer. A cycle or an
        // unserialisable member would only show up in the browser.
        PortalExportDto export = await Sut.ExportAsync();

        string json = JsonSerializer.Serialize(export, Json);

        json.Should().NotBeNullOrWhiteSpace();

        using JsonDocument parsed = JsonDocument.Parse(json);

        parsed.RootElement.TryGetProperty("folders", out JsonElement folders).Should().BeTrue();
        folders.GetArrayLength().Should().Be(export.Folders.Count);
    }

    [Fact]
    public async Task ExportAsync_CarriesNoCredentials()
    {
        // This object is meant to be pasted somewhere else. Anything secret in it
        // leaves with it.
        PortalExportDto export = await Sut.ExportAsync();

        string json = JsonSerializer.Serialize(export, Json).ToUpperInvariant();

        foreach (string forbidden in new[] { "PASSWORDHASH", "SECURITYSTAMP", "TOKEN", "PASSWORD" })
        {
            json.Should().NotContain(forbidden);
        }
    }

    public void Dispose()
    {
        Fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>Part A: over the in-memory services.</summary>
public sealed class MockPortalExportTests : PortalExportContractTests
{
    protected override ServiceFixture CreateFixture() => new MockServiceFixture();
}

/// <summary>Part B: the same export, over EF Core.</summary>
public sealed class SqlPortalExportTests : PortalExportContractTests
{
    protected override ServiceFixture CreateFixture() => new SqlServiceFixture();
}
