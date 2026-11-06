using System.Net;
using TeamPortal.Application.Abstractions;

namespace TeamPortal.Application.Common;

public static class EmailBodies
{
    private const string ProductName = "Orchestra";

    /// <summary>How long a confirmation or reset link is worth clicking.</summary>
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromHours(24);

    public static EmailMessage ConfirmEmail(string to, string fullName, string confirmUrl) =>
        new(
            to,
            $"Confirm your {ProductName} account",
            Wrap(
                $"Hello {Escape(fullName)},",
                "Someone - we hope it was you - created a " + ProductName +
                " account with this email address. Confirm it to finish signing up.",
                "Confirm my account",
                confirmUrl,
                "If it was not you, ignore this email. The account cannot be used until " +
                "someone confirms it."),
            Plain(
                $"Hello {fullName},",
                $"Confirm your {ProductName} account by opening this link:",
                confirmUrl,
                "If it was not you, ignore this email. The account cannot be used until " +
                "someone confirms it."));

    /// <summary>
    /// Sent when an administrator approves the account. This is the only message
    /// in the flow that is unambiguously good news, so it says what to do next
    /// rather than explaining what happened.
    /// </summary>
    public static EmailMessage AccountApproved(string to, string fullName, string signInUrl) =>
        new(
            to,
            $"Your {ProductName} account is ready",
            Wrap(
                $"Hello {Escape(fullName)},",
                "An administrator has approved your account. You can sign in now.",
                "Sign in",
                signInUrl,
                "You start with read-only access. Ask an administrator if you need to be " +
                "able to change things."),
            Plain(
                $"Hello {fullName},",
                "An administrator has approved your account. Sign in here:",
                signInUrl,
                "You start with read-only access. Ask an administrator if you need to be " +
                "able to change things."));

    public static EmailMessage ResetPassword(string to, string fullName, string resetUrl) =>
        new(
            to,
            $"Reset your {ProductName} password",
            Wrap(
                $"Hello {Escape(fullName)},",
                "A password reset was requested for this account. The link below works " +
                "once, and expires in 24 hours.",
                "Choose a new password",
                resetUrl,
                "If you did not ask for this, ignore this email. Your password has not " +
                "changed."),
            Plain(
                $"Hello {fullName},",
                "A password reset was requested for this account. This link works once " +
                "and expires in 24 hours:",
                resetUrl,
                "If you did not ask for this, ignore this email. Your password has not " +
                "changed."));

    /// <summary>
    /// Deliberately plain markup: table-based, inline styles, no external assets.
    /// Every rule about writing HTML for a browser is wrong for an email client.
    /// </summary>
    private static string Wrap(
        string greeting, string body, string buttonText, string url, string footer) =>
        $"""
        <!doctype html>
        <html><body style="margin:0;padding:24px;background:#F6F4F8;font-family:Segoe UI,Arial,sans-serif;color:#1E1726;">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;margin:0 auto;background:#FFFFFF;border-radius:10px;padding:32px;">
            <tr><td>
              <p style="margin:0 0 4px;font-size:13px;letter-spacing:.12em;text-transform:uppercase;color:#7C6F8C;">{ProductName}</p>
              <p style="margin:0 0 16px;font-size:16px;">{greeting}</p>
              <p style="margin:0 0 24px;font-size:15px;line-height:1.6;">{body}</p>
              <p style="margin:0 0 24px;">
                <a href="{Escape(url)}" style="display:inline-block;background:#4A2767;color:#FFFFFF;text-decoration:none;padding:12px 22px;border-radius:6px;font-size:15px;">{buttonText}</a>
              </p>
              <p style="margin:0 0 8px;font-size:13px;color:#57496A;">If the button does not work, copy this into your browser:</p>
              <p style="margin:0 0 24px;font-size:13px;word-break:break-all;color:#4A2767;">{Escape(url)}</p>
              <p style="margin:0;font-size:13px;color:#7C6F8C;line-height:1.6;">{footer}</p>
            </td></tr>
          </table>
        </body></html>
        """;

    private static string Plain(string greeting, string body, string url, string footer) =>
        string.Join(
            Environment.NewLine + Environment.NewLine,
            ProductName, greeting, body, url, footer);

    /// <summary>
    /// A name comes from a registration form and goes into an HTML email. Encode
    /// it - the fact that the recipient is the person who typed it is not a
    /// reason to skip this, because the address on the form is not proof of who
    /// owns it.
    /// </summary>
    private static string Escape(string value) => WebUtility.HtmlEncode(value);
}
