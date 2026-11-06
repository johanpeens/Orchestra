using TeamPortal.Application.Dtos;
using TeamPortal.Application.Requests;
using TeamPortal.Infrastructure.Identity;
using TeamPortal.Services.Tests.Common;

namespace TeamPortal.Services.Tests.Sql;

public class SignInHandoffTests
{
    private const string Admin = "admin@company.com";

    [Fact]
    public async Task ASuccessfulSignIn_ReturnsATicket()
    {
        using var fixture = new SqlServiceFixture();

        SignInResultDto result = await fixture.Auth.SignInAsync(
            new SignInRequest(Admin, fixture.DemoPassword));

        result.Succeeded.Should().BeTrue();
        result.Ticket.Should().NotBeNullOrWhiteSpace(
            "the cookie cannot be written here, so something has to carry the sign-in " +
            "to a request that can write it");
    }

    [Fact]
    public async Task AFailedSignIn_ReturnsNoTicket()
    {
        // A ticket is a signed-in session in waiting. One handed out on a failed
        // attempt would be a way in without a password.
        using var fixture = new SqlServiceFixture();

        SignInResultDto result = await fixture.Auth.SignInAsync(
            new SignInRequest(Admin, "wrong"));

        result.Succeeded.Should().BeFalse();
        result.Ticket.Should().BeNull();
    }

    [Fact]
    public async Task SigningIn_DoesNotTryToWriteACookie()
    {
        // The fixture's authentication service throws the way a live circuit
        // does. If this ever calls SignInManager.SignInAsync again, this test is
        // where it surfaces - rather than in the browser, which is where it
        // surfaced last time.
        using var fixture = new SqlServiceFixture();

        Func<Task> act = async () => await fixture.Auth.SignInAsync(
            new SignInRequest(Admin, fixture.DemoPassword));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SigningOut_DoesNotTryToDeleteACookieEither()
    {
        using var fixture = new SqlServiceFixture();

        await fixture.Auth.SignInAsync(new SignInRequest(Admin, fixture.DemoPassword));

        Func<Task> act = async () => await fixture.Auth.SignOutAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task TwoSignIns_GetDifferentTickets()
    {
        using var fixture = new SqlServiceFixture();

        SignInResultDto first = await fixture.Auth.SignInAsync(
            new SignInRequest(Admin, fixture.DemoPassword));

        SignInResultDto second = await fixture.Auth.SignInAsync(
            new SignInRequest(Admin, fixture.DemoPassword));

        second.Ticket.Should().NotBe(first.Ticket);
    }
}

public class SignInTicketStoreTests
{
    [Fact]
    public void ATicket_CanBeRedeemedOnce()
    {
        var store = new InMemorySignInTicketStore();

        string ticket = store.Issue(userId: 7, isPersistent: true);

        SignInTicket? first = store.Redeem(ticket);

        first.Should().NotBeNull();
        first!.UserId.Should().Be(7);
        first.IsPersistent.Should().BeTrue();
    }

    [Fact]
    public void ATicket_CannotBeRedeemedTwice()
    {
        // Replay. A ticket left in a browser history or a proxy log must be
        // worthless the moment it has been used.
        var store = new InMemorySignInTicketStore();

        string ticket = store.Issue(userId: 7, isPersistent: false);

        store.Redeem(ticket).Should().NotBeNull();
        store.Redeem(ticket).Should().BeNull();
    }

    [Fact]
    public void AnUnknownTicket_IsRefused()
    {
        var store = new InMemorySignInTicketStore();

        store.Redeem("not-a-ticket").Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankTicket_IsRefused(string ticket)
    {
        new InMemorySignInTicketStore().Redeem(ticket).Should().BeNull();
    }

    [Fact]
    public void TicketsAreLongAndUnguessable()
    {
        // 256 bits, base64url. A guessable ticket would be a way to sign in as
        // somebody else without their password.
        var store = new InMemorySignInTicketStore();

        string ticket = store.Issue(userId: 1, isPersistent: false);

        ticket.Length.Should().BeGreaterThanOrEqualTo(40);
        ticket.Should().MatchRegex("^[A-Za-z0-9_-]+$", "it travels in a query string");
    }

    [Fact]
    public void TicketsAreDistinct()
    {
        var store = new InMemorySignInTicketStore();

        IReadOnlyList<string> issued =
        [
            .. Enumerable.Range(0, 200).Select(_ => store.Issue(userId: 1, isPersistent: false))
        ];

        issued.Distinct(StringComparer.Ordinal).Should().HaveCount(issued.Count);
    }

    [Fact]
    public void RedeemingOneTicket_DoesNotDisturbAnother()
    {
        // Two people signing in at the same moment is the ordinary case, not the
        // edge case.
        var store = new InMemorySignInTicketStore();

        string first = store.Issue(userId: 1, isPersistent: false);
        string second = store.Issue(userId: 2, isPersistent: true);

        store.Redeem(first)!.UserId.Should().Be(1);
        store.Redeem(second)!.UserId.Should().Be(2);
    }
}
