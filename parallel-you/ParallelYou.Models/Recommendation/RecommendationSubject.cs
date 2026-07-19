using ParallelYou.Abstractions.Recommendation;

namespace ParallelYou.Models.Recommendation;

public sealed record RecommendationSubject(
    Guid PlanId,
    string Description) : IRecommendationSubject;
