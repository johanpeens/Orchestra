using TeamPortal.Application.Dtos;

namespace TeamPortal.Application.Abstractions;

public interface IDevRoleSwitcher
{
    /// <summary>
    /// Signs in as a seeded account holding the given role, without a password.
    /// Returns null when no such account exists.
    /// </summary>
    CurrentUserDto? SwitchToRole(string role);
}
