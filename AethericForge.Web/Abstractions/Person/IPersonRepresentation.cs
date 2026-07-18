using ParallelYou.Abstractions;

namespace AethericForge.Web.Abstractions.Person;

public interface IPersonRepresentation
{
    Guid PersonId { get; }
    Provenance Provenance { get; }
    string? DisplayName { get; }
}
