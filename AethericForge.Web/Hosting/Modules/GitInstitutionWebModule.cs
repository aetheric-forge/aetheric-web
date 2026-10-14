using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aetheric.Provisioning.Definitions;
using AethericForge.Runtime.Abstractions.Interfaces.Institutions;
using AethericForge.Runtime.Abstractions.Interfaces.Institutions.Plugins;
using AethericForge.Runtime.Institutions.Plugins;
using Microsoft.Extensions.FileProviders;

namespace AethericForge.Web.Hosting.Modules;

/// <summary>A pinned Git definition paired with its independently built institution web plugin.</summary>
public sealed class GitInstitutionWebModule
{
    public sealed record Bundle(string Id, string Repository, string Revision, string Assembly,
        string DefinitionPath, string BindingsPath, string DefinitionHash, string BindingsHash,
        string[] Stylesheets, Dictionary<string, string> Files, string? ConfigurationSection = null);

    private readonly object _factory;
    private readonly MethodInfo _configure;
    private readonly string _directory;
    // Keep the collectible context alive while services and Razor components use its assemblies.
    private readonly AssemblyLoadContext _loadContext;
    public Bundle Source { get; }
    public Assembly WebAssembly => _factory.GetType().Assembly;
    public LoadedInstitution Definition { get; }
    public IEnumerable<string> Stylesheets => Source.Stylesheets;

    private GitInstitutionWebModule(Bundle source, LoadedInstitution definition,
        object factory, MethodInfo configure, string directory) =>
        (Source, Definition, _factory, _configure, _directory, _loadContext) =
            (source, definition, factory, configure, directory, AssemblyLoadContext.GetLoadContext(factory.GetType().Assembly)!);

