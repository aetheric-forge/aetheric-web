using Microsoft.Extensions.DependencyInjection;
using AethericForge.Web.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AethericForge.Web.Tests;

public class InstitutionServiceConfigurationTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(v =>
            new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

    [Fact]
    public void ResolvesEachSettingAndPreservesFalseZeroAndEmptyOverrides()
    {
        var result = InstitutionServiceConfiguration.Resolve(Config(
            ("Redis:Host", "shared"),
            ("Redis:Port", "6379"),
            ("Redis:Ssl", "true"),
            ("Redis:Database", "4"),
            ("A:Redis:Host", ""),
            ("A:Redis:Ssl", "false"),
            ("A:Redis:Database", "0"),
            ("A:Redis:Password", "local")), "A", "Redis");
        Assert.Equal("", result["Redis:Host"]);
        Assert.Equal(6379, result.GetValue<int>("Redis:Port"));
        Assert.False(result.GetValue<bool>("Redis:Ssl"));
        Assert.Equal(0, result.GetValue<int>("Redis:Database"));
        Assert.Equal("local", result["Redis:Password"]);
    }

    [Theory]
    [InlineData("Keycloak", "ClientId", "ClientSecret")]
    [InlineData("MongoDb", "Username", "Password")]
    [InlineData("RabbitMq", "Username", "Password")]
    [InlineData("S3", "AccessKey", "SecretKey")]
    public void RequiresCompleteLocalCredentialPair(string service, string user, string secret)
    {
        var config = Config(
            ($"{service}:{secret}", "platform-secret"),
            ($"B:{service}:{secret}", "other-secret"),
            ($"A:{service}:{user}", "local-user"));
        var error = Assert.Throws<InvalidOperationException>(() =>
            InstitutionServiceConfiguration.Resolve(config, "A", service));
        Assert.Contains($"A:{service}:{secret}", error.Message);
        Assert.DoesNotContain("platform-secret", error.Message);
        Assert.DoesNotContain("other-secret", error.Message);
    }

    [Fact]
    public void RedisDoesNotInheritPlatformAclUser()
    {
        var result = InstitutionServiceConfiguration.Resolve(Config(
            ("Redis:User", "admin"),
            ("A:Redis:Password", "local")), "A", "Redis");
        Assert.Null(result["Redis:User"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void MissingOrBlankPasswordCannotUseLegacyOrPlatformCredentials(string? password)
    {
        Assert.Throws<InvalidOperationException>(() => InstitutionServiceConfiguration.Resolve(Config(
            ("Other:Redis:Password", "other"),
            ("Redis:Password", "shared"),
            ("A:Redis:Password", password)), "A", "Redis"));
    }

    [Fact]
    public void RejectsOpaqueConnectionStringsWithoutDisclosingValues()
    {
        var error = Assert.Throws<InvalidOperationException>(() => InstitutionServiceConfiguration.Resolve(Config(
            ("MongoDb:ConnectionString", "sensitive")), "A", "MongoDb"));
        Assert.DoesNotContain("sensitive", error.Message);
    }
    [Fact]
    public void CampusDoesNotReplaceTheModulesScopedMongoClients()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddForgeCampus();
        foreach (var module in new[] { "Library", "ParallelYou", "Decisions" })
            Assert.DoesNotContain(services, descriptor => descriptor.IsKeyedService
                && Equals(descriptor.ServiceKey, module)
                && descriptor.ServiceType == typeof(MongoDB.Driver.IMongoClient));
        Assert.DoesNotContain(services, descriptor => !descriptor.IsKeyedService
            && descriptor.ServiceType == typeof(MongoDB.Driver.IMongoClient));
    }

    [Fact]
    public void CampusRegistersOnlyWorkbenchStagingRedisConnection()
    {
        var services = new ServiceCollection();
        services.AddForgeCampus();

        var connection = Assert.Single(services.Where(descriptor =>
            descriptor.ServiceType == typeof(StackExchange.Redis.IConnectionMultiplexer)));
        Assert.Equal("Workbench", connection.ServiceKey);
        Assert.Equal(ServiceLifetime.Singleton, connection.Lifetime);
    }

    [Theory]
    [InlineData("https://user:secret@example.com")]
    [InlineData("https://example.com?token=secret")]
    public void RejectsCredentialsEmbeddedInEndpoints(string endpoint)
    {
        var error = Assert.Throws<InvalidOperationException>(() => InstitutionServiceConfiguration.Resolve(Config(
            ("S3:ServiceUrl", endpoint), ("Archive:S3:AccessKey", "local"),
            ("Archive:S3:SecretKey", "local")), "Archive", "S3"));
        Assert.DoesNotContain("secret", error.Message);
    }

}
