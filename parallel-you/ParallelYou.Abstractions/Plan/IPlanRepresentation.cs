namespace ParallelYou.Abstractions.Plan;

public interface IPlanRepresentation
{
    Guid Id { get; }
    Guid PlanId { get; }
    Provenance Provenance { get; }
    string DisplayName { get; }
    string Course { get; }
    IReadOnlyCollection<Guid> IntentionIds { get; }
    bool IsAccepted { get; }
}
