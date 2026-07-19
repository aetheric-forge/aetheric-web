using ParallelYou.Abstractions;

namespace ParallelYou.Models.Person;

public sealed class PersonRepresentation(
    Guid personId,
    Provenance provenance,
    string? displayName)
    : PersonRepresentationBase(personId, provenance, displayName);
