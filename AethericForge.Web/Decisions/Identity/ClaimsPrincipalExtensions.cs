using System.Security.Claims;

namespace AdrCampus.Web.Identity;

public static class ClaimsPrincipalExtensions
{
    public static string? GetSubjectId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
        principal.FindFirstValue("sub");
}
