using Microsoft.AspNetCore.WebUtilities;
using TeamPortal.Application.Abstractions;

namespace TeamPortal.Infrastructure.Email;

public sealed class AppUrlProvider : IAppUrlProvider
{
    public AppUrlProvider(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException(
                "The application's base URL is needed to build links for emails. " +
                "Set App:BaseUrl in configuration.",
                nameof(baseUrl));
        }

        BaseUrl = baseUrl.TrimEnd('/');
    }

    public string BaseUrl { get; }

    public string Absolute(string relativePath, params (string Key, string Value)[] query)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentNullException.ThrowIfNull(query);

        string url = $"{BaseUrl}/{relativePath.TrimStart('/')}";

        foreach ((string key, string value) in query)
        {
            url = QueryHelpers.AddQueryString(url, key, value);
        }

        return url;
    }
}
