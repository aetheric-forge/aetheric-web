using System.Text;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Artifacts;
using AethericForge.Runtime.Abstractions.Interfaces.Knowledge.Authorities;
using AethericForge.Runtime.Abstractions.Interfaces.Library.Services;
using AethericForge.Runtime.Models.Knowledge.Primitives;
using AethericForge.Runtime.Models.Knowledge.Representations;
using ParallelYou.Abstractions.Capture;

namespace ParallelYou.Services.Capture;

public class CaptureService(ILibrarian librarian) : ServiceBase, ICaptureService
{
    public async Task<IKnowledgeArtifact> CaptureAsync(
        ICaptureEvidence evidence,
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(authority);
        
        var descriptor = new KnowledgeDescriptor(evidence.Title);
        var content = Encoding.UTF8.GetBytes(evidence.Content);
        var representation = new KnowledgeRepresentation(
            "text/plain",
            content.LongLength,
            _ => Task.FromResult<Stream>(new MemoryStream(content)),
            encoding: "utf-8"
        );
        var artifact = await librarian.PublishArtifactAsync(
            descriptor,
            [representation],
            authority: authority,
            ct: cancellationToken);
        
        return artifact;
    }

    public Task<IReadOnlyCollection<IKnowledgeArtifact>> GetCapturesAsync(
        IKnowledgeAuthority authority,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authority);
        return librarian.FindArtifactsAsync(authority, cancellationToken);
    }
}
