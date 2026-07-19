using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Attention;

namespace ParallelYou.Models.Attention;

public abstract class AttentionRepresentationBase : IAttentionRepresentation
{
    public Guid AttentionId { get; init; }
    public Provenance Provenance { get; init; }
    public string? DisplayName { get; init; }

    protected AttentionRepresentationBase(Guid attentionId, Provenance provenance, string? displayName)
    {
        AttentionId = attentionId;
        Provenance = provenance;
        DisplayName = displayName;
    }
}
