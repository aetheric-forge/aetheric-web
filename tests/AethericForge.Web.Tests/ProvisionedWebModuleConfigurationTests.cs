using System.Collections.Immutable;
using System.Text.Json;
using Aetheric.Provisioning.Engine;
using AethericForge.Web.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AethericForge.Web.Tests;

public sealed class ProvisionedWebModuleConfigurationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "module-manifests-" + Guid.NewGuid().ToString("N"));
    private ConfigurationManager Configuration()
    {
        var configuration = new ConfigurationManager();
        configuration["Provisioning:Consumers:Directory"] = _directory;
        configuration["Provisioning:Consumers:Environment"] = "production";
        configuration["Registry:Keycloak:ClientSecret"] = "existing-sign-in-secret";
        return configuration;
    }
    private static ConsumerConfiguration Manifest(string module)
    {
        var repository = module == "decisions" ? "https://github.com/aetheric-forge/adr-campus" : "https://github.com/aetheric-forge/" + module;
        var settings = WebModuleConsumerProfiles.RequiredSettings(module).ToImmutableDictionary(key => key, _ => "scoped-value")
            .SetItem(WebModuleConsumerProfiles.Section(module) + ":Enabled", "true");
        if (module == "decisions") settings = settings.SetItem("Decisions:Owner:DeploymentId", new string('d', 64)).SetItem("Decisions:Owner:Environment", "production");
        return new(1, module, InstitutionDeploymentAttempt.Identity(repository, module, "production"), module, "1.0.0", "production", new string('a', 64), settings)
        { InstitutionName = "Existing institution" };
    }
    private void Write(ConsumerConfiguration manifest)
    {
        var folder = Path.Combine(_directory, manifest.Module); Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "configuration.json"), JsonSerializer.SerializeToUtf8Bytes(manifest));
    }
    [Fact]
    public void Empty_consumer_slots_do_not_enable_modules()
    {
        var configuration = Configuration();
        Assert.Equal(3, ProvisionedWebModuleConfiguration.Apply(configuration).Count);
        Assert.Null(configuration["Library:Enabled"]);
        Assert.Null(configuration["Archive:Enabled"]);
    }
    [Theory]
    [InlineData("library")]
    [InlineData("archive")]
    public void Complete_artifacts_replace_stale_module_settings_and_preserve_sign_in(string module)
    {
        Write(Manifest(module)); var configuration = Configuration();
        configuration[WebModuleConsumerProfiles.Section(module) + ":Enabled"] = "false";
        ProvisionedWebModuleConfiguration.Apply(configuration);
        Assert.Equal("true", configuration[WebModuleConsumerProfiles.Section(module) + ":Enabled"]);
        Assert.Equal("existing-sign-in-secret", configuration["Registry:Keycloak:ClientSecret"]);
    }
    [Fact]
    public void Wrong_deployment_or_incomplete_artifacts_are_rejected()
    {
        Write(Manifest("library") with { DeploymentId = new string('b', 64) });
        Assert.Throws<InvalidDataException>(() => ProvisionedWebModuleConfiguration.Apply(Configuration()));
        Write(Manifest("library") with { Settings = Manifest("library").Settings.Remove("Library:MongoDb:Password") });
        Assert.Throws<InvalidDataException>(() => ProvisionedWebModuleConfiguration.Apply(Configuration()));
        Write(Manifest("library") with { Settings = Manifest("library").Settings.Add("RootCredentials:Password", "forbidden") });
        Assert.Throws<InvalidDataException>(() => ProvisionedWebModuleConfiguration.Apply(Configuration()));
    }
    [Fact]
    public void Decisions_must_match_the_existing_campus_owner_and_keeps_directory_credentials_separate()
    {
        Write(Manifest("decisions")); var configuration = Configuration();
        Assert.Throws<InvalidDataException>(() => ProvisionedWebModuleConfiguration.Apply(configuration));
        configuration["PublicSite:Enabled"] = "false";
        configuration["Decisions:ExpectedOwnerDeploymentId"] = new string('c', 64);
        Assert.Throws<InvalidDataException>(() => ProvisionedWebModuleConfiguration.Apply(configuration));
        configuration["Decisions:ExpectedOwnerDeploymentId"] = new string('d', 64);
        ProvisionedWebModuleConfiguration.Apply(configuration);
        Assert.Equal("scoped-value", configuration["Decisions:Directory:Keycloak:ClientSecret"]);
        Assert.Equal("existing-sign-in-secret", configuration["Registry:Keycloak:ClientSecret"]);
    }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
