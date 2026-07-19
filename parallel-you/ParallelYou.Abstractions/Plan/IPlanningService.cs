using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;

namespace ParallelYou.Abstractions.Plan;

public interface IPlanningService
{
    Task<IPlan> CreatePlanAsync(
        Guid personId,
        IPlanSubmission submission,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);

    Task<IPlan?> RevisePlanAsync(
        Guid personId,
        Guid planId,
        IPlanSubmission submission,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IPlan>> GetPlansAsync(
        Guid personId,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IPlanRepresentation>> GetHistoryAsync(
        Guid personId,
        Guid planId,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);
}
