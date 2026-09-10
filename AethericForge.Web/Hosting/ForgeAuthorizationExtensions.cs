using System.Security.Claims;

namespace AethericForge.Web.Hosting;

public static class ForgeAuthorizationExtensions
{
    public const string AdministratorPolicy = "ForgeAdministrator";
    public const string AdministratorGroup = "/forge-admins";
    private const string MappedGroupsClaim =
        "http://schemas.microsoft.com/ws/2008/06/identity/claims/groups";

    public static IServiceCollection AddForgeAuthorization(
        this IServiceCollection services,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        var developmentAdministrators = environment.IsDevelopment()
            ? configuration.GetSection("Membership:DevelopmentAdministratorSubjects").Get<string[]>() ?? []
            : [];

        services.AddAuthorizationBuilder()
            .AddPolicy(AdministratorPolicy, policy => policy.RequireAssertion(context =>
                HasAdministratorGroup(context.User) ||
                IsDevelopmentAdministrator(context.User, developmentAdministrators)));

        return services;
    }

    private static bool HasAdministratorGroup(ClaimsPrincipal user) =>
        user.Claims
            .Where(claim =>
                string.Equals(claim.Type, "groups", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, MappedGroupsClaim, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(claim.Type, ClaimTypes.GroupSid, StringComparison.OrdinalIgnoreCase))
            .Any(claim =>
            string.Equals(claim.Value, AdministratorGroup, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(claim.Value, AdministratorGroup.TrimStart('/'), StringComparison.OrdinalIgnoreCase));

    private static bool IsDevelopmentAdministrator(ClaimsPrincipal user, IReadOnlyCollection<string> subjects)
    {
        var subject = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return subject is not null && subjects.Contains(subject, StringComparer.OrdinalIgnoreCase);
    }
}
