using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Time;

namespace ParallelYou.Models.Time;

public abstract class TimeRepresentationBase : ITimeRepresentation
{
    public Guid TimeId { get; init; }
    public Provenance Provenance { get; init; }
    public string? DisplayName { get; init; }

    protected TimeRepresentationBase(Guid timeId, Provenance provenance, string? displayName)
    {
        TimeId = timeId;
        Provenance = provenance;
        DisplayName = displayName;
    }
}
