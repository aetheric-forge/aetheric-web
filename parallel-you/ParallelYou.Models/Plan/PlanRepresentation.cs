using ParallelYou.Abstractions;

namespace ParallelYou.Models.Plan;

public sealed class PlanRepresentation : PlanRepresentationBase
{
    public PlanRepresentation(
        Guid id,
        Guid planId,
        Provenance provenance,
        string displayName,
        string course,
        IReadOnlyCollection<Guid> intentionIds,
        bool isAccepted)
        : base(
            id,
            planId,
            provenance,
            displayName,
            course,
            intentionIds,
            isAccepted)
    {
    }
}
