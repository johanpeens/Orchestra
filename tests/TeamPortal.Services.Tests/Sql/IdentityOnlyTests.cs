using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;
using TeamPortal.Domain.Identity;
using TeamPortal.Infrastructure.Data;
using TeamPortal.Services.Tests.Common;

namespace TeamPortal.Services.Tests.Sql;

public class IdentityOnlyTests
{
    private const string Admin = "admin@company.com";

    /// <summary>Meets the password rules in AddPortalIdentityCore.</summary>
    private const string NewPassword = "Renewed!2026";

    [Fact]
    public async Task SignInAsync_OnSuccess_StampsLastLoginAt()
    {
        using var fixture = new SqlServiceFixture();

        await fixture.Auth.SignInAsync(new SignInRequest(Admin, fixture.DemoPassword));

        AppUser user = await FindAdminAsync(fixture);

        user.LastLoginAt.Should().NotBeNull();
        user.LastLoginAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task SignInAsync_BeforeAnySignIn_LeavesLastLoginAtNull()
    {
        using var fixture = new SqlServiceFixture();

        AppUser user = await FindAdminAsync(fixture);

        user.LastLoginAt.Should().BeNull("a seeded account has never signed in");
    }

    [Fact]
    public async Task Password_IsStoredAsAHashNotAsText()
    {
        // The mock kept passwords in a string field, which was fine for a screen
        // that never persisted. This is the test that says it is no longer fine.
        using var fixture = new SqlServiceFixture();

        AppUser user = await FindAdminAsync(fixture);

        user.PasswordHash.Should().NotBeNullOrWhiteSpace();
        user.PasswordHash.Should().NotContain(fixture.DemoPassword);
    }

    [Fact]
    public async Task ResetToken_UsedTwice_IsRejectedTheSecondTime()
    {
        // A reset link that keeps working is a permanent back door into the
        // account for anyone who ever saw the email.
        using var fixture = new SqlServiceFixture();

        UserManager<AppUser> users = fixture.Get<UserManager<AppUser>>();
        AppUser user = (await users.FindByEmailAsync(Admin))!;
        string token = await users.GeneratePasswordResetTokenAsync(user);

        Result first = await fixture.Auth.ResetPasswordAsync(
            new ResetPasswordRequest(Admin, token, NewPassword, NewPassword));

        first.IsSuccess.Should().BeTrue();

        Result second = await fixture.Auth.ResetPasswordAsync(
            new ResetPasswordRequest(Admin, token, "Different!2026", "Different!2026"));

        second.IsFailure.Should().BeTrue("a reset token is single use");
    }

    [Fact]
    public async Task ResetPasswordAsync_WithAGarbageToken_IsRejected()
    {
        using var fixture = new SqlServiceFixture();

        Result result = await fixture.Auth.ResetPasswordAsync(
            new ResetPasswordRequest(Admin, "not-a-token", "Renewed!2026", "Renewed!2026"));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPasswordAsync_ForAnUnknownEmail_GivesTheSameAnswerAsABadToken()
    {
        // Otherwise the reset form becomes the user-enumeration tool that the
        // forgot-password form deliberately is not.
        using var fixture = new SqlServiceFixture();

        Result unknown = await fixture.Auth.ResetPasswordAsync(
            new ResetPasswordRequest("nobody@company.com", "x", "Renewed!2026", "Renewed!2026"));

        Result badToken = await fixture.Auth.ResetPasswordAsync(
            new ResetPasswordRequest(Admin, "x", "Renewed!2026", "Renewed!2026"));

        unknown.ErrorSummary.Should().Be(badToken.ErrorSummary);
    }

    [Fact]
    public async Task ResetPasswordAsync_OnALockedAccount_LetsThePersonBackIn()
    {
        // A reset is the way out of a lockout. Without clearing the counter the
        // person is still locked out with a password that now works.
        using var fixture = new SqlServiceFixture();

        for (int i = 0; i < 5; i++)
        {
            await fixture.Auth.SignInAsync(new SignInRequest(Admin, "wrong"));
        }

        UserManager<AppUser> users = fixture.Get<UserManager<AppUser>>();
        AppUser user = (await users.FindByEmailAsync(Admin))!;
        string token = await users.GeneratePasswordResetTokenAsync(user);

        await fixture.Auth.ResetPasswordAsync(
            new ResetPasswordRequest(Admin, token, NewPassword, NewPassword));

        SignInResultDto signIn = await fixture.Auth.SignInAsync(
            new SignInRequest(Admin, NewPassword));

        signIn.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task SignInAsync_AfterAReset_RejectsTheOldPassword()
    {
        using var fixture = new SqlServiceFixture();

        UserManager<AppUser> users = fixture.Get<UserManager<AppUser>>();
        AppUser user = (await users.FindByEmailAsync(Admin))!;
        string token = await users.GeneratePasswordResetTokenAsync(user);

        string oldPassword = fixture.DemoPassword;

        await fixture.Auth.ResetPasswordAsync(
            new ResetPasswordRequest(Admin, token, NewPassword, NewPassword));

        SignInResultDto result = await fixture.Auth.SignInAsync(
            new SignInRequest(Admin, oldPassword));

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public void PasswordHash_IsNeverReturnedByAnyDto()
    {
        // A reflection sweep rather than a list, so a DTO added next year is
        // covered by a test written today.
        Assembly application = typeof(CurrentUserDto).Assembly;

        string[] forbidden =
        [
            "PasswordHash", "Password", "SecurityStamp", "ConcurrencyStamp",
            "Token", "AccessFailedCount"
        ];

        IReadOnlyList<string> leaks =
        [
            .. application.GetTypes()
                .Where(t => t.Name.EndsWith("Dto", StringComparison.Ordinal))
                .SelectMany(t => t.GetProperties())
                .Where(p => forbidden.Contains(p.Name, StringComparer.Ordinal))
                .Select(p => $"{p.DeclaringType!.Name}.{p.Name}")
        ];

        leaks.Should().BeEmpty("nothing the UI can bind to may carry a credential");
    }

    [Fact]
    public async Task Seeder_CreatesTheDemoAccountsAndRoles()
    {
        using var fixture = new SqlServiceFixture();

        IDbContextFactory<PortalDbContext> factory = fixture.Get<IDbContextFactory<PortalDbContext>>();
        await using PortalDbContext db = await factory.CreateDbContextAsync();

        (await db.Users.CountAsync()).Should().Be(4);
        (await db.Roles.CountAsync()).Should().Be(4);
        (await db.UserRoles.CountAsync()).Should().Be(4, "every seeded account has a role");
    }

    [Fact]
    public async Task Seeder_LinksTheDemoAccountsToTheirEmployeeRecords()
    {
        // AppUser.EmployeeId is what puts a face on the app bar. A seeded account
        // with no employee behind it shows an initial and nothing else.
        using var fixture = new SqlServiceFixture();

        IDbContextFactory<PortalDbContext> factory = fixture.Get<IDbContextFactory<PortalDbContext>>();
        await using PortalDbContext db = await factory.CreateDbContextAsync();

        AppUser sarah = await db.Users.SingleAsync(u => u.Email == "sarah@company.com");

        sarah.EmployeeId.Should().NotBeNull();
    }

    private static async Task<AppUser> FindAdminAsync(SqlServiceFixture fixture)
    {
        IDbContextFactory<PortalDbContext> factory = fixture.Get<IDbContextFactory<PortalDbContext>>();
        await using PortalDbContext db = await factory.CreateDbContextAsync();

        return await db.Users.AsNoTracking().SingleAsync(u => u.Email == Admin);
    }
}
