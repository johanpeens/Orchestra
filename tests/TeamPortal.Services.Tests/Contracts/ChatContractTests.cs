using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Dtos;
using TeamPortal.Services.Tests.Common;

namespace TeamPortal.Services.Tests.Contracts;

public abstract class ChatContractTests : IDisposable
{
    protected ChatContractTests() => Fixture = CreateFixture();

    protected ServiceFixture Fixture { get; }

    protected abstract ServiceFixture CreateFixture();

    private IChatService Sut => Fixture.Get<IChatService>();

    // --- It answers from the data --------------------------------------------

    [Fact]
    public async Task AskAsync_AboutAProcessByName_DescribesThatProcess()
    {
        ChatReplyDto reply = await Sut.AskAsync("What is initial contact?");

        reply.Text.Should().Contain("Initial Contact");
        reply.Text.Should().Contain("Client Onboarding");
    }

    [Fact]
    public async Task AskAsync_AboutAProcess_ListsItsStepsInOrder()
    {
        ChatReplyDto reply = await Sut.AskAsync("What are the steps in initial contact?");

        int receive = reply.Text.IndexOf("Receive the inquiry", StringComparison.Ordinal);
        int acknowledge = reply.Text.IndexOf("Acknowledge the client", StringComparison.Ordinal);

        receive.Should().BeGreaterThan(-1);
        acknowledge.Should().BeGreaterThan(receive);
    }

    [Fact]
    public async Task AskAsync_WhoIsResponsible_NamesThePeopleAndTheirSteps()
    {
        ChatReplyDto reply = await Sut.AskAsync("Who is responsible for initial contact?");

        reply.Text.Should().Contain("Sarah");
        reply.Text.Should().Contain("step");
    }

    [Fact]
    public async Task AskAsync_WhoIsResponsible_ListsEachPersonOnce()
    {
        // Sarah owns more than one step. "Who is involved" is a list of people,
        // not a tally of assignments.
        ChatReplyDto reply = await Sut.AskAsync("Who works on initial contact?");

        int occurrences = reply.Text.Split("Sarah").Length - 1;

        occurrences.Should().Be(1);
    }

    // --- It refuses rather than guesses ---------------------------------------

    [Fact]
    public async Task AskAsync_AboutSomethingNotInThePortal_SaysSo()
    {
        ChatReplyDto reply = await Sut.AskAsync("What is our refund policy for hovercraft?");

        reply.Text.Should().Contain("could not find");
        reply.Sources.Should().BeEmpty();
    }

    [Fact]
    public async Task AskAsync_WhenItCannotFindAnything_NamesWhatItDoesHold()
    {
        // A bare "no" leaves somebody who missed by one word with nowhere to go.
        ChatReplyDto reply = await Sut.AskAsync("Tell me about hovercraft licensing");

        reply.Text.Should().Contain("Client Onboarding");
    }

    [Fact]
    public async Task AskAsync_WithNothingTypedAtAll_ExplainsWhatItCanDo()
    {
        ChatReplyDto reply = await Sut.AskAsync("   ");

        reply.Text.Should().Contain("Ask me about");
        reply.Sources.Should().BeEmpty();
    }

    // --- Every answer can be checked ------------------------------------------

    [Fact]
    public async Task AskAsync_CitesTheProcessItAnsweredFrom()
    {
        ChatReplyDto reply = await Sut.AskAsync("What is initial contact?");

        reply.Sources.Should().NotBeEmpty();
        reply.Sources[0].ProcessName.Should().Be("Initial Contact");
        reply.Sources[0].FolderName.Should().Be("Client Onboarding");
    }

