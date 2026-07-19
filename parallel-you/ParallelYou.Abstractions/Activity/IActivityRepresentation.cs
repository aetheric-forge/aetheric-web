namespace ParallelYou.Abstractions.Activity;

public interface IActivityRepresentation
{
    Guid ActivityId { get; }
    Provenance Provenance { get; }
    string? DisplayName { get; }
}
