using Microsoft.AspNetCore.Identity;

namespace TeamPortal.Domain.Identity;

public class AppRole : IdentityRole<int>
{
    public string? Description { get; set; }

    public ICollection<AppUserRole> UserRoles { get; set; } = [];
}
