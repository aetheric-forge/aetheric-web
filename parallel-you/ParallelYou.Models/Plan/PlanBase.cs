using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Plan;

namespace ParallelYou.Models.Plan;

public abstract class PlanBase : IPlan
{
    public Guid Id { get; init; }
    public Guid PersonId { get; init; }
    public PlanRepresentation Representation { get; init; }

    IPlanRepresentation IPlan.Representation => Representation;

    protected PlanBase(
        Guid id,
        Guid personId,
        PlanRepresentation representation)
    {
        Id = id;
        PersonId = personId;
        Representation = representation;
    }
}
