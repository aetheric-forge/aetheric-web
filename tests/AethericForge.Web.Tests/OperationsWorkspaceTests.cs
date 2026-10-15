using System.Security.Claims;
using AethericForge.Runtime.Abstractions.Interfaces.IssueReports.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.IssueReports.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Security.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Security.Services;
using AethericForge.Runtime.Models.Authorities;
using AethericForge.Runtime.Providers.Staging.InMemory;
using AethericForge.Runtime.Services.IssueReports;
using AethericForge.Runtime.Services.Security;
using AethericForge.Runtime.Services.Staging;
using AethericForge.Web.Hosting;
using AethericForge.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AethericForge.Web.Tests;

public sealed class OperationsWorkspaceTests
{
    private sealed class Authentication(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(user));
    }

    private static ClaimsPrincipal User(bool administrator) => new(new ClaimsIdentity(
        administrator ? [new Claim("sub", "admin-subject"), new Claim("groups", "/forge-admins")]
            : [new Claim("sub", "ordinary-user")], "test"));

    private static OperationsWorkspace Workspace(StagingService staging, ClaimsPrincipal user)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddAuthorizationBuilder().AddPolicy(ForgeAuthorizationExtensions.AdministratorPolicy,
            policy => policy.RequireClaim("groups", "/forge-admins"));
        var provider = services.BuildServiceProvider();
        var store = new OperationsLedgerStore(staging, "existing-owner-stage");
        return new(new Warden(store, new Team<IIssueReportsClerk>([])),
            new Sentinel(store, new Team<ISecurityClerk>([])), new Authentication(user),
            provider.GetRequiredService<IAuthorizationService>());
    }

    [Fact]
    public async Task Issues_and_security_survive_new_service_instances_with_actor_history()
    {
        var staging = new StagingService([new InMemoryStagingProvider("existing-owner-stage")]);
        var workspace = Workspace(staging, User(true));
        await workspace.SubmitIssueAsync("Broken link", "The documentation link fails.");
        await workspace.RaiseSecurityAsync(SecurityRecordKind.AuditFinding, "Review access", "An access review is due.", SecuritySeverity.High);
        var issue = Assert.Single(await workspace.ListIssuesAsync());
        var record = Assert.Single(await workspace.ListSecurityAsync());
        await workspace.UpdateIssueAsync(issue.Id, IssueStatus.Resolved, "Link repaired.");
        await workspace.UpdateSecurityAsync(record.Id, SecurityStatus.Investigating, "Review started.");

        var reloaded = Workspace(staging, User(true));
        var savedIssue = Assert.Single(await reloaded.ListIssuesAsync());
        Assert.Equal(IssueStatus.Resolved, savedIssue.Status);
        Assert.Equal("Link repaired.", savedIssue.ResolutionNote);
        Assert.Equal("admin-subject", savedIssue.ReporterId);
        Assert.NotNull(savedIssue.ResolvedAt);
        Assert.Equal(OperationsWorkspace.Domain, savedIssue.Domain);
        Assert.Equal(SecurityStatus.Investigating, Assert.Single(await reloaded.ListSecurityAsync()).Status);
        var history = await reloaded.HistoryAsync(record.Id);
        Assert.Equal(2, history.Count);
        Assert.All(history, item => Assert.Equal("admin-subject", item.ActorId));
        Assert.Null(history[0].PreviousStatus);
        Assert.Equal(SecurityStatus.Open, history[1].PreviousStatus);
        Assert.Equal("Review started.", history[1].Note);
        Assert.Empty(await reloaded.HistoryAsync(Guid.NewGuid()));
        // The same stage can host unrelated catalogs without exposing them through this domain.
        var otherWarden = new Warden(new OperationsLedgerStore(staging, "existing-owner-stage"), new Team<IIssueReportsClerk>([]));
        await otherWarden.SubmitAsync("another-app", "Private", "Other domain", "other");
        Assert.Single(await reloaded.ListIssuesAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Anonymous_and_non_administrators_cannot_read_or_mutate_ledgers(bool authenticated)
    {
        var staging = new StagingService([new InMemoryStagingProvider("existing-owner-stage")]);
        var user = authenticated ? User(false) : new ClaimsPrincipal(new ClaimsIdentity());
        var workspace = Workspace(staging, user);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => workspace.ListIssuesAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => workspace.ListSecurityAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => workspace.HistoryAsync(Guid.NewGuid()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => workspace.SubmitIssueAsync("Title", "Details"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => workspace.RaiseSecurityAsync(SecurityRecordKind.Incident, "Title", "Details", SecuritySeverity.Low));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => workspace.UpdateIssueAsync(Guid.NewGuid(), IssueStatus.Resolved, "No"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => workspace.UpdateSecurityAsync(Guid.NewGuid(), SecurityStatus.Closed, "No"));
        Assert.Empty(await Workspace(staging, User(true)).ListIssuesAsync());
        Assert.Empty(await Workspace(staging, User(true)).ListSecurityAsync());
    }

    [Fact]
    public async Task Missing_records_and_invalid_input_do_not_report_success()
    {
        var workspace = Workspace(new StagingService([new InMemoryStagingProvider("existing-owner-stage")]), User(true));
        await Assert.ThrowsAsync<InvalidOperationException>(() => workspace.UpdateIssueAsync(Guid.NewGuid(), IssueStatus.Resolved, "Note"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => workspace.UpdateSecurityAsync(Guid.NewGuid(), SecurityStatus.Closed, "Note"));
        await Assert.ThrowsAsync<ArgumentException>(() => workspace.SubmitIssueAsync(" ", "Details"));
        await Assert.ThrowsAsync<ArgumentException>(() => workspace.RaiseSecurityAsync((SecurityRecordKind)999, "Title", "Details", SecuritySeverity.Low));
        await Assert.ThrowsAsync<ArgumentException>(() => workspace.UpdateSecurityAsync(Guid.NewGuid(), (SecurityStatus)999, "Note"));
    }
}
