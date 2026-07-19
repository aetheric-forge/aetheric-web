using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Intention;

namespace ParallelYou.Models.Intention;

public abstract class IntentionRepresentationBase : IIntentionRepresentation
{
    public Guid Id { get; init; }
    public Guid IntentionId { get; init; }
    public Provenance Provenance { get; init; }
    public string Description { get; init; }
    public bool IsAdopted { get; init; }

    protected IntentionRepresentationBase(
        Guid id,
        Guid intentionId,
        Provenance provenance,
        string description,
        bool isAdopted)
    {
        Id = id;
        IntentionId = intentionId;
        Provenance = provenance;
        Description = description;
        IsAdopted = isAdopted;
    }
}
