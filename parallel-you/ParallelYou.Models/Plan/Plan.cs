namespace ParallelYou.Models.Plan;

public class Plan : PlanBase
{
    public Plan(
        Guid id,
        Guid personId,
        PlanRepresentation representation)
        : base(id, personId, representation)
    {
    }
}
