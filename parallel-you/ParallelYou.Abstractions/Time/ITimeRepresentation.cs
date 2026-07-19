namespace ParallelYou.Abstractions.Time;

public interface ITimeRepresentation
{
    Guid TimeId { get; }
    Provenance Provenance { get; }
    string? DisplayName { get; }
}
