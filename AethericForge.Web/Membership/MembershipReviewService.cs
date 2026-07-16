namespace AethericForge.Web.Membership;

public sealed class MembershipReviewService(
    IMembershipApplicationStore store,
    IMemberIdentityProvisioner provisioner)
{
    public async Task ApproveAsync(
        Guid id,
        string reviewer,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var application = await RequireApplicationAsync(id, cancellationToken);
        application.Status = MembershipApplicationStatus.Approved;
        application.ReviewedBy = reviewer;
        application.ReviewedAt = DateTimeOffset.UtcNow;
        application.ReviewNotes = notes;
        await store.SaveAsync(application, cancellationToken);

        await TryProvisionAsync(application, cancellationToken);
    }

    public async Task RetryProvisioningAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var application = await RequireApplicationAsync(id, cancellationToken);
        if (application.Status != MembershipApplicationStatus.Approved || application.ProvisioningStatus != IdentityProvisioningStatus.Failed)
        {
            throw new InvalidOperationException($"Application {id} cannot be retried.");
        }

        await TryProvisionAsync(application, cancellationToken);
    }

    private async Task TryProvisionAsync(
        MembershipApplication application,
        CancellationToken cancellationToken = default)
    {
        application.ProvisioningStatus = IdentityProvisioningStatus.InProgress;
        application.ProvisioningError = null;
        await store.SaveAsync(application, cancellationToken);

        try
        {
            var result = await provisioner.ProvisionAsync(
                new MemberProvisioningRequest(
                    application.Id,
                    application.DisplayName,
                    application.Email,
                    application.RequestedUsername),
                cancellationToken);

            application.ExternalIdentityId = result.ExternalIdentityId;
            application.ProvisioningStatus = IdentityProvisioningStatus.Provisioned;
            application.ProvisioningError = null;
        }
        catch (Exception exception)
        {
            application.ProvisioningStatus = IdentityProvisioningStatus.Failed;
            application.ProvisioningError = exception.Message;
        }

        await store.SaveAsync(application, cancellationToken);
    }

    public async Task DeclineAsync(
        Guid id,
        string reviewer,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var application = await RequireApplicationAsync(id, cancellationToken);
        application.Status = MembershipApplicationStatus.Declined;
        application.ReviewedBy = reviewer;
        application.ReviewedAt = DateTimeOffset.UtcNow;
        application.ReviewNotes = notes;
        await store.SaveAsync(application, cancellationToken);
    }

    private async Task<MembershipApplication> RequireApplicationAsync(Guid id, CancellationToken cancellationToken) =>
        await store.FindAsync(id, cancellationToken)
        ?? throw new InvalidOperationException($"Membership application {id} was not found.");
}
