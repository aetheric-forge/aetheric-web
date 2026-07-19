using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Primitives;

namespace ParallelYou.Abstractions.Reflection;

public interface IReflectionService
{
    Task<IReflection> StartReflectionAsync(
        Guid personId,
        IReflectionSubject subject,
        IEnumerable<string> questions,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);

    Task<bool> AddEvidenceAsync(
        Guid reflectionId,
        IReflectionEvidence evidence,
        IKnowledgeAuthority authority,
        IKnowledgeReference? evidenceReference = null,
        CancellationToken cancellationToken = default);

    Task<bool> AddInsightAsync(
        Guid reflectionId,
        IReflectionInsightSubmission insight,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IReflection>> GetReflectionsAsync(
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);
}
