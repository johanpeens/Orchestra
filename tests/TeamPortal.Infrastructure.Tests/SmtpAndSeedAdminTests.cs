using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using TeamPortal.Application.Common;
using TeamPortal.Domain.Enums;
using TeamPortal.Domain.Identity;
using TeamPortal.Infrastructure.Data;
using TeamPortal.Infrastructure.Email;
using TeamPortal.Infrastructure.Tests.Common;

namespace TeamPortal.Infrastructure.Tests;

public class SmtpOptionsTests
{
    [Fact]
    public void Port465_ResolvesToImplicitTls()
    {
        // The one that matters. 465 servers start TLS immediately and never send
        // a plaintext greeting, so a client waiting to negotiate STARTTLS hangs
        // until it times out - and the user is told the email could not be sent
        // with no hint as to why.
        var options = new SmtpOptions { Port = 465 };

        options.ResolveSecurity().Should().Be(SecureSocketOptions.SslOnConnect);
    }

    [Fact]
    public void Port587_ResolvesToStartTls()
    {
        new SmtpOptions { Port = 587 }.ResolveSecurity()
            .Should().Be(SecureSocketOptions.StartTls);
    }

    [Fact]
    public void AnUnusualPort_IsLeftForMailKitToNegotiate()
    {
        new SmtpOptions { Port = 2525 }.ResolveSecurity()
            .Should().Be(SecureSocketOptions.Auto);
    }

    [Fact]
    public void AnExplicitSecurityMode_BeatsThePortGuess()
    {
        var options = new SmtpOptions { Port = 465, Security = SecureSocketOptions.StartTls };

        options.ResolveSecurity().Should().Be(SecureSocketOptions.StartTls);
    }

    [Theory]
    [InlineData("", "from@company.com")]
    [InlineData("mail.company.com", "")]
    [InlineData("", "")]
    public void WithoutAHostOrAFromAddress_ItIsNotConfigured(string host, string from)
    {
        new SmtpOptions { Host = host, From = from }.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void WithAHostAndAFromAddress_ItIsConfigured()
    {
        // No password required: a relay that authenticates by IP is normal.
        new SmtpOptions { Host = "mail.company.com", From = "from@company.com" }
            .IsConfigured.Should().BeTrue();
    }
}

public class SeedAdminOptionsTests
{
    [Fact]
    public void WithNoPassword_ItIsNotConfigured()
    {
        // Creating an administrator with a blank password because somebody forgot
        // to set a secret is the worst outcome available here, so a half-filled
        // section is skipped rather than guessed at.
        new SeedAdminOptions { Email = "admin@company.com" }.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void WithNoEmail_ItIsNotConfigured()
    {
        new SeedAdminOptions { Password = "something" }.IsConfigured.Should().BeFalse();
    }

    [Fact]
    public void TheRoleDefaultsToAdmin()
    {
        // The only reason this section exists.
        new SeedAdminOptions().Role.Should().Be(AppRoles.Admin);
    }
}

public class SeedAdminTests
{
    private static SeedAdminOptions Options => new()
    {
        Email = "owner@company.com",
        Password = "Owner!2026x",
        Role = AppRoles.Admin,
        FullName = "The Owner"
    };

    [Fact]
    public async Task Seeder_WithNoAdminConfigured_CreatesOnlyTheDemoAccounts()
    {
        await using var fixture = new IdentityDatabaseFixture();

        await fixture.SeedAsync(admin: null);

        await using PortalDbContext db = fixture.NewContext();

        (await db.Users.CountAsync()).Should().Be(4, "just the four demo accounts");
    }

    [Fact]
    public async Task Seeder_WithAnAdminConfigured_CreatesIt()
    {
        await using var fixture = new IdentityDatabaseFixture();

        await fixture.SeedAsync(Options);

        await using PortalDbContext db = fixture.NewContext();
        AppUser admin = await db.Users.SingleAsync(u => u.Email == "owner@company.com");

        admin.FullName.Should().Be("The Owner");
        admin.PasswordHash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TheSeededAdmin_IsThroughBothGates()
    {
        // It is not a self-registration. Whoever put the password in the secret
        // store is the approval, so making them click a link and then wait for
        // an administrator - when they ARE the administrator - is a deadlock.
        await using var fixture = new IdentityDatabaseFixture();

        await fixture.SeedAsync(Options);

        await using PortalDbContext db = fixture.NewContext();
        AppUser admin = await db.Users.SingleAsync(u => u.Email == "owner@company.com");

        admin.EmailConfirmed.Should().BeTrue();
        admin.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        admin.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task TheSeededAdmin_GetsTheConfiguredRole()
    {
        await using var fixture = new IdentityDatabaseFixture();

        await fixture.SeedAsync(Options);

        IList<string> roles = await fixture.RolesForAsync("owner@company.com");

        roles.Should().ContainSingle().Which.Should().Be(AppRoles.Admin);
    }

    [Fact]
    public async Task Seeder_RunTwice_DoesNotDuplicateTheAdmin()
    {
        await using var fixture = new IdentityDatabaseFixture();

        await fixture.SeedAsync(Options);
        await fixture.SeedAsync(Options);

        await using PortalDbContext db = fixture.NewContext();

        (await db.Users.CountAsync(u => u.Email == "owner@company.com")).Should().Be(1);
    }

    [Fact]
    public async Task Seeder_RunAgainWithADifferentPassword_LeavesTheExistingOneAlone()
    {
        // Otherwise every restart silently resets a password the owner may have
        // changed, and the value in the secret store quietly becomes the truth
        // again without anyone deciding that.
        await using var fixture = new IdentityDatabaseFixture();

        await fixture.SeedAsync(Options);

        string firstHash = await fixture.PasswordHashForAsync("owner@company.com");

        SeedAdminOptions changed = Options;
        changed.Password = "Different!2026x";
        await fixture.SeedAsync(changed);

        string secondHash = await fixture.PasswordHashForAsync("owner@company.com");

        secondHash.Should().Be(firstHash);
    }

    [Fact]
    public async Task Seeder_WithAnAdminEmailThatIsAlreadyADemoAccount_ChangesNothing()
    {
        await using var fixture = new IdentityDatabaseFixture();

        SeedAdminOptions clash = Options;
        clash.Email = "admin@company.com";

        await fixture.SeedAsync(clash);

        await using PortalDbContext db = fixture.NewContext();

        (await db.Users.CountAsync(u => u.Email == "admin@company.com")).Should().Be(1);
    }
}
