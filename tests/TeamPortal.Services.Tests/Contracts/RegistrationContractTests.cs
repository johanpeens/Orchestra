using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Enums;
using TeamPortal.Application.Requests;
using TeamPortal.Mock.Services;
using TeamPortal.Services.Tests.Common;

namespace TeamPortal.Services.Tests.Contracts;

public abstract class RegistrationContractTests : IDisposable
{
    protected const string NewEmail = "nomsa@company.com";
    protected const string GoodPassword = "Portal!2026x";

    protected RegistrationContractTests() => Fixture = CreateFixture();

    protected ServiceFixture Fixture { get; }

    protected abstract ServiceFixture CreateFixture();

    /// <summary>The messages this implementation sent. Both sides keep them.</summary>
    protected abstract IReadOnlyList<EmailMessage> Sent { get; }

    /// <summary>Signs in as the seeded administrator, for the approval half.</summary>
    protected abstract Task BecomeAdminAsync();

    private static RegisterRequest Valid(string email = NewEmail) =>
        new("Nomsa Dlamini", email, GoodPassword, GoodPassword);

    // --- Registering ---------------------------------------------------------

    [Fact]
    public async Task RegisterAsync_WithAValidRequest_Succeeds()
    {
        Result result = await Fixture.Auth.RegisterAsync(Valid());

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_SendsAConfirmationEmailToTheAddressGiven()
    {
        await Fixture.Auth.RegisterAsync(Valid());

        Sent.Should().ContainSingle()
            .Which.To.Should().Be(NewEmail);
    }

    [Fact]
    public async Task RegisterAsync_TheEmailCarriesALinkWithAToken()
    {
        await Fixture.Auth.RegisterAsync(Valid());

        EmailMessage message = Sent.Single();

        message.TextBody.Should().Contain("confirm-email");
        MockEmailSender.TokenFrom(message).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RegisterAsync_TheEmailHasBothAnHtmlAndATextBody()
    {
        // A message with no text part is scored as spam by most filters, and some
        // corporate clients strip HTML outright.
        await Fixture.Auth.RegisterAsync(Valid());

        EmailMessage message = Sent.Single();

        message.HtmlBody.Should().NotBeNullOrWhiteSpace();
        message.TextBody.Should().NotBeNullOrWhiteSpace();
        message.Subject.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RegisterAsync_WithAWeakPassword_IsRefused()
    {
        Result result = await Fixture.Auth.RegisterAsync(
            new RegisterRequest("Nomsa", NewEmail, "short", "short"));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Field == nameof(RegisterRequest.Password));
    }

    [Fact]
    public async Task RegisterAsync_WhenThePasswordsDoNotMatch_IsRefused()
    {
        Result result = await Fixture.Auth.RegisterAsync(
            new RegisterRequest("Nomsa", NewEmail, GoodPassword, "Something!2026x"));

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Field == nameof(RegisterRequest.ConfirmPassword));
    }

    [Fact]
    public async Task RegisterAsync_WhenValidationFails_SendsNothing()
    {
        await Fixture.Auth.RegisterAsync(new RegisterRequest("", "not-an-email", "x", "y"));

        Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterAsync_ForAnAddressThatAlreadyHasAnAccount_StillReportsSuccess()
    {
        // Otherwise the form is a way to test which addresses are registered.
        Result result = await Fixture.Auth.RegisterAsync(Valid("admin@company.com"));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_ForAnExistingAccount_SendsAResetRatherThanAConfirmation()
    {
        // The difference goes in the email, where only the owner of the mailbox
        // sees it - not in the response, where anybody can.
        await Fixture.Auth.RegisterAsync(Valid("admin@company.com"));

        EmailMessage message = Sent.Single();

        message.TextBody.Should().Contain("reset-password");
        message.TextBody.Should().NotContain("confirm-email");
    }

    // --- The first gate ------------------------------------------------------

    [Fact]
    public async Task SignInAsync_BeforeConfirming_IsRefused()
    {
        await Fixture.Auth.RegisterAsync(Valid());

        SignInResultDto result = await Fixture.Auth.SignInAsync(
            new SignInRequest(NewEmail, GoodPassword));

        result.Succeeded.Should().BeFalse();
        result.Reason.Should().Be(SignInFailureReason.EmailNotConfirmed);
    }

    [Fact]
    public async Task ConfirmEmailAsync_WithTheTokenFromTheEmail_Succeeds()
    {
        await Fixture.Auth.RegisterAsync(Valid());
        string token = MockEmailSender.TokenFrom(Sent.Single())!;

        Result result = await Fixture.Auth.ConfirmEmailAsync(
            new ConfirmEmailRequest(NewEmail, token));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmEmailAsync_Twice_SucceedsBothTimes()
    {
        // People click links twice, and a forwarded email gets opened by the
        // recipient after the sender has already used it.
        await Fixture.Auth.RegisterAsync(Valid());
        string token = MockEmailSender.TokenFrom(Sent.Single())!;

        await Fixture.Auth.ConfirmEmailAsync(new ConfirmEmailRequest(NewEmail, token));

        Result second = await Fixture.Auth.ConfirmEmailAsync(
            new ConfirmEmailRequest(NewEmail, token));

        second.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmEmailAsync_WithAGarbageToken_IsRefused()
    {
        await Fixture.Auth.RegisterAsync(Valid());

        Result result = await Fixture.Auth.ConfirmEmailAsync(
            new ConfirmEmailRequest(NewEmail, "not-a-token"));

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmEmailAsync_ForAnUnknownAddress_IsRefused()
    {
        Result result = await Fixture.Auth.ConfirmEmailAsync(
            new ConfirmEmailRequest("nobody@company.com", "whatever"));

        result.IsFailure.Should().BeTrue();
    }

    // --- The second gate -----------------------------------------------------

    [Fact]
    public async Task SignInAsync_AfterConfirmingButBeforeApproval_IsRefused()
    {
        // The whole point of the approval queue. Confirming an address only
        // proves somebody can read that mailbox.
        await RegisterAndConfirmAsync();

        SignInResultDto result = await Fixture.Auth.SignInAsync(
            new SignInRequest(NewEmail, GoodPassword));

        result.Succeeded.Should().BeFalse();
        result.Reason.Should().Be(SignInFailureReason.AwaitingApproval);
    }

    [Fact]
    public async Task ANewRegistration_ShowsUpInTheApprovalQueueOnceConfirmed()
    {
        await RegisterAndConfirmAsync();
        await BecomeAdminAsync();

        IReadOnlyList<UserAccountDto> waiting =
            await Fixture.Approvals.GetAllAsync(ApprovalFilter.AwaitingDecision);

        waiting.Should().ContainSingle().Which.Email.Should().Be(NewEmail);
    }

    [Fact]
    public async Task ANewRegistration_IsNotInTheQueueUntilItIsConfirmed()
    {
        // Waiting on them, not on the administrator. Mixing the two makes the
        // queue impossible to clear.
        await Fixture.Auth.RegisterAsync(Valid());
        await BecomeAdminAsync();

        IReadOnlyList<UserAccountDto> waiting =
            await Fixture.Approvals.GetAllAsync(ApprovalFilter.AwaitingDecision);

        waiting.Should().BeEmpty();
    }

    [Fact]
    public async Task ApproveAsync_LetsThemSignIn()
    {
        int userId = await RegisterConfirmAndFindAsync();

        Result approved = await Fixture.Approvals.ApproveAsync(userId, AppRoles.Viewer);
        approved.IsSuccess.Should().BeTrue();

        SignInResultDto result = await Fixture.Auth.SignInAsync(
            new SignInRequest(NewEmail, GoodPassword));

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task ApproveAsync_EmailsThemToSaySo()
    {
        int userId = await RegisterConfirmAndFindAsync();

        await Fixture.Approvals.ApproveAsync(userId, AppRoles.Viewer);

        EmailMessage approval = Sent[^1];

        approval.To.Should().Be(NewEmail);
        approval.Subject.Should().Contain("ready");
    }

    [Fact]
    public async Task ApproveAsync_GivesThemTheRoleTheAdminPicked()
    {
        int userId = await RegisterConfirmAndFindAsync();

        await Fixture.Approvals.ApproveAsync(userId, AppRoles.Manager);

        SignInResultDto result = await Fixture.Auth.SignInAsync(
            new SignInRequest(NewEmail, GoodPassword));

        result.User!.Roles.Should().Contain(AppRoles.Manager);
    }

    [Fact]
    public async Task ApproveAsync_BeforeTheEmailIsConfirmed_IsRefused()
    {
        // Approving an address nobody has proved they can read would skip the
        // first gate entirely.
        await Fixture.Auth.RegisterAsync(Valid());
        await BecomeAdminAsync();

        UserAccountDto account = (await Fixture.Approvals.GetAllAsync())
            .Single(a => a.Email == NewEmail);

        Result result = await Fixture.Approvals.ApproveAsync(account.UserId, AppRoles.Viewer);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ApproveAsync_WithARoleThatDoesNotExist_IsRefused()
    {
        int userId = await RegisterConfirmAndFindAsync();

        Result result = await Fixture.Approvals.ApproveAsync(userId, "Superuser");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task RejectAsync_KeepsThemOut()
    {
        int userId = await RegisterConfirmAndFindAsync();

        await Fixture.Approvals.RejectAsync(userId);

        SignInResultDto result = await Fixture.Auth.SignInAsync(
            new SignInRequest(NewEmail, GoodPassword));

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task RejectAsync_SendsNoEmail()
    {
        // Telling a stranger their request was seen and refused is more
        // information than they are owed.
        int userId = await RegisterConfirmAndFindAsync();
        int before = Sent.Count;

        await Fixture.Approvals.RejectAsync(userId);

        Sent.Should().HaveCount(before);
    }

    [Fact]
    public async Task RejectAsync_TakesEveryRoleAway()
    {
        // A rejected account that kept a role comes back to life the moment
        // somebody flips its status by hand.
        int userId = await RegisterConfirmAndFindAsync();
        await Fixture.Approvals.ApproveAsync(userId, AppRoles.Manager);

        await Fixture.Approvals.RejectAsync(userId);

        UserAccountDto account = (await Fixture.Approvals.GetAllAsync())
            .Single(a => a.Email == NewEmail);

        account.Roles.Should().BeEmpty();
    }

    // --- Who may see the queue -----------------------------------------------

    [Theory]
    [InlineData(AppRoles.Manager)]
    [InlineData(AppRoles.Member)]
    [InlineData(AppRoles.Viewer)]
    public async Task TheQueue_IsEmptyForAnyoneWhoIsNotAnAdmin(string role)
    {
        // A list of who has an account here is worth something on its own, so the
        // service refuses rather than relying on the page being unreachable.
        await RegisterAndConfirmAsync();
        SignInAs(role);

        (await Fixture.Approvals.GetAllAsync()).Should().BeEmpty();
        (await Fixture.Approvals.GetPendingCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ApprovingAsSomeoneWhoIsNotAnAdmin_IsRefused()
    {
        int userId = await RegisterConfirmAndFindAsync();
        SignInAs(AppRoles.Manager);

        Result result = await Fixture.Approvals.ApproveAsync(userId, AppRoles.Admin);

        result.IsFailure.Should().BeTrue();
    }

    // --- Helpers -------------------------------------------------------------

    /// <summary>Signs in as a given role without going through a password.</summary>
    protected abstract void SignInAs(string role);

    private async Task RegisterAndConfirmAsync()
    {
        await Fixture.Auth.RegisterAsync(Valid());

        string token = MockEmailSender.TokenFrom(Sent.Single())!;
        await Fixture.Auth.ConfirmEmailAsync(new ConfirmEmailRequest(NewEmail, token));
    }

    private async Task<int> RegisterConfirmAndFindAsync()
    {
        await RegisterAndConfirmAsync();
        await BecomeAdminAsync();

        return (await Fixture.Approvals.GetAllAsync())
            .Single(a => a.Email == NewEmail)
            .UserId;
    }

    public void Dispose()
    {
        Fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>Part A: the registration flow against the in-memory implementation.</summary>
public sealed class MockRegistrationTests : RegistrationContractTests
{
    private MockServiceFixture Mock => (MockServiceFixture)Fixture;

    protected override ServiceFixture CreateFixture() => new MockServiceFixture();

    protected override IReadOnlyList<EmailMessage> Sent => Mock.Email.Sent;

    protected override Task BecomeAdminAsync()
    {
        Fixture.SignInAs(1, AppRoles.Admin);
        return Task.CompletedTask;
    }

    protected override void SignInAs(string role) => Fixture.SignInAs(1, role);
}

/// <summary>Part B: the same flow against ASP.NET Core Identity.</summary>
public sealed class IdentityRegistrationTests : RegistrationContractTests
{
    private SqlServiceFixture Sql => (SqlServiceFixture)Fixture;

    protected override ServiceFixture CreateFixture() => new SqlServiceFixture();

    protected override IReadOnlyList<EmailMessage> Sent => Sql.Email.Sent;

    protected override Task BecomeAdminAsync()
    {
        Fixture.SignInAs(1, AppRoles.Admin);
        return Task.CompletedTask;
    }

    protected override void SignInAs(string role) => Fixture.SignInAs(1, role);
}
