namespace AethericForge.Web.Membership;

public sealed class DevelopmentMemberIdentityProvisioner(
    ILogger<DevelopmentMemberIdentityProvisioner> logger) : IMemberIdentityProvisioner
{
    public Task<MemberProvisioningResult> ProvisionAsync(
        MemberProvisioningRequest request,
        CancellationToken cancellationToken = default)
    {
        var identityId = $"development:{request.ApplicationId:N}";
        logger.LogInformation(
            "Development provisioner recorded member {Email} as {IdentityId}",
            request.Email,
            identityId);

        return Task.FromResult(new MemberProvisioningResult(identityId));
    }
}
