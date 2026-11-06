using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.JSInterop;
using MudBlazor;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Dtos;
using TeamPortal.Web.Components.Dialogs;

namespace TeamPortal.Web.Components.Pages;

public partial class ChatPage : ComponentBase
{
    /// <summary>
    /// Openers for the empty state. Real questions against the seeded data, not
    /// slogans - one that returns "I could not find that" would teach the wrong
    /// thing on the first try.
    /// </summary>
    private static readonly string[] Suggestions =
    [
        "What happens during client onboarding?",
        "Who is responsible for preparing the proposal?",
        "What are the steps in initial contact?",
        "Which processes does Sales own?",
        "Who is on the team and what do they do?",
        "Show me the screenshot for receiving an inquiry"
    ];

    private readonly List<ChatMessageDto> _messages = [];

    private string _draft = string.Empty;
    private bool _thinking;
    private string? _error;

    [Inject]
    private IChatService Chat { get; set; } = null!;

    [Inject]
    private IJSRuntime Js { get; set; } = null!;

    [Inject]
    private IWebHostEnvironment Environment { get; set; } = null!;

    [Inject]
    private IDialogService Dialogs { get; set; } = null!;

    /// <summary>
    /// Artwork for the mascot, in the order it is preferred. Main.svg is the
    /// drawing that ships; the gif is still honoured so dropping an animated one
    /// in beats the static art without touching this file.
    /// </summary>
    private static readonly string[] MascotArtwork = ["img/chatbot.gif", "Main.svg"];

    /// <summary>
    /// The first one that is actually on disk, or null. Checked rather than
    /// assumed: an img tag pointing at a file nobody added renders a broken
    /// image icon, which looks worse than the drawn bot it replaced.
    /// </summary>
    private string? MascotArt => MascotArtwork
        .FirstOrDefault(path => Environment.WebRootFileProvider.GetFileInfo(path).Exists);

    /// <summary>
    /// The bot leans into a "thinking" pose while an answer is on its way. It is
    /// the only feedback that something is happening besides the word "Looking",
    /// and it is where the eye already is.
    /// </summary>
    private string MascotClass =>
        _thinking
            ? "app-chat__mascotart app-chat__mascotart--thinking"
            : "app-chat__mascotart";

    private bool CanSend => !_thinking && !string.IsNullOrWhiteSpace(_draft);

    private static string BubbleClass(ChatMessageDto message) =>
        message.Role == ChatRole.Person
            ? "app-chat__bubble app-chat__bubble--person"
            : "app-chat__bubble app-chat__bubble--portal";

    private Task SendAsync() => AskAsync(_draft);

    private async Task AskAsync(string question)
    {
        if (_thinking || string.IsNullOrWhiteSpace(question))
        {
            return;
        }

        _error = null;
        _draft = string.Empty;

        // The question goes up before the answer is asked for, so the transcript
        // reads in the order it happened even if the answer is slow.
        _messages.Add(new ChatMessageDto(ChatRole.Person, question.Trim(), DateTime.UtcNow));
        _thinking = true;

        await ScrollToEndAsync();

        try
        {
            // Everything before this question, which the page has anyway. A
            // model-backed chat needs it or "who is responsible for it?" has no
            // "it"; the built-in retriever ignores it. How much of it actually
            // goes to the model is Chat:HistoryTurns.
            IReadOnlyList<ChatMessageDto> history = [.. _messages[..^1]];

            ChatReplyDto reply = await Chat.AskAsync(question, history);

            _messages.Add(
                new ChatMessageDto(ChatRole.Portal, reply.Text, DateTime.UtcNow)
                {
                    Sources = reply.Sources,
                    Images = reply.Images
                });
        }
        catch (InvalidOperationException ex)
        {
            // The question stays in the transcript. Losing what you typed
            // because the answer failed is the worst part of a chat going wrong.
            _error = $"That question could not be answered: {ex.Message}";
        }
        finally
        {
            _thinking = false;
        }

        await ScrollToEndAsync();
    }

    /// <summary>
    /// Opens a screenshot at full size, in the same viewer the step detail uses.
    /// Scaled into a chat bubble it is recognisable and unreadable, and reading
    /// the text in it is usually the point.
    /// </summary>
    private Task OpenImageAsync(ChatImageDto image) =>
        ScreenshotDialog.ShowAsync(Dialogs, image.Url, image.FileName);

    private async Task ClearAsync()
    {
        _messages.Clear();
        _draft = string.Empty;
        _error = null;

        await Task.CompletedTask;
    }

    /// <summary>
    /// Keeps the newest message in view. Best-effort: on the prerender pass
    /// there is no JS runtime to call, and a chat that throws because it could
    /// not scroll would be a poor trade.
    /// </summary>
    private async Task ScrollToEndAsync()
    {
        StateHasChanged();

        try
        {
            await Js.InvokeVoidAsync("teamPortal.scrollToEnd", ".app-chat__log");
        }
        catch (JSException)
        {
            // No-op: the transcript is still correct, it is just not scrolled.
        }
        catch (InvalidOperationException)
        {
            // Prerendering - no circuit to call into yet.
        }
    }
}
