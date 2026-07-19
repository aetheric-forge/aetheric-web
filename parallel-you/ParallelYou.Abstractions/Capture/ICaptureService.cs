using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;

namespace ParallelYou.Abstractions.Capture;

public interface ICaptureService
{
    Task<IKnowledgeArtifact> CaptureAsync(
        ICaptureEvidence evidence,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<IKnowledgeArtifact>> GetCapturesAsync(
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default);
}
