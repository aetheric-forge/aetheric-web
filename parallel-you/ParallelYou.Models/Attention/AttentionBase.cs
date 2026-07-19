using ParallelYou.Abstractions;
using ParallelYou.Abstractions.Attention;

namespace ParallelYou.Models.Attention;

public abstract class AttentionBase : IAttention
{
    public Guid Id { get; init; }

    protected AttentionBase(Guid id)
    {
        Id = id;
    }
}