    [Fact]
    public async Task AskAsync_ItsSources_CarryTheIdsNeededToLinkBack()
    {
        // The page builds /processes/{folder}/{process} out of these. Zero would
        // route to nothing.
        ChatReplyDto reply = await Sut.AskAsync("What is initial contact?");

        reply.Sources[0].FolderId.Should().BeGreaterThan(0);
        reply.Sources[0].ProcessId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AskAsync_NeverCitesMoreThanAHandful()
    {
        // A broad question matches nearly everything. Citing all of it is the
        // same as citing none of it.
        ChatReplyDto reply = await Sut.AskAsync("client");

        reply.Sources.Should().HaveCountLessThanOrEqualTo(5);
    }

    // --- Ranking ---------------------------------------------------------------

    [Fact]
    public async Task AskAsync_PrefersAProcessNamedInTheQuestion()
    {
        // "Onboard Client" and "Client Approval" both contain the word. Naming
        // one of them in full has to win.
        ChatReplyDto reply = await Sut.AskAsync("Tell me about client approval");

        reply.Sources[0].ProcessName.Should().Be("Client Approval");
    }

    [Fact]
    public async Task AskAsync_WhenSeveralMatch_SaysWhatElseItFound()
    {
        ChatReplyDto reply = await Sut.AskAsync("client");

        reply.Text.Should().Contain("Also matched");
    }

    [Fact]
    public async Task AskAsync_IgnoresCase()
    {
        ChatReplyDto upper = await Sut.AskAsync("INITIAL CONTACT");
        ChatReplyDto lower = await Sut.AskAsync("initial contact");

        upper.Sources[0].ProcessId.Should().Be(lower.Sources[0].ProcessId);
    }

    [Fact]
    public async Task AskAsync_MatchesOnPartOfAWord()
    {
        // "onboard" has to find "Onboarding" without dragging in a stemmer.
        ChatReplyDto reply = await Sut.AskAsync("onboard");

        reply.Sources.Should().NotBeEmpty();
    }

    // --- Listing by business unit ---------------------------------------------

    [Fact]
    public async Task AskAsync_WhichProcessesAUnitOwns_ListsThemRatherThanDescribingOne()
    {
        // This asked for a filter and got a lookup: it found the best-matching
        // Sales process and described that, which is a confident answer to a
        // question nobody asked.
        ChatReplyDto reply = await Sut.AskAsync("Which processes does Sales own?");

        // The three Sales processes in the seed. A live database will have
        // different assignments - the point is that all of that unit's
        // processes are listed, not that these three are eternal.
        reply.Text.Should().StartWith("Sales owns");
        reply.Text.Should().Contain("Initial Contact");
        reply.Text.Should().Contain("Gather Info");
        reply.Text.Should().Contain("Client Approval");
    }

    [Fact]
    public async Task AskAsync_WhichProcessesAUnitOwns_DoesNotExpandAnyOfThem()
    {
        // A list is a list. Pasting one process's steps into it makes the shape
        // of the answer disagree with the shape of the question.
        ChatReplyDto reply = await Sut.AskAsync("Which processes does Sales own?");

        reply.Text.Should().NotContain("Receive the inquiry");
    }

    [Fact]
    public async Task AskAsync_ListingByUnit_SaysWhereEachProcessSits()
    {
        ChatReplyDto reply = await Sut.AskAsync("List all processes owned by Sales");

        reply.Text.Should().Contain("Client Onboarding");
    }

    [Fact]
    public async Task AskAsync_ListingByUnit_CitesEveryProcessItNamed()
    {
        ChatReplyDto reply = await Sut.AskAsync("Which processes does Sales own?");

        reply.Sources.Should().NotBeEmpty();
        reply.Sources.Select(s => s.ProcessName).Should().Contain("Initial Contact");
    }

    [Fact]
    public async Task AskAsync_AboutAUnitWithoutAskingForAList_StillDescribesOneProcess()
    {
        // "Tell me about Sales" is not a request for an inventory.
        ChatReplyDto reply = await Sut.AskAsync("Tell me about the Sales work");

        reply.Text.Should().NotStartWith("Sales owns");
    }

    [Fact]
    public async Task AskAsync_WhoOwnsSomething_StillAnswersWithPeople()
    {
        // "Who" wins over "which" - the two branches must not fight.
        ChatReplyDto reply = await Sut.AskAsync("Who is responsible for initial contact?");

        reply.Text.Should().Contain("Sarah");
        reply.Text.Should().NotContain("owns 3 processes");
    }

    // --- The openers on the empty state ---------------------------------------

    [Theory]
    [InlineData("What happens during client onboarding?")]
    [InlineData("Who is responsible for preparing the proposal?")]
    [InlineData("What are the steps in initial contact?")]
    [InlineData("Which processes does Sales own?")]
    public async Task AskAsync_EverySuggestedOpener_ActuallyFindsSomething(string opener)
    {
        // These are the first thing anybody clicks. One of them shipped
        // answering the wrong question; this is what would have caught it.
        ChatReplyDto reply = await Sut.AskAsync(opener);

        reply.Text.Should().NotContain("could not find");
        reply.Sources.Should().NotBeEmpty();
    }

    public void Dispose()
    {
        Fixture.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>Part A: over the in-memory services.</summary>
public sealed class MockChatTests : ChatContractTests
{
    protected override ServiceFixture CreateFixture() => new MockServiceFixture();
}

/// <summary>Part B: the same answers, over EF Core.</summary>
public sealed class SqlChatTests : ChatContractTests
{
    protected override ServiceFixture CreateFixture() => new SqlServiceFixture();
}
