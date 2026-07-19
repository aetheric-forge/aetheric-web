namespace ParallelYou.Abstractions.Person;

public interface IPersonRepresentation
{
    Guid PersonId { get; }
    Provenance Provenance { get; }
    string? DisplayName { get; }
}
