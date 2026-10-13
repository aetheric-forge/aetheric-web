using System.Collections.Immutable;
using System.Text.Json;
using Aetheric.Provisioning.Engine;
using AethericForge.Web.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AethericForge.Web.Tests;

public sealed class ProvisionedParallelYouConfigurationTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), "parallel-you-manifest-" + Guid.NewGuid().ToString("N") + ".json");
    private static readonly string DeploymentId = new('a', 64);
    private static ConsumerConfiguration Manifest() => new(1, "parallel-you", DeploymentId, "parallel-you", "1.0.0", "test", new string('b', 64),
        new Dictionary<string, string>
        {
            ["ParallelYou:Enabled"] = "true", ["ParallelYou:MongoDb:Host"] = "mongo.example", ["ParallelYou:MongoDb:Port"] = "27017",
            ["ParallelYou:MongoDb:DatabaseName"] = "personal-library", ["ParallelYou:MongoDb:Username"] = "parallel-you-library",
            ["ParallelYou:MongoDb:Password"] = "mongo-consumer-secret", ["ParallelYou:MongoDb:AuthenticationDatabase"] = "admin",
            ["Workbench:Redis:Host"] = "redis.example", ["Workbench:Redis:Port"] = "6379", ["Workbench:Redis:Ssl"] = "true",
            ["Workbench:Redis:Database"] = "3", ["Workbench:Redis:User"] = "parallel-you-scoped", ["Workbench:Redis:Password"] = "redis-consumer-secret",
            ["Registry:Keycloak:Authority"] = "https://identity.example", ["Registry:Keycloak:Realm"] = "members",
            ["Registry:Keycloak:ClientId"] = "parallel-you-web", ["Registry:Keycloak:ClientSecret"] = "oidc-consumer-secret"
        }.ToImmutableDictionary()) { InstitutionName = "My Parallel You" };

    private ConfigurationManager Configuration()
    {
        var configuration = new ConfigurationManager();
        configuration["ParallelYou:ProvisionedConfigurationPath"] = _path;
        configuration["ParallelYou:DeploymentId"] = DeploymentId;
        configuration["ParallelYou:Environment"] = "test";
        return configuration;
    }

    [Fact]
    public void Artifact_loads_a_complete_revision_and_preserves_unrelated_host_settings()
    {
        File.WriteAllBytes(_path, JsonSerializer.SerializeToUtf8Bytes(Manifest()));
        var configuration = Configuration();
        configuration["ParallelYou:MongoDb:Password"] = "stale-secret";
        configuration["PublicSite:ProtectionKeyDirectory"] = "/persistent/keys";
        var manifest = ProvisionedParallelYouConfiguration.Apply(configuration);
        Assert.NotNull(manifest);
        Assert.Equal("mongo-consumer-secret", configuration["ParallelYou:MongoDb:Password"]);
        Assert.Equal("My Parallel You", configuration["ParallelYou:Institution:Name"]);
        Assert.Equal("/persistent/keys", configuration["PublicSite:ProtectionKeyDirectory"]);
        Assert.Equal("3", configuration["Workbench:Redis:Database"]);
    }

    [Fact]
    public void Wrong_environment_or_institution_is_rejected_before_settings_are_applied()
    {
        File.WriteAllBytes(_path, JsonSerializer.SerializeToUtf8Bytes(Manifest() with { Environment = "production" }));
        var configuration = Configuration();
        Assert.Throws<InvalidDataException>(() => ProvisionedParallelYouConfiguration.Apply(configuration));
        Assert.Null(configuration["ParallelYou:MongoDb:Password"]);
    }

    [Theory]
    [InlineData("Registry:Keycloak:ClientSecret", null)]
    [InlineData("RootCredentials:Directory", "/admin/root-credentials")]
    public void Partial_or_extra_settings_are_rejected(string key, string? value)
    {
        var manifest = Manifest();
        manifest = manifest with { Settings = value is null ? manifest.Settings.Remove(key) : manifest.Settings.Add(key, value) };
        File.WriteAllBytes(_path, JsonSerializer.SerializeToUtf8Bytes(manifest));
        Assert.Throws<InvalidDataException>(() => ProvisionedParallelYouConfiguration.Apply(Configuration()));
    }

    [Fact]
    public void Explicit_path_never_silently_falls_back_when_artifact_is_missing()
    {
        Assert.Throws<FileNotFoundException>(() => ProvisionedParallelYouConfiguration.Apply(Configuration()));
    }

    public void Dispose() => File.Delete(_path);
}
