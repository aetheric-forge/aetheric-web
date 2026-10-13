using AethericForge.Web.Hosting;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace AethericForge.Web.Tests;

public class PublicAuthenticationTests
{
    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
    {
        ["Keycloak:Authority"] = "https://identity.example",
        ["Keycloak:Realm"] = "members",
        ["Registry:Keycloak:ClientId"] = "web",
        ["Registry:Keycloak:ClientSecret"] = "test-secret"
    }).Build();

    [Fact]
    public void Public_signin_validates_oidc_without_campus_or_person_services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddForgeCampusAuthentication(Configuration(), registerCampusIdentity: false);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(OpenIdConnectDefaults.AuthenticationScheme);
        Assert.Equal("https://identity.example/realms/members", options.Authority);
        Assert.Equal("web", options.ClientId);
        Assert.Equal("code", options.ResponseType);
        Assert.True(options.UsePkce);
        Assert.False(options.SaveTokens);
        Assert.DoesNotContain(services, x => x.ServiceType.Name is "ICurrentPersonAccessor" or "ICurrentIdentityAccessor");
    }

    [Fact]
    public void Campus_signin_retains_its_identity_accessors()
    {
        var services = new ServiceCollection();
        services.AddForgeCampusAuthentication(Configuration());
        Assert.Contains(services, x => x.ServiceType.Name == "ICurrentPersonAccessor");
        Assert.Contains(services, x => x.ServiceType.Name == "ICurrentIdentityAccessor");
    }

    [Fact]
    public async Task Standalone_module_uses_validated_oidc_subject_without_resolving_a_campus()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddForgeCampusAuthentication(Configuration(), registerCampusIdentity: false, registerParallelYouIdentity: true);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(OpenIdConnectDefaults.AuthenticationScheme);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("sub", "validated-subject"),
            new Claim(ClaimTypes.NameIdentifier, "untrusted-alias"),
            new Claim("forge:identity-scheme", "Local")
        }, "oidc");
        var context = new TokenValidatedContext(new DefaultHttpContext(),
            new AuthenticationScheme("oidc", "oidc", typeof(OpenIdConnectHandler)), options, new ClaimsPrincipal(identity), new AuthenticationProperties());
        await options.Events.OnTokenValidated(context);
        Assert.Equal("validated-subject", identity.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal("OpenIdConnect", Assert.Single(identity.FindAll("forge:identity-scheme")).Value);
        Assert.Null(context.Result);
    }

    [Fact]
    public async Task Standalone_module_rejects_a_principal_without_a_subject()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddForgeCampusAuthentication(Configuration(), registerCampusIdentity: false, registerParallelYouIdentity: true);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get(OpenIdConnectDefaults.AuthenticationScheme);
        var context = new TokenValidatedContext(new DefaultHttpContext(),
            new AuthenticationScheme("oidc", "oidc", typeof(OpenIdConnectHandler)), options, new ClaimsPrincipal(new ClaimsIdentity("oidc")), new AuthenticationProperties());
        await options.Events.OnTokenValidated(context);
        Assert.NotNull(context.Result?.Failure);
    }
}
