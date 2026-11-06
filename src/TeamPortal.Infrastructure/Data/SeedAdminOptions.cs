using TeamPortal.Application.Common;

namespace TeamPortal.Infrastructure.Data;

public sealed class SeedAdminOptions
{
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Never in appsettings.json. `dotnet user-secrets set "SeedAdmin:Password"`
    /// in DEV; the pipeline's secret store above it.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Defaults to Admin, which is the only reason this exists.</summary>
    public string Role { get; set; } = AppRoles.Admin;

    public string? FullName { get; set; }

    /// <summary>
    /// True when there is enough to create the account. A half-filled section is
    /// skipped rather than guessed at - creating an administrator with a blank
    /// password because someone forgot to set a secret is the worst outcome here.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password);
}
