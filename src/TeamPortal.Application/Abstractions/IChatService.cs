using TeamPortal.Application.Dtos;

namespace TeamPortal.Application.Abstractions;

public interface IChatService
{
    /// <summary>
    /// Answers a question about the portal's contents. Never throws for a
    /// question it cannot answer - it says it cannot, and names what it does
    /// hold, so the asker can correct themselves.
    /// </summary>
    /// <param name="history">
    /// Earlier turns, oldest first. Optional, and ignored by the built-in
    /// retriever, which treats every question on its own. A model-backed
    /// implementation needs it or "who is responsible for it?" has no "it".
    /// </param>
    Task<ChatReplyDto> AskAsync(
        string question,
        IReadOnlyList<ChatMessageDto>? history = null,
        CancellationToken ct = default);
}
