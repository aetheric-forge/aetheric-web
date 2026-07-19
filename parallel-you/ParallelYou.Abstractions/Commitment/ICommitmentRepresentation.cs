namespace ParallelYou.Abstractions.Commitment;

public interface ICommitmentRepresentation
{
    Guid CommitmentId { get; }
    Provenance Provenance { get; }
    string? DisplayName { get; }
}
