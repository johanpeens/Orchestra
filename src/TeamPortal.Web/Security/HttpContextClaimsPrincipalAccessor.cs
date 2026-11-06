using System.Globalization;
using System.Security.Claims;
using TeamPortal.Application.Dtos;
using TeamPortal.Infrastructure.Identity;

namespace TeamPortal.Web.Security;

internal sealed class HttpContextClaimsPrincipalAccessor : IClaimsPrincipalAccessor
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextClaimsPrincipalAccessor(IHttpContextAccessor accessor) => _accessor = accessor;

    public CurrentUserDto? Current
    {
        get
        {
            ClaimsPrincipal? principal = _accessor.HttpContext?.User;

            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            string? id = principal.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(id, CultureInfo.InvariantCulture, out int userId))
            {
                return null;
            }

            return new CurrentUserDto(
                userId,
                principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
                principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
                PhotoUrl: null,
                [.. principal.FindAll(ClaimTypes.Role).Select(c => c.Value)]);
        }
    }
}
