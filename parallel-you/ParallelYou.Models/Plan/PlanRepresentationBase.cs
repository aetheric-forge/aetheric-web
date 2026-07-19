using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Plan;

namespace ParallelYou.Models.Plan;

public abstract class PlanRepresentationBase : IPlanRepresentation
{
    public Guid Id { get; init; }
    public Guid PlanId { get; init; }
    public Provenance Provenance { get; init; }
    public string DisplayName { get; init; }
    public string Course { get; init; }
    public IReadOnlyCollection<Guid> IntentionIds { get; init; }
    public bool IsAccepted { get; init; }

    protected PlanRepresentationBase(
        Guid id,
        Guid planId,
        Provenance provenance,
        string displayName,
        string course,
        IReadOnlyCollection<Guid> intentionIds,
        bool isAccepted)
    {
        Id = id;
        PlanId = planId;
        Provenance = provenance;
        DisplayName = displayName;
        Course = course;
        IntentionIds = intentionIds;
        IsAccepted = isAccepted;
    }
}
