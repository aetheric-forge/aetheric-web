using System.Security.Claims;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Institutions.Campus;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace AethericForge.Web.Hosting;

public static class ForgeAuthenticationExtensions
{
    public static IEndpointRouteBuilder MapForgeCampusAuthentication(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/account/login", LoginAsync);
        endpoints.MapPost("/account/logout", LogoutAsync);

        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        ICampus campus,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(context);
        var form = await context.Request.ReadFormAsync(cancellationToken);

        var username = form["username"].ToString();
        var password = form["password"].ToString();
        var returnUrl = GetSafeReturnUrl(form["returnUrl"].ToString());

        var principalIdentity = await campus.Registry.AuthenticateAsync(
            IdentityScheme.Local,
            new Dictionary<string, string>
            {
                ["username"] = username,
                ["password"] = password
            },
            cancellationToken);

        if (principalIdentity is null || !principalIdentity.IsAuthenticated)
        {
            var encodedReturnUrl = Uri.EscapeDataString(returnUrl);
            return Results.LocalRedirect($"/login?error=invalid&returnUrl={encodedReturnUrl}");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, principalIdentity.SubjectId),
            new Claim(ClaimTypes.Name, principalIdentity.DisplayName ?? principalIdentity.SubjectId),
            new Claim("forge:identity-scheme", principalIdentity.Scheme.ToString())
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return Results.LocalRedirect(returnUrl);
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        IAntiforgery antiforgery)
    {
        await antiforgery.ValidateRequestAsync(context);
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.LocalRedirect("/");
    }

    private static string GetSafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) ||
            !returnUrl.StartsWith('/') ||
            returnUrl.StartsWith("//", StringComparison.Ordinal))
        {
            return "/";
        }

        return returnUrl;
    }
}
