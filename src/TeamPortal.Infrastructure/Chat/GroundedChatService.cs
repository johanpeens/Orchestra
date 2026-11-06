using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Dtos;
using TeamPortal.Application.Services;

namespace TeamPortal.Infrastructure.Chat;

internal abstract partial class GroundedChatService : IChatService
{
    private readonly IPortalExportService _export;
    private readonly IChatService _fallback;
    private readonly ILogger _log;

    /// <summary>
    /// Compact, not indented: this is read by a model that is billed by the
    /// token, and the whitespace was a third of the payload.
    /// </summary>
    private static readonly JsonSerializerOptions Compact = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    protected GroundedChatService(
        IPortalExportService export,
        IChatService fallback,
        ChatOptions options,
        ILogger log)
    {
        _export = export;
        _fallback = fallback;
        _log = log;
        Options = options;
    }

    protected ChatOptions Options { get; }

    /// <summary>
    /// The one thing a provider has to supply: send this and give me back the
    /// text. Throws <see cref="ChatProviderException"/> for anything the caller
    /// should surface rather than crash on.
    /// </summary>
    protected abstract Task<string> CompleteAsync(
        string systemPrompt,
        IReadOnlyList<ChatMessageDto> history,
        string question,
        CancellationToken ct);

    public async Task<ChatReplyDto> AskAsync(
        string question,
        IReadOnlyList<ChatMessageDto>? history = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return await _fallback.AskAsync(question, history, ct).ConfigureAwait(false);
        }

        PortalExportDto portal = await _export
            .ExportAsync(Options.IncludeScreenshotData, ct)
            .ConfigureAwait(false);

        string grounding = Ground(portal);

        try
        {
            string answer = await CompleteAsync(
                SystemPrompt(grounding),
                Trim(history),
                question,
                ct).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(answer))
            {
                return await FallBackAsync(question, history, "the model returned nothing", ct)
                    .ConfigureAwait(false);
            }

            (string text, IReadOnlyList<ChatImageDto> images) = ExtractScreenshots(answer, portal);

