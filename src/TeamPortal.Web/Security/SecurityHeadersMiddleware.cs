namespace TeamPortal.Web.Security;

internal static class SecurityHeadersMiddleware
{
    // The app's typography comes from Google Fonts - see App.razor and
    // Frontend/FontUsage.md. The stylesheet and the font files are on two
    // different hosts, and both have to be named or the page silently falls back
    // to Times New Roman.
    private const string FontStyles = "https://fonts.googleapis.com";
    private const string FontFiles = "https://fonts.gstatic.com";

    public static IApplicationBuilder UseSecurityHeaders(
        this IApplicationBuilder app, bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            IHeaderDictionary headers = context.Response.Headers;

            // Trust the declared content type and nothing else. Without this a
            // browser may sniff an uploaded image as HTML and run it.
            headers["X-Content-Type-Options"] = "nosniff";

            // No framing. The app has no reason to be embedded, and clickjacking
            // needs a frame.
            headers["X-Frame-Options"] = "DENY";

            // Do not leak the path someone was on to whatever they click through
            // to - a process URL carries ids.
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // Nothing here uses a camera, a microphone or a location.
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

            headers["Content-Security-Policy"] = BuildCsp(isDevelopment);

            await next();
        });
    }

    private static string BuildCsp(bool isDevelopment)
    {
        List<string> directives =
        [
            "default-src 'self'",

            // 'unsafe-inline' on script-src, and it is not an oversight.
            //
            // Blazor's <ImportMap /> renders an inline <script type="importmap">
            // whose contents change with every asset fingerprint, so it can be
            // covered by neither a stable hash nor - in .NET 9 - a nonce, because
            // the component does not accept one. The choice is between allowing
            // inline script and deleting the import map, and deleting a piece of
            // the framework to satisfy a header is the worse trade.
            //
            // Revisit when ImportMap supports a nonce. Until then the useful
            // restrictions are the ones below: no eval, and nothing off-origin.
            "script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval'",

            // MudBlazor writes styles at runtime, so 'unsafe-inline' is required
            // here regardless. The font host is added because the app's own
            // stylesheet lives there.
            $"style-src 'self' 'unsafe-inline' {FontStyles}",

            // Uploads are data URIs while they are being previewed.
            "img-src 'self' data:",

            $"font-src 'self' {FontFiles} data:",

            "frame-ancestors 'none'",
            "base-uri 'self'",
            "form-action 'self'",
            "object-src 'none'"
        ];

        if (isDevelopment)
        {
            // Development tooling talks to ports this app does not own: Browser
            // Link and the hot-reload agent both open sockets on a random
            // localhost port, and the refresh script injects its own styles.
            //
            // Only in development. A policy that fights the tooling gets switched
            // off wholesale by the next person, which is worse than a policy that
            // is honest about where it stops.
            directives.Add("connect-src 'self' ws: wss: http://localhost:* https://localhost:*");
        }
        else
        {
            directives.Add("connect-src 'self' wss:");
        }

        return string.Join("; ", directives);
    }
}
