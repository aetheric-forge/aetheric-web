namespace ParallelYou.Abstractions.Plan;

public interface IPlan
{
    Guid Id { get; }
    Guid PersonId { get; }
    IPlanRepresentation Representation { get; }
}
