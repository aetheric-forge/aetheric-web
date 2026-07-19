namespace ParallelYou.Abstractions.Recommendation;

public interface IRecommendation
{
    Guid Id { get; }
    Guid PersonId { get; }
    IRecommendationRepresentation Representation { get; }
}
