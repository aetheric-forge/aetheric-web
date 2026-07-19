using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Commitment;

namespace ParallelYou.Models.Commitment;

public abstract class CommitmentBase : ICommitment
{
    public Guid Id { get; init; }

    protected CommitmentBase(Guid id)
    {
        Id = id;
    }
}
