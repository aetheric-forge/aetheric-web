using AethericForge.Runtime.Institutions.Faculty;
using AethericForge.Runtime.Institutions.Campus;
using Aetheric.Provisioning.Definitions;
using AethericForge.Web.Hosting.Modules;
using AethericForge.Web.Marketing;
using AethericContracts.Membership;
using AethericForge.Web.Components;
using AethericForge.Web.Hosting;
using AethericForge.Web.Membership;
using AethericForge.Web.Services;
using Forge.Primitives.MongoDb;
using Forge.Primitives.Redis;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using StackExchange.Redis;

// MongoDB.Driver 3.x removed its old implicit Guid-serialization default - without this, any Guid-
// keyed write throws "GuidSerializer cannot serialize a Guid when GuidRepresentation is Unspecified."
// Must run before any Mongo store is constructed.
MongoBsonSetup.EnsureGuidRepresentationRegistered();

var builder = WebApplication.CreateBuilder(args);
ProvisionedParallelYouConfiguration.Apply(builder.Configuration);
var consumerRevisions = ProvisionedWebModuleConfiguration.Apply(builder.Configuration);
if (consumerRevisions.Count > 0)
    builder.Services.AddHostedService(sp => new ConsumerConfigurationReloadService(
        sp.GetRequiredService<IHostApplicationLifetime>(), consumerRevisions,
        sp.GetRequiredService<ILogger<ConsumerConfigurationReloadService>>()));

// Defaults to public content only (Home/Projects/About/Articles/Videos - no /campus/* member or
// admin routes) with no external infrastructure required at all. Flip to false once Keycloak,
// Redis, and the Membership Mongo connection are configured and ready to come back online - the
// full campus/membership/authentication wiring below is unchanged, just conditional now.
var publicOnly = builder.Configuration.GetValue("PublicSite:Enabled", true);
var publicSignIn = publicOnly && builder.Configuration.GetValue<bool>("PublicSite:SignInEnabled");
var modules = new List<GitInstitutionWebModule>();
var specs = new[] { (Id: "parallel-you", Section: "ParallelYou"), (Id: "library", Section: "Library"),
    (Id: "archive", Section: "Archive"), (Id: "decisions", Section: "Decisions") };
foreach (var spec in specs)
{
    if (publicOnly && !builder.Configuration.GetValue<bool>(spec.Section + ":Enabled")) continue;
    if (publicOnly && spec.Id == "decisions")
        throw new InvalidOperationException("Decisions must be mounted under an existing owning institution. Enable full campus hosting to supply its Library and Workbench.");
    using var gitHttp = PublicGitHubSource.CreateHttpClient();
    var bundle = builder.Configuration[spec.Section + ":Module:BundlePath"]
        ?? Path.Combine(builder.Environment.ContentRootPath, "modules", spec.Id, "module.json");
    var module = await GitInstitutionWebModule.LoadAsync(bundle, new PublicGitHubSource(gitHttp));
    if (module.Source.Id != spec.Id || module.Source.ConfigurationSection != spec.Section)
        throw new InvalidDataException("The module bundle does not match its configured shell slot.");
    module.ConfigureServices(builder.Services, builder.Configuration, sp => publicOnly ? null
        : spec.Id == "decisions" ? sp.GetRequiredService<ICampus>().Resolve<IArchitectureFaculty>()
        : sp.GetRequiredService<ICampus>());
    modules.Add(module);
}
var enabledModuleIds = modules.Select(module => module.Source.Id).ToHashSet(StringComparer.Ordinal);
var authenticationEnabled = !publicOnly || publicSignIn || modules.Count != 0;
builder.Services.AddSingleton(new ShellNavigation(!publicOnly, enabledModuleIds));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// AuthorizeView (MainLayout's account widget, App.razor's CascadingAuthenticationState) needs
// these registered regardless of mode - with no real authentication handler wired up in public-
// only mode, they simply resolve to an anonymous user, and AuthorizeView renders its
// NotAuthorized branch. AddForgeAuthorization only registers policy/handler definitions (no
// external connections of its own) - MainLayout checks the ForgeAdministrator policy on every
// page, so it has to exist even when nothing can ever satisfy it.
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddForgeAuthorization(builder.Environment, builder.Configuration);
builder.Services.AddScoped<IHomePageService, HomePageService>();
builder.Services.AddSingleton(TimeProvider.System);

