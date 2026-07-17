namespace AethericForge.Web.Membership;

public sealed record MemberProvisioningRequest(
    Guid ApplicationId,
    string DisplayName,
    string Email,
    string RequestedUsername);

public sealed record MemberProvisioningResult(string ExternalIdentityId);

public interface IMemberIdentityProvisioner
{
    Task<MemberProvisioningResult> ProvisionAsync(
        MemberProvisioningRequest request,
        CancellationToken cancellationToken = default);
}
