using AethericForge.Web.Abstractions;
using AethericForge.Web.Abstractions.Person;

namespace AethericForge.Web.Models.Person;

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