            return new ChatReplyDto(text, CitedIn(text, portal)) { Images = images };
        }
        catch (ChatProviderException ex)
        {
            // A chat that stops working because a key expired is worse than one
            // that quietly does less. The built-in retriever still answers from
            // the same data, so the section keeps working while somebody fixes
            // the configuration.
            return await FallBackAsync(question, history, ex.Message, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return await FallBackAsync(question, history, "the model timed out", ct)
                .ConfigureAwait(false);
        }
    }

    private async Task<ChatReplyDto> FallBackAsync(
        string question, IReadOnlyList<ChatMessageDto>? history, string why, CancellationToken ct)
    {
        // The reason, never the key. No API key may reach a log.
        _log.LogWarning(
            "Chat provider {Provider} could not answer ({Reason}); using the built-in retriever.",
            Options.ResolvedProvider,
            why);

        ChatReplyDto reply = await _fallback.AskAsync(question, history, ct).ConfigureAwait(false);

        return reply with
        {
            Text = reply.Text
                + "\n\n(Answered from the portal directly - the AI model was unavailable.)"
        };
    }

    // --- Grounding ------------------------------------------------------------

    /// <summary>
    /// The export as JSON, trimmed if it is too big to send. Trimming drops
    /// step DESCRIPTIONS first and whole processes never: losing prose costs
    /// detail, losing a process makes the model confidently believe it does not
    /// exist, which is the one failure this design is meant to prevent.
    /// </summary>
    private string Ground(PortalExportDto portal)
    {
        string json = JsonSerializer.Serialize(portal, Compact);

        if (json.Length <= Options.MaxGroundingCharacters)
        {
            return json;
        }

        // People are not touched. The roster is a few hundred bytes and half of
        // what the chat is now asked about; trimming it to save space would be
        // trading the answer for the payload.
        PortalExportDto lean = portal with
        {
            Folders =
            [
                .. portal.Folders.Select(f => f with
                {
                    Processes =
                    [
                        .. f.Processes.Select(p => p with
                        {
                            Steps =
                            [
                                .. p.Steps.Select(s => s with
                                {
                                    Description = Shorten(s.Description),
                                    Screenshots = []
                                })
                            ]
                        })
                    ]
                })
            ]
        };

        string trimmed = JsonSerializer.Serialize(lean, Compact);

        if (_log.IsEnabled(LogLevel.Information))
        {
            _log.LogInformation(
                "Chat grounding trimmed from {Before} to {After} characters.",
                json.Length,
                trimmed.Length);
        }

        return trimmed;
    }

    private static string? Shorten(string? text) =>
        text is { Length: > 300 } ? text[..300] + "..." : text;

    /// <summary>
    /// What the model is allowed to do. The refusal instruction is the important
    /// line: a confident wrong answer about who signs off a client application is
    /// worse than "that is not written up".
    /// </summary>
    private static string SystemPrompt(string grounding) =>
        """
        You answer questions about one company's documented business processes for
        staff who work there.

        Everything you know is in the JSON below. It is the complete record: every
        folder, every process, the ordered steps inside each one, who is
        responsible for each step, which business unit owns it and what state it
        is in. It also carries "people" - the full staff roster, with each
        person's job title, business unit and whether they are an employee or
        management.

        Questions about people are as valid as questions about processes. Someone
        may appear in the roster and own no steps; that means they work here and
        are not assigned to anything documented, not that they do not exist.

        Rules, in order of importance:

        1. Answer ONLY from the JSON. If it does not say something, say that it is
           not written up. Never fill a gap with what a company like this one
           usually does - a plausible invention is worse than an admission,
           because the reader cannot tell them apart.
        2. The record contains NO information about anybody's gender. Never write
           "he", "she", "him" or "her" about a person in it - a first name is not
           evidence of anyone's gender, and guessing wrong misgenders a real
           colleague. Write "they", or repeat the person's name. This holds no
           matter how strongly a name suggests otherwise.
        3. Name the processes and steps you drew on, using their exact names from
           the JSON. Those names are matched afterwards to link your answer back
           to the pages it came from, so spelling them exactly matters.
        4. Be brief and concrete. Numbered steps when the question is about a
           sequence, names and their steps when it is about people.
        5. Plain text only. No markdown, no bold, no bullet characters - the
           answer is rendered as plain text and the syntax would show.
        6. If the question is not about these processes, say so rather than
           answering it from general knowledge.
        7. Some steps have screenshots. When one would genuinely help - the
           question is about what something looks like, or where to click, or
           the step is hard to follow in words - end your answer with a line of
           its own:
               [screenshot: EXACT STEP NAME]
           One per line, at most three, and only for steps whose "screenshots"
           list is not empty. Do not write image links yourself; that marker is
           replaced with the real picture. Leave it out when the words are
           enough - an answer decorated with a screenshot nobody asked for is
           just noise.

        The record:

        """
        + grounding;

    /// <summary>
    /// Pulls the [screenshot: ...] markers out of an answer and turns them into
    /// real images.
    ///
    /// The model only ever names a step. The URL comes from the portal's own
    /// record, so a name it invented resolves to nothing and quietly disappears
    /// instead of rendering a broken image - which is the whole reason it is not
    /// allowed to write links itself.
    /// </summary>
    private static (string Text, IReadOnlyList<ChatImageDto> Images) ExtractScreenshots(
        string answer, PortalExportDto portal)
    {
        MatchCollection markers = ScreenshotMarker().Matches(answer);

        if (markers.Count == 0)
        {
            return (answer.Trim(), []);
        }

        List<ChatImageDto> images = [];

        foreach (Match marker in markers)
        {
            string wanted = marker.Groups["step"].Value.Trim();

            foreach (ExportFolderDto folder in portal.Folders)
            {
                foreach (ExportProcessDto process in folder.Processes)
                {
                    foreach (ExportStepDto step in process.Steps)
                    {
                        if (!string.Equals(step.Name, wanted, StringComparison.OrdinalIgnoreCase)
                            || step.Screenshots.Count == 0)
                        {
                            continue;
                        }

                        ExportScreenshotDto shot = step.Screenshots[0];

                        if (!images.Any(i => i.Url == shot.Url))
                        {
                            images.Add(new ChatImageDto(
                                process.Name, step.Name, shot.FileName, shot.Url));
                        }
                    }
                }
            }
        }

        // The markers go, whether or not they resolved. A reader should never
        // see the plumbing, least of all a request for a picture that is not
        // there.
        string text = ScreenshotMarker().Replace(answer, string.Empty).Trim();

        return (text, images.Count > 3 ? [.. images.Take(3)] : images);
    }

    [GeneratedRegex(
        @"\[screenshot:\s*(?<step>[^\]]+)\]",
        RegexOptions.IgnoreCase)]
    private static partial Regex ScreenshotMarker();

    /// <summary>
    /// Which processes the answer actually named. Matching the model's prose
    /// back to the export is what makes the answer checkable - the reader gets
    /// links to the pages and can see for themselves whether it said the truth.
    /// </summary>
    private static IReadOnlyList<ChatSourceDto> CitedIn(string answer, PortalExportDto portal) =>
    [
        .. portal.Folders
            .SelectMany(folder => folder.Processes.Select(p => (Folder: folder, Process: p)))
            .Where(x =>
                x.Process.Name.Length > 2
                && answer.Contains(x.Process.Name, StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .Select(x => new ChatSourceDto(
                x.Folder.Id, x.Folder.Name, x.Process.Id, x.Process.Name))
    ];

    /// <summary>
    /// The last few turns, oldest first. Enough for "who is responsible for it?"
    /// to know what "it" was, without re-sending an hour of conversation on
    /// every question.
    /// </summary>
    private IReadOnlyList<ChatMessageDto> Trim(IReadOnlyList<ChatMessageDto>? history) =>
        history is null || history.Count == 0
            ? []
            : [.. history.TakeLast(Math.Max(0, Options.HistoryTurns))];

    /// <summary>Reads the text out of a provider's response, or explains why not.</summary>
    protected static string TextOrThrow(string? text, string provider) =>
        text ?? throw new ChatProviderException(
            $"{provider} returned a response with no text in it.");

    protected static StringBuilder Trimmed(string value, int max) =>
        new(value.Length > max ? value[..max] : value);
}

/// <summary>
/// Anything that went wrong reaching the model and that the caller should
/// handle rather than crash on. The message is logged; it must never carry the
/// API key.
/// </summary>
internal sealed class ChatProviderException : Exception
{
    public ChatProviderException(string message) : base(message) { }

    public ChatProviderException(string message, Exception inner) : base(message, inner) { }
}
