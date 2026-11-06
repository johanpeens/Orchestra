using Microsoft.AspNetCore.Mvc;
using TeamPortal.Application.Abstractions;
using TeamPortal.Application.Common;

namespace TeamPortal.Web.Endpoints;

internal static class MediaEndpoints
{
    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet($"{MediaPaths.Prefix}{{**storedPath}}", (
                string storedPath,
                [FromServices] IFileReader store) =>
            {
                Stream? content = store.OpenRead(storedPath);

                if (content is null)
                {
                    // Same answer for "not there" and "outside the root", so the
                    // endpoint cannot be used to probe the filesystem.
                    return Results.NotFound();
                }

                return Results.File(content, ContentTypeFor(storedPath));
            })
            // The whole point of this endpoint. Without it, it is wwwroot with
            // extra steps.
            .RequireAuthorization()
            .WithName("Media");

        return endpoints;
    }

    private static string ContentTypeFor(string storedPath) =>
        Path.GetExtension(storedPath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
}
