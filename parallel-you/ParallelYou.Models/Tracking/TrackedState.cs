using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Tracking;

namespace ParallelYou.Models.Tracking;

public sealed record TrackedState(
    Guid Id,
    Guid PersonId,
    TrackedSubject Subject,
    string Value,
    Provenance Provenance,
    decimal Confidence) : ITrackedState
{
    ITrackedSubject ITrackedState.Subject => Subject;
}
