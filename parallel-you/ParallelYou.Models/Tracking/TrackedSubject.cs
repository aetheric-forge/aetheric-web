using ParallelYou.Abstractions.Tracking;

namespace ParallelYou.Models.Tracking;

public sealed record TrackedSubject(
    Guid Id,
    string Type,
    string Description) : ITrackedSubject;
