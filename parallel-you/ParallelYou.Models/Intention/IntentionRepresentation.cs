using ParallelYou.Abstractions;

namespace ParallelYou.Models.Intention;

public sealed class IntentionRepresentation : IntentionRepresentationBase
{
    public IntentionRepresentation(
        Guid id,
        Guid intentionId,
        Provenance provenance,
        string description,
        bool isAdopted)
        : base(id, intentionId, provenance, description, isAdopted)
    {
    }
}
