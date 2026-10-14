using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using AethericForge.Runtime.Providers.Staging.InMemory;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheric.Provisioning.Definitions;
using Aetheric.Provisioning.Engine;
using AethericForge.Runtime.Abstractions.Interfaces.Institutions.Plugins;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Staging.Providers;
using AethericForge.Runtime.Models.Identity.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Identity.Authentication;
using AethericForge.Runtime.Models.Knowledge.Authorities;
using AethericForge.Runtime.Providers.Knowledge.InMemory;
using AethericForge.Web.Hosting.Modules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AethericForge.Web.Tests;

public class GitInstitutionModuleTests
{
    private sealed class AnonymousAuthentication : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }
    private static string ManifestPath => Path.Combine(AppContext.BaseDirectory, "modules", "parallel-you", "module.json");

    private sealed class FixtureGitSource : IDefinitionSource
    {
        public DefinitionSourceRequest? Request { get; private set; }
        public bool WrongCommit { get; init; }
        public bool WrongHash { get; init; }
        public Task<SourceLoadResult> LoadAsync(DefinitionSourceRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            SourceDocument Read(string path)
            {
                var text = File.ReadAllText(Path.Combine(Path.GetDirectoryName(ManifestPath)!, path));
                var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
                return new(text, new SourceProvenance(request.Repository, WrongCommit ? new string('0', 40) : request.Revision,
                    path, WrongHash ? new string('0', 64) : digest));
            }
            return Task.FromResult(new SourceLoadResult(new SourceBundle(Read(request.DefinitionPath), Read(request.BindingsPath)), []));
        }
    }

    [Fact]
    public async Task Git_definition_loads_the_external_plugin_and_its_authenticated_Razor_pages()
    {
        var source = new FixtureGitSource();
        var module = await GitInstitutionWebModule.LoadAsync(ManifestPath, source);
        Assert.Equal(module.Source.Repository, source.Request!.Repository);
        Assert.Equal(module.Source.Revision, source.Request.Revision);
        Assert.Equal("parallel-you", module.Definition.Requirements.Id);
        Assert.NotSame(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(module.WebAssembly));
        var working = new[] { "capture", "captures", "reflect", "track", "intentions", "plans", "recommendations" };
        var pages = module.WebAssembly.GetTypes().Where(type => typeof(IComponent).IsAssignableFrom(type))
            .SelectMany(type => type.GetCustomAttributes<RouteAttribute>().Select(route => (type, route.Template))).ToArray();
        foreach (var page in working)
        {
            var match = Assert.Single(pages.Where(x => x.Template == "/parallel-you/" + page));
            Assert.NotEmpty(match.type.GetCustomAttributes<AuthorizeAttribute>(true));
        }
        Assert.Single(pages.Where(x => x.Template == "/parallel-you"));
        Assert.Contains(module.Stylesheets, path => path.StartsWith("_content/ParallelYou.Web/"));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(ManifestPath)!, "wwwroot", "app.css")));

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ParallelYou:MongoDb:Host"] = "unused.example", ["ParallelYou:MongoDb:DatabaseName"] = "personal",
            ["ParallelYou:MongoDb:Username"] = "consumer", ["ParallelYou:MongoDb:Password"] = "test-only",
            ["Workbench:Redis:Host"] = "unused.example", ["Workbench:Redis:Password"] = "test-only"
        }).Build();
        var services = new ServiceCollection().AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<AuthenticationStateProvider, AnonymousAuthentication>();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        module.ConfigureServices(services, configuration, _ => null);
        Assert.Contains(services, entry => entry.ServiceType.FullName == "ParallelYou.Abstractions.Capture.ICaptureService");
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        Assert.Same(module, provider.GetRequiredService<GitInstitutionWebModule>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Changed_or_unlisted_plugin_files_fail_before_Git_access(bool addUnlistedFile)
    {
        var directory = Path.Combine(Path.GetTempPath(), "institution-bundle-" + Guid.NewGuid().ToString("N"));
        var original = Path.GetDirectoryName(ManifestPath)!;
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var path in Directory.EnumerateFiles(original, "*", SearchOption.AllDirectories))
            {
                var destination = Path.Combine(directory, Path.GetRelativePath(original, path));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(path, destination);
            }
            if (addUnlistedFile) File.WriteAllText(Path.Combine(directory, "unreviewed.dll"), "unreviewed");
            else File.AppendAllText(Path.Combine(directory, "ParallelYou.Web.dll"), "changed");
            var source = new FixtureGitSource();
            await Assert.ThrowsAsync<InvalidDataException>(() => GitInstitutionWebModule.LoadAsync(Path.Combine(directory, "module.json"), source));
            Assert.Null(source.Request);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Git_commit_or_document_hash_mismatch_is_rejected(bool wrongCommit, bool wrongHash) =>
        await Assert.ThrowsAsync<InvalidDataException>(() => GitInstitutionWebModule.LoadAsync(ManifestPath,
            new FixtureGitSource { WrongCommit = wrongCommit, WrongHash = wrongHash }));

    [Fact]
    public async Task Private_module_can_write_through_shared_runtime_capabilities_with_identity_isolation()
    {
        var module = await GitInstitutionWebModule.LoadAsync(ManifestPath, new FixtureGitSource());
        var context = AssemblyLoadContext.GetLoadContext(module.WebAssembly)!;
        var factory = (IInstitutionFactory)Activator.CreateInstance(module.WebAssembly.GetType("ParallelYou.Web.Plugin.ParallelYouWebFactory")!)!;
        var servicesAssembly = context.LoadFromAssemblyName(new AssemblyName("ParallelYou.Services"));
        var modelsAssembly = context.LoadFromAssemblyName(new AssemblyName("ParallelYou.Models"));
        var hosting = module.WebAssembly.GetType("ParallelYou.Web.Hosting.ParallelYouHosting")!;
        var librarian = hosting.GetMethod("CreateLibrarian")!.Invoke(null, [new InMemoryKnowledgeProvider("parallel-you")]);
        var artificer = hosting.GetMethod("CreateArtificer")!.Invoke(null, [new IStagingProvider[] { new InMemoryStagingProvider("PlanCurrent") }]);
        using var provider = new ServiceCollection().BuildServiceProvider();
        var institutionContext = Activator.CreateInstance(servicesAssembly.GetType("ParallelYou.Services.ParallelYouContext")!,
            [factory.Template, provider, null]);
        var institution = servicesAssembly.GetType("ParallelYou.Services.ParallelYouModule")!.GetMethod("Create")!
            .Invoke(null, [institutionContext, librarian, artificer])!;
        var capture = institution.GetType().GetProperty("Capture")!.GetValue(institution)!;
        var evidence = Activator.CreateInstance(modelsAssembly.GetType("ParallelYou.Models.Capture.CaptureEvidence")!, ["A note", "From the web shell"]);
        var authority = new KnowledgeAuthority(new IdentitySubject("owner", IdentityScheme.OpenIdConnect), "ParallelYou.Capture");
        var task = (Task)capture.GetType().GetMethod("CaptureAsync")!.Invoke(capture, [evidence, authority, CancellationToken.None])!;
        await task;
        var artifact = (IKnowledgeArtifact)task.GetType().GetProperty("Result")!.GetValue(task)!;
        Assert.Equal("parallel-you", artifact.Reference.Scheme);
        var read = (Task)capture.GetType().GetMethod("GetCapturesAsync")!.Invoke(capture,
            [new KnowledgeAuthority(new IdentitySubject("other", IdentityScheme.OpenIdConnect), authority.Context), CancellationToken.None])!;
        await read;
        Assert.Empty((IReadOnlyCollection<IKnowledgeArtifact>)read.GetType().GetProperty("Result")!.GetValue(read)!);
    }
}
