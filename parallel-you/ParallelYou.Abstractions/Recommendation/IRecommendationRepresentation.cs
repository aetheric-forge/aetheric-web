namespace ParallelYou.Abstractions.Recommendation;

public interface IRecommendationRepresentation
{
    Guid Id { get; }
    Guid RecommendationId { get; }
    IRecommendationSubject Subject { get; }
    IReadOnlyCollection<IRecommendationCandidate> Candidates { get; }
    string Rationale { get; }
    IReadOnlyCollection<string> Assumptions { get; }
    decimal Confidence { get; }
    Provenance Provenance { get; }
    RecommendationResponse Response { get; }
    IReadOnlyCollection<Guid> SelectedCandidateIds { get; }
    Provenance? ResponseProvenance { get; }
}
