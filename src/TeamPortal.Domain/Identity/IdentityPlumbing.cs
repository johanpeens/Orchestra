using Microsoft.AspNetCore.Identity;

namespace TeamPortal.Domain.Identity;

public class AppUserRole : IdentityUserRole<int>
{
    public AppUser User { get; set; } = null!;

    public AppRole Role { get; set; } = null!;
}

public class AppUserClaim : IdentityUserClaim<int>;

public class AppUserLogin : IdentityUserLogin<int>;

public class AppUserToken : IdentityUserToken<int>;

public class AppRoleClaim : IdentityRoleClaim<int>;
