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

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
builder.Services.AddForgeAuthorization(builder.Environment, builder.Configuration);
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<IHomePageService, HomePageService>();

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

builder.Services.AddSingleton(TimeProvider.System);

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

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapForgeCampusDiagnostics();
app.MapForgeCampusAuthentication();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
