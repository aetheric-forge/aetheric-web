using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Intention;

namespace ParallelYou.Models.Intention;

public abstract class IntentionBase : IIntention
{
    public Guid Id { get; init; }
    public Guid PersonId { get; init; }
    public IntentionRepresentation Representation { get; init; }

    IIntentionRepresentation IIntention.Representation => Representation;

    protected IntentionBase(
        Guid id,
        Guid personId,
        IntentionRepresentation representation)
    {
        Id = id;
        PersonId = personId;
        Representation = representation;
    }
}
