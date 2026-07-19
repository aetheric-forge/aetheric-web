using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;

namespace ParallelYou.Abstractions.Tracking;

public interface ITrackingService
{
    Task<ITrackedState> TrackAsync(
        ITrackedState state,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);

    Task<ITrackedState?> GetCurrentStateAsync(
        Guid personId,
        Guid subjectId,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ITrackedState>> GetCurrentStatesAsync(
        Guid personId,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ITrackedState>> GetHistoryAsync(
        Guid personId,
        Guid subjectId,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);
}
