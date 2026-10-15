using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using Aetheric.Provisioning.Definitions;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Archive.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Providers;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Workbench.Services;
using AethericForge.Runtime.Institutions.Abstractions.Builders;
using AethericForge.Runtime.Institutions.Archive;
using AethericForge.Runtime.Institutions.Campus;
using AethericForge.Runtime.Institutions.Decisions;
using AethericForge.Runtime.Institutions.Library;
using AethericForge.Runtime.Institutions.Workbench;
using AethericForge.Runtime.Models.Archive.Primitives;
using AethericForge.Runtime.Models.Authorities;
using AethericForge.Runtime.Providers.Archive.InMemory;
using AethericForge.Runtime.Providers.Knowledge.InMemory;
using AethericForge.Runtime.Providers.Staging.InMemory;
using AethericForge.Runtime.Services.Knowledge;
using AethericForge.Runtime.Services.Staging;
using AethericForge.Runtime.Services.Workbench;
using AethericForge.Web.Hosting.Modules;
using AethericForge.Web.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AethericForge.Web.Tests;

public sealed class MultiInstitutionModuleTests
{
    private sealed class AnonymousAuthentication : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(
            new AuthenticationState(new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity())));
    }
    private sealed class BundledSource(string manifest) : IDefinitionSource
    {
        public Task<SourceLoadResult> LoadAsync(DefinitionSourceRequest request, CancellationToken cancellationToken = default)
        {
            SourceDocument Read(string path)
            {
                var text = File.ReadAllText(Path.Combine(Path.GetDirectoryName(manifest)!, path));
                return new(text, new(request.Repository, request.Revision, path,
                    Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))));
            }
            return Task.FromResult(new SourceLoadResult(new(Read(request.DefinitionPath), Read(request.BindingsPath)), []));
        }
    }
    private static Task<GitInstitutionWebModule> Load(string id)
    {
        var manifest = Path.Combine(AppContext.BaseDirectory, "modules", id, "module.json");
        return GitInstitutionWebModule.LoadAsync(manifest, new BundledSource(manifest));
    }
    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
    {
        ["Library:MongoDb:Host"]="unused.example", ["Library:MongoDb:DatabaseName"]="existing-library",
        ["Library:MongoDb:Username"]="library", ["Library:MongoDb:Password"]="library-test",
        ["Decisions:MongoDb:Host"]="unused.example", ["Decisions:MongoDb:DatabaseName"]="existing-decisions",
        ["Decisions:MongoDb:Username"]="decisions", ["Decisions:MongoDb:Password"]="decisions-test",
        ["Archive:S3:ServiceUrl"]="https://unused.example", ["Archive:S3:AccessKey"]="archive", ["Archive:S3:SecretKey"]="archive-test",
        ["Decisions:S3:ServiceUrl"]="https://unused.example", ["Decisions:S3:AccessKey"]="decisions", ["Decisions:S3:SecretKey"]="decisions-test",
        ["Registry:Keycloak:Authority"]="https://sso.aethericforge.ca", ["Registry:Keycloak:Realm"]="int.aethericforge.ca",
        ["Registry:Keycloak:ClientId"]="directory", ["Registry:Keycloak:ClientSecret"]="directory-test",
        ["Organization:Id"]="existing-organization", ["Organization:DisplayName"]="Existing organization",
        ["Organization:MemberGroupId"]="existing-members", ["Organization:MaintainerGroupId"]="existing-maintainers",
        ["Decisions:BackgroundServicesEnabled"]="false"
    }).Build();

    [Theory]
    [InlineData("library", "/library")]
    [InlineData("library", "/library/articles/new")]
    [InlineData("decisions", "/decisions")]
    [InlineData("decisions", "/decisions/drafts/new")]
    [InlineData("decisions", "/decisions/administration")]
    public async Task Module_pages_keep_their_paths_layouts_and_authentication(string id, string path)
    {
        var module = await Load(id);
        var page = Assert.Single(module.WebAssembly.GetTypes().Where(type => typeof(IComponent).IsAssignableFrom(type))
            .Where(type => type.GetCustomAttributes<RouteAttribute>().Any(route => route.Template == path)));
        Assert.NotEmpty(page.GetCustomAttributes<AuthorizeAttribute>(true));
        Assert.NotNull(page.GetCustomAttribute<LayoutAttribute>());
        Assert.DoesNotContain(module.WebAssembly.GetTypes(), type => type.GetCustomAttributes<RouteAttribute>().Any(route => route.Template == "/"));
    }

    [Fact]
    public async Task The_modules_share_owner_contracts_and_preserve_existing_archive_keys()
    {
        var library = await Load("library");
        var archive = await Load("archive");
        var decisions = await Load("decisions");
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var configuration = Config();
        var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuration)
            .AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<AuthenticationStateProvider, AnonymousAuthentication>();
        services.AddRouting();
        var untouched = new KnowledgeService([new InMemoryKnowledgeProvider("unrelated")], new Team<ICuratorClerk>([]));
        services.AddSingleton<IKnowledgeService>(untouched);
        ICampus? owner = null;
        IAdministrationFaculty? administration = null;
        foreach (var module in new[] { library, archive, decisions })
            module.ConfigureServices(services, configuration, _ => module == decisions ? administration : owner);
        services.RemoveAllKeyed<IKnowledgeProvider>("Library");
        services.AddKeyedSingleton<IKnowledgeProvider>("Library", new InMemoryKnowledgeProvider("ForgeCampus"));
        services.AddKeyedSingleton<IKnowledgeProvider>("Library", new InMemoryKnowledgeProvider("adr-campus"));
        var storage = new InMemoryArchiveProvider("MinIO");
        await storage.PutAsync("existing/catalog-v1", new MemoryStream(Encoding.UTF8.GetBytes("existing content")));
        services.RemoveAllKeyed<IArchiveProvider>("Archive");
        services.AddKeyedSingleton<IArchiveProvider>("Archive", storage);
        services.AddKeyedSingleton<IArchiveProvider>("Archive", new InMemoryArchiveProvider("adr-campus"));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var template = InstitutionTemplateBuilder.Create().WithDescriptor("Existing owner", new Version(1,0,0), "Test owner").Build();
        owner = new Campus(new CampusContext(template, provider));
        library.Register(owner, provider);
        archive.Register(owner, provider);
        var staging = new StagingService([new InMemoryStagingProvider("adr-campus-workbench")]);
        owner.Register<IWorkbench>(new Workbench(new WorkbenchContext(template, provider, owner),
            new Artificer(staging, new Team<IWorkbenchWorker>([])), new WorkbenchService()));
        administration = ((Campus)owner).RegisterFaculty<IAdministrationFaculty>(
            (InstitutionTemplate)template, provider, "Administration", "Administrator",
            static (context, dean) => new AdministrationFaculty(context, dean));
        decisions.Register(administration, provider);
        Assert.Same(owner, owner.Library.Context.Parent);
        Assert.Same(owner, owner.Archive.Context.Parent);
        Assert.Same(administration, administration.ResolveOrganization<IDecisions>("decisions").Context.Owner);
        Assert.Same(untouched, provider.GetRequiredService<IKnowledgeService>());
        Assert.Same(provider.GetRequiredKeyedService<IKnowledgeService>("Library"), ((ILibraryContext)owner.Library.Context).Knowledge);
        using var content = await owner.Archive.RetrieveAsync(new ArchiveReference("MinIO", "existing/catalog-v1"));
        using var reader = new StreamReader(content);
        Assert.Equal("existing content", await reader.ReadToEndAsync());
        Assert.Same(library, provider.GetRequiredKeyedService<GitInstitutionWebModule>("library"));
        Assert.Same(archive, provider.GetRequiredKeyedService<GitInstitutionWebModule>("archive"));
        Assert.Equal(3, provider.GetServices<GitInstitutionWebModule>().Count());
        using var scope = provider.CreateScope();
        var drafts = services.Single(descriptor => !descriptor.IsKeyedService && descriptor.ServiceType.FullName == "AdrCampus.Core.Drafts.IDraftRepository");
        var draftRepository = scope.ServiceProvider.GetRequiredService(drafts.ServiceType);
        Assert.NotNull(administration.ResolveOrganization<IDecisions>("decisions").Recorder);
        var core = AssemblyLoadContext.GetLoadContext(decisions.WebAssembly)!
            .LoadFromAssemblyName(new AssemblyName("AdrCampus.Core"));
        object Value(string type, params object?[] args) => Activator.CreateInstance(core.GetType("AdrCampus.Core.Domain." + type)!, args)!;
        var organization = Value("OrganizationId", "existing-organization");
        var author = Value("MemberId", "existing-author");
        var id = Value("AdrId", Guid.NewGuid());
        var text = Value("DraftContent", "Loaded office draft", "Existing context", "Choose the shared facilities", "Retain the record");
        var draft = core.GetType("AdrCampus.Core.Domain.AdrDraft")!.GetMethod("Create")!
            .Invoke(null, [id, organization, author, text, DateTimeOffset.UtcNow, null])!;
        object Operation() => core.GetType("AdrCampus.Core.Domain.OperationId")!.GetMethod("New")!.Invoke(null, null)!;
        var created = await InvokeAsync(draftRepository, "CreateAsync", draft, Operation(), CancellationToken.None);
        Assert.Equal("Created", created!.GetType().GetProperty("Status")!.GetValue(created)!.ToString());
        var read = await InvokeAsync(draftRepository, "GetByAuthorAsync", organization, author, id, CancellationToken.None);
        Assert.NotNull(read);
        var proposals = services.Single(descriptor => !descriptor.IsKeyedService && descriptor.ServiceType.FullName == "AdrCampus.Core.Proposals.IProposalRepository");
        var proposed = await InvokeAsync(scope.ServiceProvider.GetRequiredService(proposals.ServiceType), "ProposeAsync",
            organization, author, id, 1L, Operation(), DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.Equal("Proposed", proposed!.GetType().GetProperty("Status")!.GetValue(proposed)!.ToString());
        Assert.Null(await InvokeAsync(draftRepository, "GetByAuthorAsync", organization, author, id, CancellationToken.None));
    }

    private static async Task<object?> InvokeAsync(object target, string method, params object?[] args)
    {
        var task = (Task)target.GetType().GetMethod(method)!.Invoke(target, args)!;
        await task;
        return task.GetType().GetProperty("Result")?.GetValue(task);
    }
}
