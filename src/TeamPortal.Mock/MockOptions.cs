namespace TeamPortal.Mock;

public sealed class MockOptions
{
    /// <summary>
    /// Pause before a read answers. Non-zero by default so every screen has to show
    /// a loading state; a UI built against instant reads never grows one.
    /// </summary>
    public TimeSpan ReadDelay { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <summary>Pause before a write answers, so Save buttons have to be disabled.</summary>
    public TimeSpan WriteDelay { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// When true, the next write fails with a form-level error and then resets itself.
    /// Wired to a dev-only toggle so error paths can be demonstrated on request, and
    /// stripped from Release builds in phase B5.
    /// </summary>
    public bool FailNextWrite { get; set; }

    /// <summary>
    /// Registers IDevRoleSwitcher so the account menu can offer "View as".
    /// Program.cs turns this on only when the environment is Development.
    /// </summary>
    public bool EnableDevRoleSwitcher { get; set; }

    /// <summary>Zero delays, for tests. Nobody wants a suite that sleeps.</summary>
    public static MockOptions Instant => new()
    {
        ReadDelay = TimeSpan.Zero,
        WriteDelay = TimeSpan.Zero
    };

    /// <summary>Consumes the one-shot failure switch.</summary>
    internal bool ConsumeFailureFlag()
    {
        if (!FailNextWrite)
        {
            return false;
        }

        FailNextWrite = false;
        return true;
    }
}
