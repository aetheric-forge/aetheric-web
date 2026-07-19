namespace ParallelYou.Abstractions.Recommendation;

public interface IRecommendationSubject
{
    Guid PlanId { get; }
    string Description { get; }
}
