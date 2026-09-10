using System.Security.Claims;
using AdrCampus.Web.Identity;
using Microsoft.AspNetCore.Authorization;

namespace AethericForge.Web.Hosting;

public static class ForgeAuthorizationExtensions
{
    public const string AdministratorPolicy = "ForgeAdministrator";
    public const string AdministratorGroup = "/forge-admins";

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
                IsDevelopmentAdministrator(context.User, developmentAdministrators)))
            .AddPolicy(IdentityPolicies.ActiveMember, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new ActiveMemberRequirement());
            })
            .AddPolicy(IdentityPolicies.ActiveMaintainer, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AddRequirements(new ActiveMaintainerRequirement());
            });

        services.AddScoped<IAuthorizationHandler, ActiveMemberAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, ActiveMaintainerAuthorizationHandler>();

        return services;
    }

    private static bool HasAdministratorGroup(ClaimsPrincipal user) =>
        user.FindAll("groups").Any(claim =>
            string.Equals(claim.Value, AdministratorGroup, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(claim.Value, AdministratorGroup.TrimStart('/'), StringComparison.OrdinalIgnoreCase));

    private static bool IsDevelopmentAdministrator(ClaimsPrincipal user, IReadOnlyCollection<string> subjects)
    {
        var subject = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return subject is not null && subjects.Contains(subject, StringComparer.OrdinalIgnoreCase);
    }
}
