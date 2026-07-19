namespace ParallelYou.Abstractions.Intention;

public interface IIntentionSubmission
{
    string Description { get; }
    Provenance Provenance { get; }
    bool IsAdopted { get; }
}
