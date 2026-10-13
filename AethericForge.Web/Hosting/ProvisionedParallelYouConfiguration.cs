using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aetheric.Provisioning.Engine;
using Microsoft.Extensions.Configuration;

namespace AethericForge.Web.Hosting;

/// <summary>Loads only a deployment-managed consumer artifact; never reads provisioning's catalog or root stores.</summary>
public static class ProvisionedParallelYouConfiguration
{
    private static readonly HashSet<string> RequiredSettings = new(StringComparer.Ordinal)
    {
        "ParallelYou:Enabled", "ParallelYou:MongoDb:Host", "ParallelYou:MongoDb:Port",
        "ParallelYou:MongoDb:DatabaseName", "ParallelYou:MongoDb:Username", "ParallelYou:MongoDb:Password",
        "ParallelYou:MongoDb:AuthenticationDatabase", "Workbench:Redis:Host", "Workbench:Redis:Port",
        "Workbench:Redis:Ssl", "Workbench:Redis:Database", "Workbench:Redis:User", "Workbench:Redis:Password",
        "Registry:Keycloak:Authority", "Registry:Keycloak:Realm", "Registry:Keycloak:ClientId", "Registry:Keycloak:ClientSecret"
    };

    public static ConsumerConfiguration? Apply(ConfigurationManager configuration)
    {
        var path = configuration["ParallelYou:ProvisionedConfigurationPath"];
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (!configuration.GetValue("PublicSite:Enabled", true))
            throw new InvalidOperationException("The Parallel You consumer artifact requires standalone module hosting.");
        var expectedId = configuration["ParallelYou:DeploymentId"];
        var environment = configuration["ParallelYou:Environment"];
        var expectedPlan = configuration["ParallelYou:PlanId"];
        if (string.IsNullOrWhiteSpace(expectedId) || string.IsNullOrWhiteSpace(environment))
            throw new InvalidOperationException("Provisioned Parallel You requires its expected DeploymentId and Environment.");
        ConsumerConfiguration? manifest;
        try
        {
            if (new FileInfo(path).Length > 65536) throw new InvalidDataException("Consumer configuration exceeds the supported size.");
            manifest = JsonSerializer.Deserialize<ConsumerConfiguration>(File.ReadAllBytes(path), new JsonSerializerOptions
            { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow });
        }
        catch (JsonException) { throw new InvalidDataException("Invalid Parallel You consumer configuration JSON."); }
        if (manifest is null || manifest.Version != 1 || manifest.Module != "parallel-you" || manifest.InstitutionId != "parallel-you"
            || manifest.DeploymentId != expectedId || manifest.Environment != environment
            || expectedPlan is not null && manifest.PlanId != expectedPlan
            || !Regex.IsMatch(manifest.DeploymentId, "\\A[0-9a-f]{64}\\z") || !Regex.IsMatch(manifest.PlanId ?? "", "\\A[0-9a-f]{64}\\z")
            || !Version.TryParse(manifest.InstitutionVersion, out _) || string.IsNullOrWhiteSpace(manifest.InstitutionName)
            || manifest.Settings is null || !RequiredSettings.SetEquals(manifest.Settings.Keys)
            || manifest.Settings.Any(x => string.IsNullOrWhiteSpace(x.Value))
            || !bool.TryParse(manifest.Settings["ParallelYou:Enabled"], out var enabled) || !enabled)
            throw new InvalidDataException("The Parallel You consumer configuration does not match this deployment.");

        // An artifact is a complete revision. Do not merge old secret values with a new database/client.
        var values = manifest.Settings.ToDictionary(x => x.Key, x => (string?)x.Value);
        values["ParallelYou:Institution:Name"] = manifest.InstitutionName;
        values["ParallelYou:Institution:Version"] = manifest.InstitutionVersion;
        values["ParallelYou:Institution:Description"] = manifest.Description;
        values["ParallelYou:Institution:PlanId"] = manifest.PlanId;
        configuration.AddInMemoryCollection(values);
        return manifest;
    }
}
