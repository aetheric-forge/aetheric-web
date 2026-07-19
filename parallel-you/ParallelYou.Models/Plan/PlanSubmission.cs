using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Plan;

namespace ParallelYou.Models.Plan;

public sealed record PlanSubmission(
    string DisplayName,
    string Course,
    IReadOnlyCollection<Guid> IntentionIds,
    Provenance Provenance,
    bool IsAccepted) : IPlanSubmission;
