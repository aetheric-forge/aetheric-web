using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Person;

namespace ParallelYou.Models.Person;

public abstract class PersonRepresentationBase : IPersonRepresentation
{
    public Guid PersonId { get; init; }
    public Provenance Provenance { get; init; }
    public string? DisplayName { get; init; }

    protected PersonRepresentationBase(Guid personId, Provenance provenance, string? displayName)
    {
        PersonId = personId;
        Provenance = provenance;
        DisplayName = displayName;
    }
}
