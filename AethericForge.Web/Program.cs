using AdrCampus.Web.Members;
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

// Defaults to public content only (Home/Projects/About/Articles/Videos - no /campus/* member or
// admin routes) with no external infrastructure required at all. Flip to false once Keycloak,
// Redis, and the Membership Mongo connection are configured and ready to come back online - the
// full campus/membership/authentication wiring below is unchanged, just conditional now.
var publicOnly = builder.Configuration.GetValue("PublicSite:Enabled", true);

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
// ActiveMemberAuthorizationHandler/ActiveMaintainerAuthorizationHandler (registered by
// AddForgeAuthorization above) depend on this - ASP.NET Core validates the whole DI graph at
// Build() time in Development, so it has to exist even though only /campus/* routes ever
// actually trigger those two policies. Only makes live calls when actually invoked, not at
// construction, so it's harmless to always register.
builder.Services.AddHttpClient(MemberRosterService.HttpClientName);
builder.Services.AddSingleton<MemberRosterService>();

builder.Services.AddScoped<IHomePageService, HomePageService>();
builder.Services.AddSingleton(TimeProvider.System);

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

    builder.Services.AddForgeCampusAuthentication(builder.Configuration);

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

if (!publicOnly)
{
    app.UseAuthentication();
    app.UseAuthorization();
}
app.UseAntiforgery();

app.MapStaticAssets();
if (!publicOnly)
{
    app.MapForgeCampusDiagnostics();
    app.MapForgeCampusAuthentication();
}
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
