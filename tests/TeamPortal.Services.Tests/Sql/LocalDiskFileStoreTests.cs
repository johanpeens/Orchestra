using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Services.Tests.Common;

namespace TeamPortal.Services.Tests.Sql;

public class LocalDiskFileStoreOnlyTests
{
    private static FileUpload Png(string name = "shot.png")
    {
        byte[] bytes = FileSignature.HeaderFor("image/png");
        return new FileUpload(name, "image/png", bytes.Length, new MemoryStream(bytes));
    }

    [Fact]
    public async Task SaveAsync_PutsTheBytesOnDisk()
    {
        using var fixture = new SqlServiceFixture();

        Result<string> saved = await fixture.Files.SaveAsync(Png());

        saved.IsSuccess.Should().BeTrue();

        string absolute = Path.Combine(fixture.StorageRoot, saved.Value!.Replace('/', Path.DirectorySeparatorChar));

        File.Exists(absolute).Should().BeTrue();
        (await File.ReadAllBytesAsync(absolute)).Should().Equal(FileSignature.HeaderFor("image/png"));
    }

    [Fact]
    public async Task SaveAsync_WritesInsideTheConfiguredRootOnly()
    {
        using var fixture = new SqlServiceFixture();

        // The caller's name is hostile. It must not reach the filesystem at all.
        Result<string> saved = await fixture.Files.SaveAsync(Png("../../../../web.config"));

        saved.IsSuccess.Should().BeTrue();

        string absolute = Path.GetFullPath(
            Path.Combine(fixture.StorageRoot, saved.Value!));

        absolute.Should().StartWith(Path.GetFullPath(fixture.StorageRoot));
        File.Exists(Path.Combine(fixture.StorageRoot, "web.config")).Should().BeFalse();
    }

    [Fact]
    public async Task OpenRead_ForAPathOutsideTheRoot_ReturnsNull()
    {
        // The endpoint passes whatever is in the URL straight through, so this is
        // the check that stops /media/../../appsettings.json working.
        using var fixture = new SqlServiceFixture();

        IFileReader reader = fixture.Get<IFileReader>();

        reader.OpenRead("../../../appsettings.json").Should().BeNull();
        reader.OpenRead("../secrets.txt").Should().BeNull();
    }

    [Fact]
    public void OpenRead_ForABlankPath_ReturnsNull()
    {
        using var fixture = new SqlServiceFixture();

        IFileReader reader = fixture.Get<IFileReader>();

        reader.OpenRead("").Should().BeNull();
        reader.OpenRead("   ").Should().BeNull();
    }

    [Fact]
    public async Task OpenRead_ForASavedFile_ReturnsItsBytes()
    {
        using var fixture = new SqlServiceFixture();

        Result<string> saved = await fixture.Files.SaveAsync(Png());

        using Stream? content = fixture.Get<IFileReader>().OpenRead(saved.Value!);

        content.Should().NotBeNull();

        using var buffer = new MemoryStream();
        await content!.CopyToAsync(buffer);

        buffer.ToArray().Should().Equal(FileSignature.HeaderFor("image/png"));
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheFileFromDisk()
    {
        // An orphaned file is a client screenshot nobody can see and nobody
        // remembers to remove.
        using var fixture = new SqlServiceFixture();

        Result<string> saved = await fixture.Files.SaveAsync(Png());
        string absolute = Path.Combine(
            fixture.StorageRoot, saved.Value!.Replace('/', Path.DirectorySeparatorChar));

        File.Exists(absolute).Should().BeTrue();

        Result deleted = await fixture.Files.DeleteAsync(saved.Value!);

        deleted.IsSuccess.Should().BeTrue();
        File.Exists(absolute).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ForAFileThatIsAlreadyGone_StillSucceeds()
    {
        // The caller wanted it gone. It is gone.
        using var fixture = new SqlServiceFixture();

        Result result = await fixture.Files.DeleteAsync("steps/never-existed.png");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ForAPathOutsideTheRoot_TouchesNothing()
    {
        using var fixture = new SqlServiceFixture();

        // A real file outside the store, standing in for anything on the disk.
        string outside = Path.Combine(Path.GetTempPath(), $"teamportal-bystander-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(outside, "not yours");

        try
        {
            string relative = Path.GetRelativePath(fixture.StorageRoot, outside);

            await fixture.Files.DeleteAsync(relative);

            File.Exists(outside).Should().BeTrue("a stored path must never reach outside the root");
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public async Task GetUrl_PointsAtTheAuthorisedEndpoint()
    {
        // Not wwwroot. /media is behind RequireAuthorization - see MediaEndpoints.
        using var fixture = new SqlServiceFixture();

        Result<string> saved = await fixture.Files.SaveAsync(Png());

        fixture.Files.GetUrl(saved.Value!).Should().StartWith("/media/");
    }
}