if (authenticationEnabled)
    builder.Services.AddForgeCampusAuthentication(builder.Configuration, registerCampusIdentity: !publicOnly, registerModuleIdentity: modules.Count != 0);

if (publicOnly && authenticationEnabled)
{
    var keyDirectory = builder.Configuration["PublicSite:ProtectionKeyDirectory"]
        ?? throw new InvalidOperationException("Public sign-in requires a persistent PublicSite:ProtectionKeyDirectory.");
    builder.Services.AddDataProtection()
        .SetApplicationName("AethericForge.Web.Public")
        .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
}

if (!publicOnly)
{
    // The default Data Protection key ring is per-process, in-memory-or-local-disk only. Any login mid-
    // flight when the container restarts (or across replicas, once there's more than one) fails with
    // "Unable to unprotect the message.State" because the key that encrypted the OIDC state/correlation
    // values no longer exists. Persisting the ring to Redis - already a dependency here - makes it survive
    // restarts and be shared across replicas.
    //
    // PersistKeysToStackExchangeRedis invokes its factory delegate on every key-ring read/write, so a
    // lazily-created-but-shared multiplexer avoids reconnecting to Redis on every single Data Protection
    // operation (rather than opening a fresh connection each time).
    var dataProtectionRedisOptions = InstitutionServiceConfiguration
        .Resolve(builder.Configuration, "ForgeCampus", "Redis")
        .GetSection("Redis")
        .Get<RedisOptions>()!;
    var dataProtectionRedis = new Lazy<IConnectionMultiplexer>(
        () => ConnectionMultiplexer.Connect(dataProtectionRedisOptions.ToConfigurationOptions()));

    builder.Services.AddDataProtection()
        .SetApplicationName("AethericForge.Web")
        .PersistKeysToStackExchangeRedis(
            () => dataProtectionRedis.Value.GetDatabase(),
            "DataProtection-Keys");

    // Shared with aetheric-admin via the aetheric-contracts submodule - this app creates applications
    // through the public Join Campus form, aetheric-admin reads/flags them (StaleMembershipApplicationsWorker).
    var membershipMongoOptions = InstitutionServiceConfiguration
        .Resolve(builder.Configuration, "Membership", "MongoDb")
        .GetSection("MongoDb")
        .Get<MongoOptions>()!;
    builder.Services.AddKeyedMongoClient("Membership", membershipMongoOptions);
    builder.Services.AddSingleton<IMembershipApplicationStore, MongoMembershipApplicationStore>();

    builder.Services.AddSingleton<IMemberIdentityProvisioner, DevelopmentMemberIdentityProvisioner>();
    builder.Services.AddScoped<MembershipReviewService>();
    builder.Services.AddForgeCampus();
}


// Marketing stays optional in public-only mode and owns its Mongo credentials.
var marketingOptions = builder.Configuration.GetSection("Marketing:MongoDb").Exists()
    ? InstitutionServiceConfiguration.Resolve(builder.Configuration, "Marketing", "MongoDb").GetSection("MongoDb").Get<MongoOptions>()
    : null;
if (!string.IsNullOrWhiteSpace(marketingOptions?.Host))
    builder.Services.AddSingleton<ICampaignCatalog>(new MongoCampaignCatalog(marketingOptions));
else
    builder.Services.AddSingleton<ICampaignCatalog, EmptyCampaignCatalog>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    if (specs.Any(spec => !enabledModuleIds.Contains(spec.Id) && context.Request.Path.StartsWithSegments("/" + spec.Id)))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next(context);
});

if (authenticationEnabled)
{
    app.UseAuthentication();
}
// Explicit placement prevents ASP.NET Core from inserting authorization before
// the disabled-module gate when authentication is turned off in public mode.
app.UseAuthorization();
app.UseAntiforgery();

foreach (var module in modules) module.MapAssets(app);
app.MapStaticAssets();
if (!publicOnly)
{
    app.MapForgeCampusDiagnostics();
}
if (authenticationEnabled)
    app.MapForgeCampusAuthentication();
var components = app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
foreach (var module in modules) components.AddAdditionalAssemblies(module.WebAssembly);

app.Run();
