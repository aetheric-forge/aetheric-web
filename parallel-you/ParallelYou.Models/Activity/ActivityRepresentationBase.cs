using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Activity;

namespace ParallelYou.Models.Activity;

public abstract class ActivityRepresentationBase : IActivityRepresentation
{
    public Guid ActivityId { get; init; }
    public Provenance Provenance { get; init; }
    public string? DisplayName { get; init; }

    protected ActivityRepresentationBase(Guid activityId, Provenance provenance, string? displayName)
    {
        ActivityId = activityId;
        Provenance = provenance;
        DisplayName = displayName;
    }
}
