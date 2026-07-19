namespace ParallelYou.Abstractions.Recommendation;

public interface IRecommendationSubmission
{
    IRecommendationSubject Subject { get; }
    IReadOnlyCollection<IRecommendationCandidate> Candidates { get; }
    string Rationale { get; }
    IReadOnlyCollection<string> Assumptions { get; }
    decimal Confidence { get; }
    Provenance Provenance { get; }
}
