namespace ParallelYou.Abstractions.Plan;

public interface IPlanSubmission
{
    string DisplayName { get; }
    string Course { get; }
    IReadOnlyCollection<Guid> IntentionIds { get; }
    Provenance Provenance { get; }
    bool IsAccepted { get; }
}
