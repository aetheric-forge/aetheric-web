using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;

namespace ParallelYou.Abstractions.Intention;

public interface IIntentionService
{
    Task<IIntention> CreateIntentionAsync(
        Guid personId,
        IIntentionSubmission submission,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);

    Task<IIntention?> ReviseIntentionAsync(
        Guid personId,
        Guid intentionId,
        IIntentionSubmission submission,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IIntention>> GetIntentionsAsync(
        Guid personId,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IIntentionRepresentation>> GetHistoryAsync(
        Guid personId,
        Guid intentionId,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);
}
