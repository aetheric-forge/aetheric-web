using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aetheric.Provisioning.Engine;

namespace AethericForge.Web.Hosting;

/// <summary>Loads complete scoped artifacts from deployment-managed, individually mounted Secrets.</summary>
public static class ProvisionedWebModuleConfiguration
{
    public static IReadOnlyDictionary<string, string?> Apply(ConfigurationManager configuration)
    {
        var directory = configuration["Provisioning:Consumers:Directory"];
        var revisions = new Dictionary<string, string?>();
        if (string.IsNullOrWhiteSpace(directory)) return revisions;
        var environment = configuration["Provisioning:Consumers:Environment"]
            ?? throw new InvalidOperationException("A consumer environment is required.");
        foreach (var module in new[] { "library", "archive", "decisions" })
        {
            var path = Path.Combine(directory, module, "configuration.json");
            revisions[path] = null;
            if (!File.Exists(path)) continue; // An empty Secret means the module has not been provisioned yet.
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length > 65536) throw new InvalidDataException("Consumer configuration exceeds its size limit.");
            ConsumerConfiguration manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<ConsumerConfiguration>(bytes, new JsonSerializerOptions
                    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })
                    ?? throw new InvalidDataException("Empty consumer configuration.");
            }
            catch (JsonException) { throw new InvalidDataException("Invalid consumer configuration JSON."); }
            var repository = module == "decisions" ? "https://github.com/aetheric-forge/adr-campus"
                : "https://github.com/aetheric-forge/" + module;
            var section = WebModuleConsumerProfiles.Section(module);
            if (manifest.Version != 1 || manifest.Module != module || manifest.InstitutionId != module
                || manifest.Environment != environment
                || manifest.DeploymentId != InstitutionDeploymentAttempt.Identity(repository, module, environment)
                || !Regex.IsMatch(manifest.PlanId ?? "", "\\A[0-9a-f]{64}\\z")
                || !Version.TryParse(manifest.InstitutionVersion, out _) || string.IsNullOrWhiteSpace(manifest.InstitutionName)
                || manifest.Settings is null || !WebModuleConsumerProfiles.RequiredSettings(module).SetEquals(manifest.Settings.Keys)
                || manifest.Settings.Any(pair => string.IsNullOrWhiteSpace(pair.Value))
                || !bool.TryParse(manifest.Settings[section + ":Enabled"], out var enabled) || !enabled)
                throw new InvalidDataException("Consumer configuration does not match its deployment slot.");
            if (module == "decisions" && (configuration.GetValue("PublicSite:Enabled", true)
                || configuration["Decisions:ExpectedOwnerDeploymentId"] != manifest.Settings["Decisions:Owner:DeploymentId"]
                || environment != manifest.Settings["Decisions:Owner:Environment"]))
                throw new InvalidDataException("Decisions requires its configured, provisioned campus owner in the same environment.");
            configuration.AddInMemoryCollection(manifest.Settings.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value)));
            revisions[path] = Convert.ToHexStringLower(SHA256.HashData(bytes));
        }
        return revisions;
    }
}

/// <summary>A Secret revision is applied on a fresh host, never merged into live provider credentials.</summary>
public sealed class ConsumerConfigurationReloadService(IHostApplicationLifetime lifetime,
    IReadOnlyDictionary<string, string?> revisions, ILogger<ConsumerConfigurationReloadService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var pair in revisions)
            {
                string? revision = null;
                try
                {
                    if (File.Exists(pair.Key)) revision = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(pair.Key, stoppingToken)));
                }
                catch (IOException) { continue; } // Projected Secret revisions switch atomically; retry transient reads.
                if (revision == pair.Value) continue;
                logger.LogInformation("Provisioned consumer configuration changed; restarting the host to apply the complete revision.");
                lifetime.StopApplication();
                return;
            }
        }
    }
}
