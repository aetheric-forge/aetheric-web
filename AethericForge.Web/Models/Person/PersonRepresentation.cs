using AethericForge.Web.Abstractions;

namespace AethericForge.Web.Models.Person;

public sealed class PersonRepresentation(
    Guid personId,
    Provenance provenance,
    string? displayName)
    : PersonRepresentationBase(personId, provenance, displayName);
