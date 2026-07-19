namespace ParallelYou.Abstractions.Intention;

public interface IIntention
{
    Guid Id { get; }
    Guid PersonId { get; }
    IIntentionRepresentation Representation { get; }
}
