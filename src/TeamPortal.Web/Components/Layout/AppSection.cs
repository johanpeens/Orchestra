namespace TeamPortal.Web.Components.Layout;

public enum AppSection
{
    /// <summary>The landing page at "/". Where you arrive, and the default.</summary>
    Home = 0,

    /// <summary>Folders, processes and steps.</summary>
    Process = 1,
    Team = 2,
    Chat = 3,

    /// <summary>
    /// The whole portal as one JSON object, for handing to a chatbot. Reachable
    /// at /package and, still, at /context.
    /// </summary>
    Package = 4
}

public static class AppSections
{
    /// <summary>
    /// Reads the section off a base-relative path. Anything unrecognised is
    /// Home, because that is what "/" is and where an unknown route lands.
    /// </summary>
    public static AppSection FromPath(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);

        string path = relativePath.TrimStart('/');

        // The root is the landing page. Everything unrecognised is Home too -
        // an unknown route falls back there, and so should the chrome.
        if (path.Length == 0)
        {
            return AppSection.Home;
        }

        if (path.StartsWith("team", StringComparison.OrdinalIgnoreCase))
        {
            return AppSection.Team;
        }

        if (path.StartsWith("chat", StringComparison.OrdinalIgnoreCase))
        {
            return AppSection.Chat;
        }

        // Both spellings, because the page answers to both.
        if (path.StartsWith("package", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("context", StringComparison.OrdinalIgnoreCase))
        {
            return AppSection.Package;
        }

        return path.StartsWith("processes", StringComparison.OrdinalIgnoreCase)
            ? AppSection.Process
            : AppSection.Home;
    }

    /// <summary>
    /// Whether the folder rail belongs on screen. Only the process side has
    /// folders to list; the others would show an empty rail taking 264px.
    /// </summary>
    public static bool HasFolderRail(this AppSection section) => section == AppSection.Process;
}
