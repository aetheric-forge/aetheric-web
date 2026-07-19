namespace ParallelYou.Abstractions.Recommendation;

public interface IRecommendationCandidate
{
    Guid Id { get; }
    Guid PlanId { get; }
    RecommendationCandidateKind Kind { get; }
    string Description { get; }
}
