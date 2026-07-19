using ParallelYou.Abstractions.Recommendation;

namespace ParallelYou.Models.Recommendation;

public sealed record Recommendation(
    Guid Id,
    Guid PersonId,
    RecommendationRepresentation Representation) : IRecommendation
{
    IRecommendationRepresentation IRecommendation.Representation => Representation;
}
