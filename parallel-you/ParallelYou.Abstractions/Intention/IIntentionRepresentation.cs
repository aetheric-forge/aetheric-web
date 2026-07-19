namespace ParallelYou.Abstractions.Intention;

public interface IIntentionRepresentation
{
    Guid Id { get; }
    Guid IntentionId { get; }
    Provenance Provenance { get; }
    string Description { get; }
    bool IsAdopted { get; }
}
