using AngleSharp.Dom;
using Bunit;
using TeamPortal.Web.Components.Pages;
using TeamPortal.Web.Tests.Common;

namespace TeamPortal.Web.Tests.Screens;

public class ChatPageTests : ComponentTestBase
{
    private IRenderedComponent<ChatPage> RenderChat()
    {
        IRenderedComponent<ChatPage> cut = Render<ChatPage>();
        cut.WaitForAssertion(() => cut.FindAll("#chat-input").Should().NotBeEmpty());
        return cut;
    }

    private static IElement Input(IRenderedComponent<ChatPage> cut) => cut.Find("#chat-input");

    private static IElement SendButton(IRenderedComponent<ChatPage> cut) =>
        cut.Find(".app-chat__send");

    private static async Task AskAsync(IRenderedComponent<ChatPage> cut, string question)
    {
        await Input(cut).InputAsync(new() { Value = question });
        await cut.Find(".app-chat__composer").SubmitAsync();

        cut.WaitForAssertion(
            () => cut.FindAll(".app-chat__bubble--portal").Should().NotBeEmpty());
    }

    // --- Before anything is asked ---------------------------------------------

    [Fact]
    public void ChatPage_StartsEmptyWithSomethingToStartFrom()
    {
        // A blank box is the hardest thing to begin at.
        IRenderedComponent<ChatPage> cut = RenderChat();

        cut.FindAll(".app-chat__bubble").Should().BeEmpty();
        cut.FindAll(".app-chat__suggestion").Should().NotBeEmpty();
    }

