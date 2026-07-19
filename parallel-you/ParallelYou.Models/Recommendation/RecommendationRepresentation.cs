using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Recommendation;

namespace ParallelYou.Models.Recommendation;

public sealed record RecommendationRepresentation(
    Guid Id,
    Guid RecommendationId,
    RecommendationSubject Subject,
    IReadOnlyCollection<RecommendationCandidate> Candidates,
    string Rationale,
    IReadOnlyCollection<string> Assumptions,
    decimal Confidence,
    Provenance Provenance,
    RecommendationResponse Response,
    IReadOnlyCollection<Guid> SelectedCandidateIds,
    Provenance? ResponseProvenance) : IRecommendationRepresentation
{
    IRecommendationSubject IRecommendationRepresentation.Subject => Subject;

    IReadOnlyCollection<IRecommendationCandidate>
        IRecommendationRepresentation.Candidates => Candidates;
}
