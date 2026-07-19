using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Commitment;

namespace ParallelYou.Models.Commitment;

public abstract class CommitmentRepresentationBase : ICommitmentRepresentation
{
    public Guid CommitmentId { get; init; }
    public Provenance Provenance { get; init; }
    public string? DisplayName { get; init; }

    protected CommitmentRepresentationBase(Guid commitmentId, Provenance provenance, string? displayName)
    {
        CommitmentId = commitmentId;
        Provenance = provenance;
        DisplayName = displayName;
    }
}