    [Fact]
    public void ChatPage_WithNothingTyped_CannotSend()
    {
        IRenderedComponent<ChatPage> cut = RenderChat();

        SendButton(cut).HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public async Task ChatPage_WithSomethingTyped_CanSend()
    {
        IRenderedComponent<ChatPage> cut = RenderChat();

        await Input(cut).InputAsync(new() { Value = "What is initial contact?" });

        SendButton(cut).HasAttribute("disabled").Should().BeFalse();
    }

    // --- Asking ----------------------------------------------------------------

    [Fact]
    public async Task ChatPage_ShowsTheQuestionAndTheAnswer()
    {
        IRenderedComponent<ChatPage> cut = RenderChat();

        await AskAsync(cut, "What is initial contact?");

        cut.Find(".app-chat__bubble--person").TextContent
            .Should().Contain("What is initial contact?");
        cut.Find(".app-chat__bubble--portal").TextContent.Should().Contain("Initial Contact");
    }

    [Fact]
    public async Task ChatPage_PutsTheQuestionBeforeTheAnswer()
    {
        // The transcript has to read in the order it happened.
        IRenderedComponent<ChatPage> cut = RenderChat();

        await AskAsync(cut, "What is initial contact?");

        cut.FindAll(".app-chat__bubble")[0].ClassName
            .Should().Contain("app-chat__bubble--person");
    }

    [Fact]
    public async Task ChatPage_ClearsTheBoxAfterAsking()
    {
        IRenderedComponent<ChatPage> cut = RenderChat();

        await AskAsync(cut, "What is initial contact?");

        Input(cut).GetAttribute("value").Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ChatPage_TakesTheEmptyStateAwayOnceAsked()
    {
        IRenderedComponent<ChatPage> cut = RenderChat();

        await AskAsync(cut, "What is initial contact?");

        cut.FindAll(".app-chat__empty").Should().BeEmpty();
    }

    [Fact]
    public async Task ChatPage_ASuggestion_AsksItVerbatim()
    {
        IRenderedComponent<ChatPage> cut = RenderChat();
        string suggestion = cut.Find(".app-chat__suggestion").TextContent.Trim();

        await cut.Find(".app-chat__suggestion").ClickAsync(new());

        // The bubble carries a "You" heading above the message, so compare
        // against the message itself rather than the whole bubble.
        cut.WaitForAssertion(
            () => cut.Find(".app-chat__bubble--person .app-chat__text").TextContent.Trim()
                .Should().Be(suggestion));
    }

    [Fact]
    public async Task ChatPage_KeepsTheWholeConversation()
    {
        IRenderedComponent<ChatPage> cut = RenderChat();

        await AskAsync(cut, "What is initial contact?");
        await Input(cut).InputAsync(new() { Value = "Who is responsible for it?" });
        await cut.Find(".app-chat__composer").SubmitAsync();

        cut.WaitForAssertion(() => cut.FindAll(".app-chat__bubble").Should().HaveCount(4));
    }

    // --- Answers are checkable -------------------------------------------------

    [Fact]
    public async Task ChatPage_LinksBackToTheProcessTheAnswerCameFrom()
    {
        // An answer with no way back to the process it came from is a rumour.
        IRenderedComponent<ChatPage> cut = RenderChat();

        await AskAsync(cut, "What is initial contact?");

        cut.Find(".app-chat__source").GetAttribute("href")
            .Should().StartWith("/processes/");
    }

    [Fact]
    public async Task ChatPage_WhenNothingMatched_ShowsNoSources()
    {
        IRenderedComponent<ChatPage> cut = RenderChat();

        await AskAsync(cut, "hovercraft licensing");

        cut.Find(".app-chat__bubble--portal").TextContent.Should().Contain("could not find");
        cut.FindAll(".app-chat__source").Should().BeEmpty();
    }

    // --- Starting over ---------------------------------------------------------

    [Fact]
    public void ChatPage_WithNoConversation_OffersNoWayToClearIt()
    {
        IRenderedComponent<ChatPage> cut = RenderChat();

        cut.FindAll(".app-chat__clear").Should().BeEmpty();
    }

    [Fact]
    public async Task ChatPage_StartOver_EmptiesTheTranscript()
    {
        IRenderedComponent<ChatPage> cut = RenderChat();
        await AskAsync(cut, "What is initial contact?");

        await cut.Find(".app-chat__clear").ClickAsync(new());

        cut.WaitForAssertion(() => cut.FindAll(".app-chat__bubble").Should().BeEmpty());
        cut.FindAll(".app-chat__empty").Should().NotBeEmpty();
    }

    // --- Reachable without a mouse ---------------------------------------------

    [Fact]
    public void ChatPage_TheInputIsLabelled()
    {
        // The placeholder is not a label - it disappears the moment you type.
        IRenderedComponent<ChatPage> cut = RenderChat();

        cut.Find("label[for=chat-input]").TextContent.Trim().Should().NotBeEmpty();
    }

    [Fact]
    public void ChatPage_TheTranscriptAnnouncesNewMessages()
    {
        IRenderedComponent<ChatPage> cut = RenderChat();

        IElement log = cut.Find(".app-chat__log");

        log.GetAttribute("role").Should().Be("log");
        log.GetAttribute("aria-live").Should().Be("polite");
    }

    // --- The mascot -----------------------------------------------------------

    [Fact]
    public void ChatPage_ReservesTheSpaceBesideTheTranscript()
    {
        IRenderedComponent<ChatPage> cut = RenderChat();

        cut.FindAll(".app-chat__mascot").Should().ContainSingle();
    }

    [Fact]
    public void ChatPage_TheMascot_IsHiddenFromScreenReaders()
    {
        // It says nothing the transcript does not. Announcing it is noise
        // between the question and the answer.
        IRenderedComponent<ChatPage> cut = RenderChat();

        cut.Find(".app-chat__mascot").GetAttribute("aria-hidden").Should().Be("true");
    }

    [Fact]
    public void ChatPage_WithNoGifDropped_DrawsOneInstead()
    {
        // An img tag pointing at a file nobody added is a broken-image icon,
        // which looks worse than the drawn bot it was meant to replace.
        IRenderedComponent<ChatPage> cut = RenderChat();

        cut.FindAll(".app-chat__mascotgif").Should().BeEmpty();
        cut.FindAll(".app-chat__bot").Should().ContainSingle();
    }

    [Fact]
    public void ChatPage_TheMascot_SitsInTheRowWithTheTranscriptNotTheComposer()
    {
        // The composer spans the full width underneath both of them.
        IRenderedComponent<ChatPage> cut = RenderChat();

        IElement body = cut.Find(".app-chat__body");

        body.QuerySelectorAll(".app-chat__log").Should().ContainSingle();
        body.QuerySelectorAll(".app-chat__mascot").Should().ContainSingle();
        body.QuerySelectorAll(".app-chat__composer").Should().BeEmpty();
    }

    [Fact]
    public void ChatPage_AtRest_TheMascotIsNotThinking()
    {
        IRenderedComponent<ChatPage> cut = RenderChat();

        cut.Find(".app-chat__mascotart").ClassName
            .Should().NotContain("app-chat__mascotart--thinking");
    }
}
