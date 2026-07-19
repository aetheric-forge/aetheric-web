namespace ParallelYou.Abstractions.Tracking;

public interface ITrackedSubject
{
    Guid Id { get; }
    string Type { get; }
    string Description { get; }
}
