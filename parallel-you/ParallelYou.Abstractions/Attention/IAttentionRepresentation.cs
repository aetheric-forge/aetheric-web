namespace ParallelYou.Abstractions.Attention;

public interface IAttentionRepresentation
{
    Guid AttentionId { get; }
    Provenance Provenance { get; }
    string? DisplayName { get; }
}
