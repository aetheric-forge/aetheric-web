using System.Security.Claims;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Institutions.Campus;
using AethericForge.Web.Abstractions.Person;
using AethericForge.Web.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
namespace AethericForge.Web.Hosting;

public static class ForgeAuthenticationExtensions
{
    public const string ViewAsUserCookieName = "ViewAsUser";

    public static IServiceCollection AddForgeCampusAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var keycloakSection = InstitutionServiceConfiguration.Resolve(configuration, "Registry", "Keycloak").GetRequiredSection("Keycloak");
        var authority = keycloakSection["Authority"];
        var realm = keycloakSection["Realm"];

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.LoginPath = "/account/login";
                options.SlidingExpiration = true;
            })
            .AddOpenIdConnect(options =>
            {
                options.MapInboundClaims = false;
                options.Authority = $"{authority}/realms/{realm}";
                options.ClientId = keycloakSection["ClientId"];
                options.ClientSecret = keycloakSection["ClientSecret"];
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;
                options.SaveTokens = false;
                // Defaults to UseIfAvailable, which silently switches to a PAR (RFC 9126) backchannel
                // POST whenever Keycloak's discovery document advertises a PAR endpoint. Keycloak
                // 26.1's PAR endpoint validates redirect_uri more strictly than its regular authorize
                // endpoint, rejecting http://localhost redirect URIs that are otherwise correctly
                // registered on the client - PKCE already covers what PAR would add here, so disable it
                // rather than chase per-environment redirect_uri quirks through Keycloak.
                options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "preferred_username",
                    RoleClaimType = ClaimTypes.Role
                };
                options.Events.OnTokenValidated = RegisterPrincipalAsync;
            });

        services.AddScoped<ICurrentIdentityAccessor, CurrentIdentityAccessor>();
        services.AddScoped<ICurrentPersonAccessor, CurrentPersonAccessor>();

        return services;
    }

    public static IEndpointRouteBuilder MapForgeCampusAuthentication(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/account/login", Login)
            .AllowAnonymous();
        endpoints.MapPost("/account/logout", LogoutAsync);
        endpoints.MapPost("/account/view-as-user", ToggleViewAsUserAsync)
            .RequireAuthorization(ForgeAuthorizationExtensions.AdministratorPolicy);

        return endpoints;
    }

    private static IResult Login(HttpContext context)
    {
        var returnUrl = GetSafeReturnUrl(
            context.Request.Query["ReturnUrl"].ToString());

        return ChallengeOpenIdConnect(returnUrl);
    }

    private static IResult ChallengeOpenIdConnect(string returnUrl)
    {
        return Results.Challenge(
            new AuthenticationProperties
            {
                RedirectUri = returnUrl
            },
            [OpenIdConnectDefaults.AuthenticationScheme]);
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(context);
        var form = await context.Request.ReadFormAsync(cancellationToken);
        var returnUrl = GetSafeReturnUrl(form["returnUrl"].ToString());

        return Results.SignOut(
            new AuthenticationProperties
            {
                RedirectUri = returnUrl
            },
            [
                CookieAuthenticationDefaults.AuthenticationScheme,
                OpenIdConnectDefaults.AuthenticationScheme
            ]);
    }

    private static async Task<IResult> ToggleViewAsUserAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        await antiforgery.ValidateRequestAsync(context);
        var form = await context.Request.ReadFormAsync(cancellationToken);
        var returnUrl = GetSafeReturnUrl(form["returnUrl"].ToString());

        // A cookie + full-page-reload toggle rather than live client-side state, because most of the
        // site is static server-rendered (no SignalR circuit) and only a handful of admin pages opt
        // into InteractiveServer - an @onclick-driven toggle would silently do nothing on every static
        // page. This mirrors the sign-in/out forms, which hit the same static/interactive constraint.
        var isCurrentlyOn = context.Request.Cookies[ViewAsUserCookieName] == "1";
        if (isCurrentlyOn)
        {
            context.Response.Cookies.Delete(ViewAsUserCookieName);
        }
        else
        {
            context.Response.Cookies.Append(ViewAsUserCookieName, "1", new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                Path = "/"
            });
        }

        return Results.LocalRedirect(returnUrl);
    }

    private static async Task RegisterPrincipalAsync(TokenValidatedContext context)
    {
        var accessToken = context.TokenEndpointResponse?.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            context.Fail("Keycloak did not return an access token.");
            return;
        }

        var campus = context.HttpContext.RequestServices.GetRequiredService<ICampus>();
        var principalIdentity = await campus.Registry.Registrar.AuthenticateAsync(
            IdentityScheme.OpenIdConnect,
            new Dictionary<string, string>
            {
                ["token"] = accessToken
            },
            context.HttpContext.RequestAborted);

        if (principalIdentity is null || !principalIdentity.IsAuthenticated)
        {
            context.Fail("Forge Authentication rejected the Keycloak principal.");
            return;
        }

        if (context.Principal?.Identity is not ClaimsIdentity identity)
        {
            context.Fail("Keycloak did not produce a claims identity.");
            return;
        }

        var personService = context.HttpContext.RequestServices
            .GetRequiredService<IPersonService>();
        var person = await personService.GetOrCreatePersonAsync(
            principalIdentity.Subject,
            context.HttpContext.RequestAborted);

        identity.AddClaim(new Claim(
            "forge:identity-scheme",
            principalIdentity.Scheme.ToString()));
        identity.AddClaim(new Claim(
            "forge:person-id",
            person.Id.ToString("D")));

        if (!identity.HasClaim(claim => claim.Type == ClaimTypes.NameIdentifier))
        {
            identity.AddClaim(new Claim(
                ClaimTypes.NameIdentifier,
                principalIdentity.SubjectId));
        }

        if (!identity.HasClaim(claim => claim.Type == identity.NameClaimType))
        {
            identity.AddClaim(new Claim(
                identity.NameClaimType,
                principalIdentity.DisplayName ?? principalIdentity.SubjectId));
        }
    }

    private static string GetSafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) ||
            !returnUrl.StartsWith('/') ||
            (returnUrl.Length > 1 &&
             (returnUrl[1] == '/' || returnUrl[1] == '\\')))
        {
            return "/";
        }

        return returnUrl;
    }
}
