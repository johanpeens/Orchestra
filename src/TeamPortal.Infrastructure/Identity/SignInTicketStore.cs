using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace TeamPortal.Infrastructure.Identity;

public interface ISignInTicketStore
{
    /// <summary>Records a verified sign-in and returns the key to redeem it with.</summary>
    string Issue(int userId, bool isPersistent);

    /// <summary>
    /// Redeems a key, or returns null if it is unknown, expired or already used.
    /// A ticket never works twice.
    /// </summary>
    SignInTicket? Redeem(string ticket);
}

public sealed record SignInTicket(int UserId, bool IsPersistent, DateTimeOffset ExpiresAt);

internal sealed class InMemorySignInTicketStore : ISignInTicketStore
{
    /// <summary>
    /// Long enough for a redirect, short enough that a ticket left in a browser
    /// history or a proxy log is worthless by the time anyone finds it.
    /// </summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<string, SignInTicket> _tickets = new(StringComparer.Ordinal);

    public string Issue(int userId, bool isPersistent)
    {
        // 256 bits from the cryptographic generator. A guessable ticket would be
        // a way to sign in as somebody else without their password.
        string key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        _tickets[key] = new SignInTicket(userId, isPersistent, DateTimeOffset.UtcNow.Add(Lifetime));

        Sweep();

        return key;
    }

    public SignInTicket? Redeem(string ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket))
        {
            return null;
        }

        // Removed on the way out, so a replayed ticket finds nothing.
        if (!_tickets.TryRemove(ticket, out SignInTicket? found))
        {
            return null;
        }

        return found.ExpiresAt > DateTimeOffset.UtcNow ? found : null;
    }

    /// <summary>
    /// Drops anything that has expired. Cheap and good enough: the dictionary
    /// only ever holds tickets issued in the last two minutes, so it does not
    /// need a background timer to stay small.
    /// </summary>
    private void Sweep()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach (KeyValuePair<string, SignInTicket> entry in _tickets)
        {
            if (entry.Value.ExpiresAt <= now)
            {
                _tickets.TryRemove(entry.Key, out _);
            }
        }
    }
}
