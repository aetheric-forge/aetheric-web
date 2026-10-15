using System.Security.Claims;
using AethericForge.Runtime.Abstractions.Interfaces.IssueReports.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.IssueReports.Services;
using AethericForge.Runtime.Abstractions.Interfaces.Security.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.Security.Services;
using AethericForge.Web.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace AethericForge.Web.Services;

/// <summary>Campus Operations uses a fixed custody domain and the signed-in administrator's identity.</summary>
public sealed class OperationsWorkspace(IWarden warden, ISentinel sentinel,
    AuthenticationStateProvider authentication, IAuthorizationService authorization)
{
    public const string Domain = "aetheric-web";

    private async Task<string> RequireAdministratorAsync()
    {
        var user = (await authentication.GetAuthenticationStateAsync()).User;
        if (user.Identity?.IsAuthenticated != true ||
            !(await authorization.AuthorizeAsync(user, null, ForgeAuthorizationExtensions.AdministratorPolicy)).Succeeded)
            throw new UnauthorizedAccessException("Campus administration access is required.");
        return user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("A signed-in subject is required.");
    }

    public async Task<IReadOnlyList<IssueReport>> ListIssuesAsync()
    {
        await RequireAdministratorAsync();
        return await warden.ListAsync(Domain);
    }

    public async Task SubmitIssueAsync(string title, string description)
    {
        var actor = await RequireAdministratorAsync();
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        await warden.SubmitAsync(Domain, title.Trim(), description.Trim(), actor);
    }

    public async Task UpdateIssueAsync(Guid id, IssueStatus status, string? note)
    {
        await RequireAdministratorAsync();
        if (!Enum.IsDefined(status)) throw new ArgumentException("Choose a valid status.");
        if (await warden.UpdateStatusAsync(Domain, id, status, note) is null)
            throw new InvalidOperationException("The issue no longer exists. Reload the page.");
    }

    public async Task<IReadOnlyList<SecurityRecord>> ListSecurityAsync()
    {
        await RequireAdministratorAsync();
        return await sentinel.ListAsync(Domain);
    }

    public async Task RaiseSecurityAsync(SecurityRecordKind kind, string title, string description, SecuritySeverity severity)
    {
        var actor = await RequireAdministratorAsync();
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(severity)) throw new ArgumentException("Choose a valid kind and severity.");
        await sentinel.RaiseAsync(Domain, kind, title.Trim(), description.Trim(), actor, severity);
    }

    public async Task UpdateSecurityAsync(Guid id, SecurityStatus status, string? note)
    {
        var actor = await RequireAdministratorAsync();
        if (!Enum.IsDefined(status)) throw new ArgumentException("Choose a valid status.");
        if (await sentinel.UpdateStatusAsync(Domain, id, status, actor, note) is null)
            throw new InvalidOperationException("The security record no longer exists. Reload the page.");
    }

    public async Task<IReadOnlyList<SecurityRecordEvent>> HistoryAsync(Guid id)
    {
        await RequireAdministratorAsync();
        return await sentinel.GetHistoryAsync(Domain, id);
    }
}
