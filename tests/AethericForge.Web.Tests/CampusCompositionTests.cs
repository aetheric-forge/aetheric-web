using AethericForge.Web.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AethericForge.Web.Tests;

public sealed class CampusCompositionTests
{
    [Fact]
    public void Campus_constructor_dependencies_are_registered_before_any_external_connection()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(TimeProvider.System);
        services.AddForgeCampus();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}