    public static async Task<GitInstitutionWebModule> LoadAsync(string manifestPath, IDefinitionSource source,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        if (new FileInfo(manifestPath).Length > 128 * 1024)
            throw new InvalidDataException("Institution module manifest is too large.");
        var bundle = JsonSerializer.Deserialize<Bundle>(await File.ReadAllBytesAsync(manifestPath, cancellationToken))
            ?? throw new InvalidDataException("Missing institution module manifest.");
        if (string.IsNullOrWhiteSpace(bundle.Id) || !Regex.IsMatch(bundle.Id, "\\A[a-z0-9-]+\\z")
            || !Regex.IsMatch(bundle.Revision ?? "", "\\A[0-9a-f]{40}\\z")
            || string.IsNullOrWhiteSpace(bundle.Assembly) || Path.GetFileName(bundle.Assembly) != bundle.Assembly
            || bundle.Files is null || bundle.Files.Count == 0 || !bundle.Files.ContainsKey(bundle.Assembly)
            || bundle.Stylesheets is null)
            throw new InvalidDataException("Invalid institution module identity or inventory.");
        var inventory = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFullPath(path) != Path.GetFullPath(manifestPath))
            .Select(path => Path.GetRelativePath(directory, path).Replace(Path.DirectorySeparatorChar, '/'));
        if (!bundle.Files.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(inventory))
            throw new InvalidDataException("Institution module contains missing or unreviewed files.");
        foreach (var (name, digest) in bundle.Files)
        {
            var path = ContainedPath(directory, name);
            await using var stream = File.OpenRead(path);
            if (Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken)) != digest)
                throw new InvalidDataException("Institution module file differs from its reviewed build: " + name);
        }
        foreach (var stylesheet in bundle.Stylesheets)
            if (!bundle.Files.ContainsKey("wwwroot/" + stylesheet))
                throw new InvalidDataException("Module stylesheet is not in its build inventory.");
        var result = await source.LoadAsync(new DefinitionSourceRequest(bundle.Repository, bundle.Revision!,
            bundle.DefinitionPath, bundle.BindingsPath), cancellationToken);
        if (result.Bundle is null || result.Issues.Length != 0)
            throw new InvalidDataException("Institution Git loading failed: " + string.Join(", ", result.Issues.Select(x => x.Code)));
        var git = result.Bundle;
        if (git.Definition.Provenance.Commit != bundle.Revision || git.Bindings.Provenance.Commit != bundle.Revision
            || git.Definition.Provenance.Repository != bundle.Repository || git.Bindings.Provenance.Repository != bundle.Repository
            || git.Definition.Provenance.ContentHash != bundle.DefinitionHash || git.Bindings.Provenance.ContentHash != bundle.BindingsHash)
            throw new InvalidDataException("Git definitions do not match the compiled institution revision.");
        var parsed = new InstitutionYamlReader().Read(git);
        if (parsed.Institution is null || parsed.Issues.Length != 0 || parsed.Institution.Requirements.Id != bundle.Id)
            throw new InvalidDataException("Invalid institution definition for this module: " +
                string.Join(", ", parsed.Issues.Select(issue => issue.Code)));

        var plugins = new InstitutionPluginLoader().LoadDirectory(directory);
        var plugin = plugins.SingleOrDefault(x => x.AssemblyPath == Path.Combine(directory, bundle.Assembly));
        if (plugin is null || plugin.Factories.Count + plugin.OrganizationFactories.Count != 1)
            throw new InvalidDataException("Required module must expose exactly one institution or organization factory.");
        object factory = plugin.Factories.Count == 1 ? plugin.Factories.Single() : plugin.OrganizationFactories.Single();
        var manifest = factory is IInstitutionFactory institutionFactory
            ? institutionFactory.Manifest : ((IOrganizationFactory)factory).Manifest;
        if (factory is IOrganizationFactory organizationFactory && organizationFactory.OrganizationId != bundle.Id)
            throw new InvalidDataException("Organization factory identity differs from its Git definition.");
        if (manifest.Version.ToString() != parsed.Institution.Requirements.Version)
            throw new InvalidDataException("Module version differs from its Git definition.");
        var configure = factory.GetType().GetMethod("ConfigureWebServices", [typeof(IServiceCollection),
            typeof(IConfiguration), typeof(Func<IServiceProvider, IInstitution>)])
            ?? throw new InvalidDataException("Institution plugin does not expose a compatible web host adapter.");
        return new(bundle, parsed.Institution, factory, configure, directory);
    }

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration,
        Func<IServiceProvider, IInstitution?> parent)
    {
        // The consumer artifact remains authoritative for deployment identity, resource credentials
        // and OIDC. The Git definition supplies the implementation's institutional contract.
        var section = Source.ConfigurationSection ?? "ParallelYou";
        var version = configuration[$"{section}:Institution:Version"];
        if (version is not null && version != Definition.Requirements.Version)
            throw new InvalidDataException("Provisioned institution version differs from the loaded Git module.");
        _configure.Invoke(_factory, [services, configuration, parent]);
        services.AddSingleton(this);
        services.AddKeyedSingleton(Source.Id, this);
    }

    public void Register(IInstitution parent, IServiceProvider services)
    {
        if (_factory is IOrganizationFactory organization)
            parent.RegisterOrganization(organization.OrganizationId, organization.Create(parent, services));
        else
        {
            var factory = (IInstitutionFactory)_factory;
            var instance = factory.Create(parent, services);
            typeof(IInstitution).GetMethod(nameof(IInstitution.Register))!.MakeGenericMethod(factory.ContractType)
                .Invoke(parent, [instance]);
        }
    }

    public IInstitution Resolve(IInstitution parent)
    {
        if (_factory is not IInstitutionFactory factory)
            throw new InvalidOperationException("An organization is resolved through its owning institution.");
        return (IInstitution)typeof(IInstitution).GetMethod(nameof(IInstitution.Resolve))!
            .MakeGenericMethod(factory.ContractType).Invoke(parent, null)!;
    }

    public void MapAssets(WebApplication app)
    {
        var assets = Path.Combine(_directory, "wwwroot");
        if (Directory.Exists(assets))
            app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(assets) });
    }

    private static string ContainedPath(string directory, string name)
    {
        var full = Path.GetFullPath(Path.Combine(directory, name));
        if (!full.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("Module file escapes its bundle directory.");
        return full;
    }
}
