using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Recommendation;

namespace ParallelYou.Models.Recommendation;

public sealed record RecommendationSubmission(
    RecommendationSubject Subject,
    IReadOnlyCollection<RecommendationCandidate> Candidates,
    string Rationale,
    IReadOnlyCollection<string> Assumptions,
    decimal Confidence,
    Provenance Provenance) : IRecommendationSubmission
{
    IRecommendationSubject IRecommendationSubmission.Subject => Subject;

    IReadOnlyCollection<IRecommendationCandidate>
        IRecommendationSubmission.Candidates => Candidates;
}
