using Microsoft.AspNetCore.Mvc;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Dtos;
using TeamPortal.Infrastructure.Messaging;

namespace TeamPortal.Web.Endpoints;

/// <summary>
/// The bot, reachable from WhatsApp. It answers from the same IChatService the
/// Chat screen uses, so a wrong answer here is reproducible in the playground -
/// which is the entire reason the playground exists.
/// </summary>
internal static class WhatsAppEndpoints
{
    public const string IncomingRoute = "/api/whatsapp/incoming";

    public static IEndpointRouteBuilder MapWhatsAppEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost(IncomingRoute, HandleAsync)
            // Anonymous of necessity: Twilio has no cookie and never will. The
            // signature check inside is what stands in for authentication, and
            // without it this URL is a public box that answers questions about
            // the business to anyone who finds it.
            .AllowAnonymous()
            .WithName("WhatsAppIncoming");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        [FromServices] WhatsAppOptions options,
        [FromServices] IServiceProvider services,
        [FromServices] ILoggerFactory logs,
        CancellationToken ct)
    {
        ILogger log = logs.CreateLogger("WhatsApp");

        if (!options.IsUsable)
        {
            // Not configured is not an error worth explaining to a caller.
            return Results.NotFound();
        }

        if (!request.HasFormContentType)
        {
            return Results.BadRequest();
        }

        IFormCollection form = await request.ReadFormAsync(ct).ConfigureAwait(false);

        List<KeyValuePair<string, string>> fields =
        [
            .. form.Select(f => new KeyValuePair<string, string>(f.Key, f.Value.ToString()))
        ];

        // The URL TWILIO called, from configuration - not the one this app sees.
        // Behind a tunnel or App Service's proxy the scheme and host differ, and
        // the signature is over Twilio's version.
        string signedUrl = string.IsNullOrWhiteSpace(options.PublicWebhookUrl)
            ? $"{request.Scheme}://{request.Host}{request.Path}"
            : options.PublicWebhookUrl;

        if (!WhatsAppSignature.IsFromTwilio(
                signedUrl,
                fields,
                request.Headers["X-Twilio-Signature"].ToString(),
                options.AuthToken!))
        {
            log.LogWarning("Rejected a WhatsApp webhook with a bad signature.");

            // 403 and nothing else. Saying WHY it failed tells a forger which
            // half they got wrong.
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        string from = form["From"].ToString();
        string body = form["Body"].ToString().Trim();

        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(body))
        {
            return Results.Ok();
        }

        // Scoped by hand. IChatService is scoped and this endpoint is outside
        // any request scope the DI container would build for a signed-in user.
        using IServiceScope scope = services.CreateScope();

        IWhatsAppConversations conversations =
            scope.ServiceProvider.GetRequiredService<IWhatsAppConversations>();
        IWhatsAppSender sender =
            scope.ServiceProvider.GetRequiredService<IWhatsAppSender>();
        IChatService chat =
            scope.ServiceProvider.GetRequiredService<IChatService>();

        if (body.Equals("reset", StringComparison.OrdinalIgnoreCase)
            || body.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            conversations.Forget(from);
            await sender.SendAsync(from, "Started a fresh conversation.", ct).ConfigureAwait(false);
            return Results.Ok();
        }

        // Capped below Twilio's own webhook timeout: the answer is generated
        // inside this request, and a model that stalls must produce an apology
        // rather than a dropped webhook Twilio then retries.
        using CancellationTokenSource timeout = CancellationTokenSource
            .CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.AnswerTimeoutSeconds));

        try
        {
            ChatReplyDto reply = await chat
                .AskAsync(body, conversations.History(from), timeout.Token)
                .ConfigureAwait(false);

            conversations.Record(
                from,
                new ChatMessageDto(ChatRole.Person, body, DateTime.UtcNow),
                new ChatMessageDto(ChatRole.Portal, reply.Text, DateTime.UtcNow));

            await sender.SendAsync(from, reply.Text, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            log.LogWarning("A WhatsApp answer timed out after {Seconds}s.",
                options.AnswerTimeoutSeconds);

            await sender.SendAsync(
                from, "That took too long to answer. Try asking it again.", ct)
                .ConfigureAwait(false);
        }

        // Empty 200. The reply goes out through the REST API above rather than
        // as TwiML in this response, so a slow answer cannot make Twilio give up
        // on the webhook and retry the whole question.
        return Results.Ok();
    }
}
