using ParallelYou.Abstractions.Recommendation;

namespace ParallelYou.Models.Recommendation;

public sealed record RecommendationCandidate(
    Guid Id,
    Guid PlanId,
    RecommendationCandidateKind Kind,
    string Description) : IRecommendationCandidate;
