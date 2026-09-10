using AethericForge.Runtime.Abstractions.Interfaces.Maintenance.Services;
using AethericForge.Web.Components;
using AethericForge.Web.Hosting;
using AethericForge.Web.Maintenance;
using AethericForge.Web.Membership;
using AethericForge.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using StackExchange.Redis;

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
var dataProtectionRedis = new Lazy<IConnectionMultiplexer>(() => ConnectionMultiplexer.Connect(new ConfigurationOptions
{
    EndPoints = { { builder.Configuration["Redis:Host"]!, builder.Configuration.GetValue<int?>("Redis:Port") ?? 6379 } },
    Password = builder.Configuration["Redis:Password"],
    Ssl = builder.Configuration.GetValue<bool>("Redis:Ssl"),
    DefaultDatabase = builder.Configuration.GetValue<int?>("Redis:Database") ?? 0,
    AbortOnConnectFail = false
}));

builder.Services.AddDataProtection()
    .SetApplicationName("AethericForge.Web")
    .PersistKeysToStackExchangeRedis(
        () => dataProtectionRedis.Value.GetDatabase(),
        "DataProtection-Keys");

builder.Services.AddForgeCampusAuthentication(builder.Configuration);
builder.Services.AddForgeAuthorization(builder.Environment, builder.Configuration);
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<IHomePageService, HomePageService>();
builder.Services.AddSingleton<IMembershipApplicationStore, InMemoryMembershipApplicationStore>();
builder.Services.AddSingleton<IMemberIdentityProvisioner, DevelopmentMemberIdentityProvisioner>();
builder.Services.AddScoped<MembershipReviewService>();
builder.Services.AddForgeCampus();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IMaintenanceWorker, StaleMembershipApplicationsWorker>();
builder.Services.AddHostedService<MaintenanceDispatchService>();

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
