using AethericForge.Web.Hosting;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

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
}
