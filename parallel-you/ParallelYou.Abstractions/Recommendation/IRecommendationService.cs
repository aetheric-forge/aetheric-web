using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;

namespace ParallelYou.Abstractions.Recommendation;

public interface IRecommendationService
{
    Task<IRecommendation> PresentRecommendationAsync(
        Guid personId,
        IRecommendationSubmission submission,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);

    Task<IRecommendation?> ReviseRecommendationAsync(
        Guid personId,
        Guid recommendationId,
        IRecommendationSubmission submission,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);

    Task<IRecommendation?> RecordResponseAsync(
        Guid personId,
        Guid recommendationId,
        RecommendationResponse response,
        IReadOnlyCollection<Guid> selectedCandidateIds,
        Provenance responseProvenance,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IRecommendation>> GetRecommendationsAsync(
        Guid personId,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IRecommendationRepresentation>> GetHistoryAsync(
        Guid personId,
        Guid recommendationId,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);
}
