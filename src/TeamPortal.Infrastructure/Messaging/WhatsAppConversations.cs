using Microsoft.Extensions.Caching.Memory;
using TeamPortal.Application.Dtos;

namespace TeamPortal.Infrastructure.Messaging;

/// <summary>
/// What each number has been talking about, so "who is responsible for it?"
/// knows what "it" was.
///
/// In memory, with a sliding expiry, and deliberately not a table. The web chat
/// keeps its transcript in the circuit and lets it die with the visit; putting
/// WhatsApp threads in the database would quietly build the record of "who
/// asked what about this business" that the Chat page goes out of its way not
/// to keep. A restart forgets everything, which for a proof of concept is a
/// feature rather than a limitation.
/// </summary>
public interface IWhatsAppConversations
{
    IReadOnlyList<ChatMessageDto> History(string phoneNumber);

    void Record(string phoneNumber, ChatMessageDto question, ChatMessageDto answer);

    void Forget(string phoneNumber);
}

internal sealed class WhatsAppConversations : IWhatsAppConversations
{
    /// <summary>
    /// Older turns than this are dropped. A model billed by the token does not
    /// need an hour of backscroll to resolve a pronoun.
    /// </summary>
    private const int MaxTurns = 10;

    private readonly IMemoryCache _cache;
    private readonly TimeSpan _life;

    public WhatsAppConversations(IMemoryCache cache, WhatsAppOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _cache = cache;
        _life = TimeSpan.FromMinutes(Math.Clamp(options.ConversationMinutes, 1, 24 * 60));
    }

    public IReadOnlyList<ChatMessageDto> History(string phoneNumber) =>
        _cache.TryGetValue(Key(phoneNumber), out List<ChatMessageDto>? found) && found is not null
            ? found
            : [];

    public void Record(string phoneNumber, ChatMessageDto question, ChatMessageDto answer)
    {
        List<ChatMessageDto> thread = [.. History(phoneNumber), question, answer];

        if (thread.Count > MaxTurns * 2)
        {
            thread = [.. thread.TakeLast(MaxTurns * 2)];
        }

        // Sliding, not absolute: a conversation stays warm while it is in use
        // and lapses once it is genuinely over.
        _cache.Set(Key(phoneNumber), thread, new MemoryCacheEntryOptions
        {
            SlidingExpiration = _life
        });
    }

    public void Forget(string phoneNumber) => _cache.Remove(Key(phoneNumber));

    /// <summary>
    /// Prefixed, because this cache is shared with whatever else the app puts in
    /// it and a bare phone number is a plausible key for something else.
    /// </summary>
    private static string Key(string phoneNumber) => $"whatsapp::{phoneNumber}";
}
